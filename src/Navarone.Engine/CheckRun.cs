using Navarone.Engine.Clashes;
using Navarone.Engine.Events;
using Navarone.Engine.Reasons;

namespace Navarone.Engine;

/// <summary>
/// One `navarone` run: the verdicts its coverage shows, but for the problems a clash holds, where it ran, what it
/// made happen, the entry of each problem it showed and each problem a clash holds red, whatever the coverage.
/// Analysis.LogRun writes the events once the CLI knows what printing them cost.
/// </summary>
public sealed record CheckRun(
    IReadOnlyList<Verdict> Verdicts,
    EventPlace Place,
    IReadOnlyList<INavaroneEvent> Happened,
    IReadOnlyList<ShownProblem> Shown,
    IReadOnlyList<Hold> Holds);
