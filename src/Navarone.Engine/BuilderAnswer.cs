using Navarone.Engine.Events;
using Navarone.Engine.Findings;
using Navarone.Engine.Reasons;

namespace Navarone.Engine;

/// <summary>
/// The builder's answer to a problem, which the validator keeps or replaces: its position, and the event a validator's
/// refusal of it logs.
/// </summary>
internal abstract record BuilderAnswer(Position Builder)
{
    /// <summary>
    /// The builder's answer to a problem that has a finding now: a reason, when its resolution turns it green. A false
    /// positive and a problem that is red have no answer to accept or refuse: each throws an InvalidDataException.
    /// </summary>
    public static ReasonAnswer Now(CheckedProblem now, ProblemId id)
    {
        if (now.FalsePositive)
        {
            throw new InvalidDataException($"{id.Value} is a false positive, green without a reason: there is nothing to accept or refuse");
        }

        if (now.Verdict.State == FindingState.Red)
        {
            throw new InvalidDataException($"{id.Value} is still red with no reason: there is nothing to accept or refuse");
        }

        var recorded = now.Verdict.Problem;
        return new ReasonAnswer(recorded.Check, recorded.Symbol.Value, now.Text);
    }

    /// <summary>The event that refuses this answer for the given rule and finding, with any code it carries stored.</summary>
    public abstract INavaroneEvent Refuse(string id, TasteRule rule, string finding, NavaroneEventHandler events);
}

/// <summary>A fix, as its fixed event logged it. Refusing it carries the code before and after the fix.</summary>
internal sealed record FixAnswer(FixedEvent Fix) : BuilderAnswer(Position.Replace)
{
    public override INavaroneEvent Refuse(string id, TasteRule rule, string finding, NavaroneEventHandler events) =>
        new RefusedFixEvent(id, Fix.Check, Fix.Symbol, finding, Fix.Before, Fix.After, rule);
}

/// <summary>
/// A reason for the problem of the given check and symbol, with the subject's text it defends. Refusing it stores that
/// text and carries it as the code before.
/// </summary>
internal sealed record ReasonAnswer(string Check, string Symbol, string Defended) : BuilderAnswer(Position.Keep)
{
    public override INavaroneEvent Refuse(string id, TasteRule rule, string finding, NavaroneEventHandler events)
    {
        var before = events.Store(Defended);
        return new RefusedReasonEvent(id, Check, Symbol, finding, before, rule);
    }
}
