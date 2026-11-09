using System.Globalization;
using System.Text.RegularExpressions;
using Navarone.Engine.Configuration;
using Navarone.Engine.Facts;
using Navarone.Engine.Findings;
using Navarone.Engine.Metrics;
using Navarone.Metrics.Cyclomatic;

namespace Navarone.Metrics.SplitComplexity;

/// <summary>
/// The trick of splitting a method, measured on today's code with no git history: a method whose helpers, methods of its
/// own type or a type nested in it called only from it or from each other, are each at or under the cyclomatic limit,
/// while the method with its helpers inlined is over this check's limit. That value is the method's decisions plus each
/// helper's decisions, plus one, so a split scores what the method would score unsplit. A method that implements an
/// interface member or overrides a base member declared outside the method's own type and the types nested in it is no
/// helper: it is called through that contract. Reads the cyclomatic results of the method and its helpers through
/// <see cref="CyclomaticMetric.ValueOf"/>.
/// </summary>
public sealed partial class SplitComplexityMetric : ISignal
{
    /// <inheritdoc />
    public string Id => "split-complexity";

    /// <inheritdoc />
    public SubjectKind Subject => SubjectKind.Method;

    /// <summary>Reads this check's own limit and the cyclomatic limit from the given limits.</summary>
    public IReadOnlyList<Finding> Measure(CodebaseFacts facts, CheckLimits limits)
    {
        var limit = limits.For(Id);
        var cyclomaticLimit = limits.For("cyclomatic");
        var methods = facts.Types.SelectMany(type => type.Methods).DistinctBy(method => method.Symbol).ToDictionary(method => method.Symbol);
        var typeOf = facts.Types.SelectMany(type => type.Methods.Select(method => (method.Symbol, Type: type.Symbol))).DistinctBy(entry => entry.Symbol).ToDictionary(entry => entry.Symbol, entry => entry.Type);
        var outer = facts.Types.DistinctBy(type => type.Symbol).ToDictionary(type => type.Symbol, type => type.ContainingType);
        var values = methods.ToDictionary(entry => entry.Key, entry => CyclomaticMetric.ValueOf(entry.Value));
        var callers = new Dictionary<SymbolId, HashSet<SymbolId>>();
        foreach (var method in methods.Values)
        {
            foreach (var callee in method.Calls.Where(methods.ContainsKey))
            {
                callers.TryAdd(callee, []);
                callers[callee].Add(method.Symbol);
            }
        }

        var splits = methods.Values
            .Where(method => values[method.Symbol] <= cyclomaticLimit)
            .Select(method => (Method: method, Helpers: HelpersOf(method, methods, values, callers, id => Within(typeOf[id], typeOf[method.Symbol], outer) && methods[id].Contracts.All(contract => Within(contract, typeOf[method.Symbol], outer)), cyclomaticLimit)))
            .Where(split => split.Helpers.Count > 0)
            .ToList();

        // A helper that is itself the root of a split belongs to the larger split that holds it. Methods that hold each
        // other, a cycle nothing else calls, are one split: its root is the ordinal-lowest symbol.
        return
        [
            .. from split in splits
               where !splits.Any(other => other.Helpers.Contains(split.Method.Symbol)
                   && (!split.Helpers.Contains(other.Method.Symbol) || string.CompareOrdinal(other.Method.Symbol.Value, split.Method.Symbol.Value) < 0))
               let helpers = split.Helpers.Select(helper => new Helper(helper, Word().Match(methods[helper].Name).Value, values[helper] - 1)).ToList()
               let inlined = values[split.Method.Symbol] + helpers.Sum(helper => helper.Decisions)
               where inlined > limit
               select new Finding(Id, split.Method.Symbol, Subject, split.Method.Location, inlined, limit, Question(split.Method, helpers, methods, values, inlined, limit), helpers),
        ];
    }

    // The largest set of candidates, methods of the root's own type or a type nested in it that fulfil no contract
    // declared outside them, each reachable from the root and at or under the cyclomatic limit, whose callers all lie
    // in the set or are the root. Candidates start as everything reachable and shed any with an outside caller until
    // none changes, so helpers that only call each other, recursion included, stay in.
    private static List<SymbolId> HelpersOf(
        MethodFacts root,
        Dictionary<SymbolId, MethodFacts> methods,
        Dictionary<SymbolId, int> values,
        Dictionary<SymbolId, HashSet<SymbolId>> callers,
        Func<SymbolId, bool> candidate,
        int cyclomaticLimit)
    {
        var helpers = Reachable(root, methods, id => candidate(id) && values[id] <= cyclomaticLimit);
        while (true)
        {
            var kept = helpers.Where(id => callers[id].All(caller => caller == id || caller == root.Symbol || helpers.Contains(caller))).ToHashSet();
            var reachable = Reachable(root, methods, kept.Contains);
            if (reachable.Count == helpers.Count)
            {
                return [.. reachable.OrderBy(id => id.Value, StringComparer.Ordinal)];
            }

            helpers = reachable;
        }
    }

    // Whether the given type is the root's type or is declared, at any depth, inside it.
    private static bool Within(SymbolId type, SymbolId rootType, Dictionary<SymbolId, SymbolId?> outer)
    {
        for (SymbolId? current = type; current is { } here; current = outer.GetValueOrDefault(here))
        {
            if (here == rootType)
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<SymbolId> Reachable(MethodFacts root, Dictionary<SymbolId, MethodFacts> methods, Func<SymbolId, bool> allowed)
    {
        var seen = new HashSet<SymbolId>();
        var pending = new Stack<MethodFacts>([root]);
        while (pending.Count > 0)
        {
            foreach (var callee in pending.Pop().Calls.Where(callee => callee != root.Symbol && methods.ContainsKey(callee) && allowed(callee)))
            {
                if (seen.Add(callee))
                {
                    pending.Push(methods[callee]);
                }
            }
        }

        return seen;
    }

    // The question, with the ledger of the helpers that add decisions: the ones a reason must name.
    private static string Question(MethodFacts method, List<Helper> helpers, Dictionary<SymbolId, MethodFacts> methods, Dictionary<SymbolId, int> values, int inlined, int limit) => string.Create(
        CultureInfo.InvariantCulture,
        $"split-complexity {inlined} is above the limit of {limit}: {method.Name} ({values[method.Symbol]}) is split into helpers only it uses. With its helpers inlined it would have cyclomatic {inlined}. Each piece passes cyclomatic while the whole does not. Why was it split instead of simplified? A reason names each helper that adds decisions by its method name: {string.Join(", ", helpers.Where(helper => helper.Decisions > 0).Select(helper => $"{methods[helper.Symbol].Name} adds {helper.Decisions}").DefaultIfEmpty("none"))}.");

    [GeneratedRegex(@"\w+")]
    private static partial Regex Word();
}
