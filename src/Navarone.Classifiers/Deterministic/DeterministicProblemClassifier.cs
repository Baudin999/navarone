using Navarone.Engine.Classification;
using Navarone.Engine.Facts;

namespace Navarone.Classifiers.Deterministic;

/// <summary>
/// Classifies a problem by rules over the facts. A cyclomatic or cognitive problem on a method the provider calls a
/// flat dispatch is a false positive, and so is a control-coupling problem on a method the provider calls a mapping: a
/// function of its input, not a caller choosing behaviour. An lcom4 problem on a value class is a false positive: every
/// method other than a constructor is a property accessor without control flow and without a call, that reads or writes
/// at most one of the class's own fields, so each piece is one field and its accessors, which is data, not a separate
/// job. No rule calls any other problem a false positive.
/// </summary>
public sealed class DeterministicProblemClassifier : IProblemClassifier
{
    /// <inheritdoc />
    // navarone: 0000039
    public string? FalsePositive(ProblemCase problem)
    {
        var finding = problem.Finding;
        if (finding.Check == "lcom4")
        {
            var type = problem.Facts.Types.FirstOrDefault(type => type.Symbol == finding.Subject);
            if (type is not null && IsValueClass(type))
            {
                return "the class is a value class: every method is a property accessor over its own field, so each piece is data, not a separate job";
            }

            return null;
        }

        var method = problem.Facts.Types.SelectMany(type => type.Methods).FirstOrDefault(method => method.Symbol == finding.Subject);
        if (method is null)
        {
            return null;
        }

        if (finding.Check is "cyclomatic" or "cognitive" && method.Dispatch != Dispatch.None)
        {
            return "the method is a flat dispatch: every branch is a single return or expression";
        }

        if (finding.Check == "control-coupling" && method.Dispatch == Dispatch.Mapping)
        {
            return "the method is a mapping: a flat dispatch whose every branch only returns a value";
        }

        return null;
    }

    // An accessor that only reads or writes one field of its own class holds data. One with control flow, or with any
    // call, in the class or out of it, does work, and a class of two such pieces may do two jobs.
    private static bool IsValueClass(TypeFacts type)
    {
        var fields = type.Fields.Where(field => !field.IsStatic).Select(field => field.Symbol).ToHashSet();
        foreach (var method in type.Methods)
        {
            if (method.IsConstructor)
            {
                continue;
            }

            if (method is not { IsAccessor: true, ControlFlow.Count: 0, Calls.Count: 0 })
            {
                return false;
            }

            if (method.FieldUses.Count > 1)
            {
                return false;
            }

            if (!method.FieldUses.All(fields.Contains))
            {
                return false;
            }
        }

        return true;
    }
}
