using Navarone.Engine.Classification;
using Navarone.Engine.Configuration;
using Navarone.Engine.Clashes;
using Navarone.Engine.Events;
using Navarone.Engine.Metrics;
using Navarone.Engine.Providers;

namespace Navarone.Engine;

/// <summary>
/// What the engine runs with: one provider, the checks, their limits, the classifiers of problems and reasons, the
/// root of the analysed repo, which holds reasons.json, the event log the engine writes what it makes happen to, the
/// user's judgements of clashes in brain's window, which `navarone` collects, and the path of the global NAVARONE.md,
/// whose rules a validator's replace names unless the repo has a NAVARONE.md of its own.
/// </summary>
public sealed record EngineSetup(
    ILanguageProvider Provider,
    IReadOnlyList<ISignal> Checks,
    CheckLimits Limits,
    IProblemClassifier ProblemClassifier,
    IReasonClassifier ReasonClassifier,
    string RepoRoot,
    NavaroneEventHandler Events,
    BrainAnswers Answers,
    string Taste);
