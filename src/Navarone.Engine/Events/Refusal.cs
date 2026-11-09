namespace Navarone.Engine.Events;

/// <summary>
/// The validator's refusal of the builder's answer to a problem, as a clash shows it. Finding says why the code must
/// still change and Rule is the rule of the user's taste it breaks. Code is the hash of the code refused: the fix's
/// code after, or the code the reason defends. Before is the hash of the code before the refused fix, or null for a
/// refused reason.
/// </summary>
public sealed record Refusal(string Id, string Check, string Symbol, string Finding, TasteRule Rule, string? Before, string Code)
{
    /// <summary>The finding's first sentence: what is wrong in the code, in the validator's words.</summary>
    public string WhatIsWrong
    {
        get
        {
            var text = Finding.Trim();
            for (var at = 0; at < text.Length - 1; at++)
            {
                var ends = text[at] is '.' or '?' or '!';
                if (ends && char.IsWhiteSpace(text[at + 1]))
                {
                    return text[..(at + 1)];
                }
            }

            return text;
        }
    }

    /// <summary>
    /// The refusal of a fix, or null when it names no rule or the fix left no subject: no reason can defend code that is
    /// gone.
    /// </summary>
    public static Refusal? Of(RefusedFixEvent refused)
    {
        if (refused.Rule is null || refused.After is null)
        {
            return null;
        }

        return new Refusal(refused.Id, refused.Check, refused.Symbol, refused.Finding, refused.Rule, refused.Before, refused.After);
    }

    /// <summary>The refusal of a reason, or null when it names no rule.</summary>
    public static Refusal? Of(RefusedReasonEvent refused)
    {
        if (refused.Rule is null)
        {
            return null;
        }

        return new Refusal(refused.Id, refused.Check, refused.Symbol, refused.Finding, refused.Rule, null, refused.Before);
    }
}
