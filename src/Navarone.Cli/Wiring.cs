using Navarone.Classifiers.Deterministic;
using Navarone.Engine;
using Navarone.Engine.Configuration;
using Navarone.Engine.Clashes;
using Navarone.Engine.Events;
using Navarone.Engine.Metrics;
using Navarone.Languages.CSharp;
using Navarone.Metrics.Cognitive;
using Navarone.Metrics.ControlCoupling;
using Navarone.Metrics.Cyclomatic;
using Navarone.Metrics.Lcom4;
using Navarone.Metrics.SplitComplexity;

namespace Navarone.Cli;

/// <summary>Wires the C# provider with what navarone.json ignores, the checks, their limits, the deterministic classifiers, the event log, brain's answers and the NAVARONE.md Claude Code loads into the engine.</summary>
internal static class Wiring
{
    /// <summary>The engine setup for the repo at the given root, which holds navarone.json and reasons.json, writing to the given event log.</summary>
    public static EngineSetup For(string repoRoot, NavaroneEventHandler events)
    {
        var config = NavaroneConfig.Load(repoRoot);
        ISignal[] checks = [new CyclomaticMetric(), new CognitiveMetric(), new Lcom4Metric(), new SplitComplexityMetric(), new ControlCouplingMetric()];
        return new EngineSetup(
            new CSharpProvider(config.Ignores),
            checks,
            config.Limits,
            new DeterministicProblemClassifier(),
            new DeterministicReasonClassifier(),
            repoRoot,
            events,
            BrainAnswers.Default(),
            TasteRules.DefaultPath());
    }
}
