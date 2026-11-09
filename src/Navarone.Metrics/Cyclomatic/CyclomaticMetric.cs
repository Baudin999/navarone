using System.Globalization;
using Navarone.Engine.Configuration;
using Navarone.Engine.Facts;
using Navarone.Engine.Findings;
using Navarone.Engine.Metrics;

namespace Navarone.Metrics.Cyclomatic;

/// <summary>
/// McCabe 1976: the decisions in a method, plus one. The value is computed from the method's evidence, so the proof a
/// reader checks is the count itself.
/// </summary>
public sealed class CyclomaticMetric : ISignal
{
    /// <summary>What every method starts at: the one path through a method without decisions.</summary>
    public const int Base = 1;

    /// <inheritdoc />
    public string Id => "cyclomatic";

    /// <inheritdoc />
    public SubjectKind Subject => SubjectKind.Method;

    /// <summary>The cyclomatic value of one method, whatever the limit. split-complexity reads it for a method and its helpers.</summary>
    public static int ValueOf(MethodFacts method) => EvidenceOf(method).Value();

    /// <summary>
    /// How the method's value comes to life: its code, the base, and one proof item per construct at the top of a
    /// statement, each with the constructs inside it as parts.
    /// </summary>
    public static Evidence EvidenceOf(MethodFacts method)
    {
        var parts = method.ControlFlow.ToLookup(fact => (object?)fact.PartOf, ReferenceEqualityComparer.Instance);
        List<ProofItem> Items(object? whole) =>
        [
            .. parts[whole].Select(fact =>
            {
                var (adds, why) = Rule(fact.Kind);
                return new ProofItem(fact.Span, fact.Construct, adds, why, Items(fact));
            }),
        ];

        return new Evidence(CodePiece.Of(method), Base, Items(null));
    }

    /// <inheritdoc />
    public IReadOnlyList<Finding> Measure(CodebaseFacts facts, CheckLimits limits)
    {
        var limit = limits.For(Id);
        return
        [
            .. from method in facts.Types.SelectMany(type => type.Methods)
               let evidence = EvidenceOf(method)
               let value = evidence.Value()
               where value > limit
               select new Finding(Id, method.Symbol, Subject, method.Location, value, limit, Question(value, limit), Evidence: evidence),
        ];
    }

    // What each kind of construct adds, and why. McCabe counts predicates, whatever syntax writes them: a when, a
    // subpattern or list element that tests a value and a list's length each decide like an if. A plain else, a switch
    // as a whole, a default case and a jump add no path of their own: a case or a branch already counts it.
    private static (int Adds, string Why) Rule(ControlFlowKind kind) => kind switch
    {
        ControlFlowKind.Branch => (1, "a branch adds one path"),
        ControlFlowKind.Else => (0, "a plain else adds no path: the branch it ends already counts its second path"),
        ControlFlowKind.Switch => (0, "a switch adds no path of its own: each of its cases counts"),
        ControlFlowKind.SwitchCase => (1, "a case adds one path"),
        ControlFlowKind.Default => (0, "a default case adds no path: it tests nothing, and a when on it counts as its own"),
        ControlFlowKind.Guard => (1, "a when adds one path: its case or catch can fail after its pattern matched"),
        ControlFlowKind.PatternTest => (1, "a subpattern, list element or list length that tests a value adds one path: the match fails when the value does not fit"),
        ControlFlowKind.Loop => (1, "a loop adds one path: it runs its body again or leaves"),
        ControlFlowKind.Catch => (1, "a catch adds one path"),
        ControlFlowKind.Conditional => (1, "a conditional expression adds one path"),
        ControlFlowKind.LogicalAnd => (1, "an and adds one path: it joins a second condition"),
        ControlFlowKind.LogicalOr => (1, "an or adds one path: it joins a second condition"),
        ControlFlowKind.Jump => (0, "a jump adds no path of its own: the branch around it counts"),
        ControlFlowKind.NullCheck => (1, "a null check adds one path: the one taken on null"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "a control flow kind cyclomatic has no rule for"),
    };

    private static string Question(int value, int limit) => string.Create(
        CultureInfo.InvariantCulture,
        $"cyclomatic complexity {value} is above the limit of {limit}. Why does this method need {value} paths?");
}
