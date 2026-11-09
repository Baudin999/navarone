using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Navarone.Engine.Events;

/// <summary>
/// A clash the user has not judged yet: the validator refused the builder's answer to a problem, Refusal, and the
/// builder held its position with Reason, a new reason for the code refused, Defended. At and Place are
/// when and where the reason was logged. CodeNow is the hash of the subject's code as the last run on its worktree
/// showed it, the code defended until a run shows it, or null when the subject is gone.
/// </summary>
public sealed record Clash(Refusal Refusal, string Reason, DefendedCode Defended, DateTimeOffset At, EventPlace Place, string? CodeNow)
{
    /// <summary>Whether the subject's code changed since the builder defended it.</summary>
    public bool Stale => CodeNow != Defended.Code;

    /// <summary>
    /// The id of the question that asks the user to judge this clash with the subject's code at CodeNow, or with no
    /// code for a subject that is gone. brain stores an answer per question id and never deletes one, and Navarone
    /// grants ids per worktree, so the id names the problem and adds a hash of the worktree, the time of the clash and
    /// the code: unique across repos and worktrees, new for every clash, and new once the code changes, so an answer
    /// about older code is never collected.
    /// </summary>
    public string QuestionId()
    {
        var asked = string.Join('\n', Place.Worktree, Refusal.Id, At.UtcTicks.ToString(CultureInfo.InvariantCulture), CodeNow ?? "gone");
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(asked)));
        return $"{Refusal.Id}-{hash[..16]}";
    }
}
