using System.Globalization;
using Navarone.Engine.Configuration;
using Navarone.Engine.Facts;
using Navarone.Engine.Findings;
using Navarone.Engine.Metrics;

namespace Navarone.Metrics.ControlCoupling;

/// <summary>A parameter that decides what the method does.</summary>
public sealed class ControlCouplingMetric : ISignal
{
    /// <inheritdoc />
    public string Id => "control-coupling";

    /// <inheritdoc />
    public SubjectKind Subject => SubjectKind.Method;

    /// <inheritdoc />
    public IReadOnlyList<Finding> Measure(CodebaseFacts facts, CheckLimits limits)
    {
        var limit = limits.For(Id);
        return
        [
            .. from method in facts.Types.SelectMany(type => type.Methods)
               let steering = method.Parameters.Where(parameter => parameter.Steers).Select(parameter => parameter.Name).ToList()
               where steering.Count > limit
               select new Finding(Id, method.Symbol, Subject, method.Location, steering.Count, limit, Question(steering, limit)),
        ];
    }

    private static string Question(List<string> steering, int limit) => string.Create(
        CultureInfo.InvariantCulture,
        $"control coupling {steering.Count} is above the limit of {limit}: {string.Join(", ", steering)} steer{(steering.Count == 1 ? "s" : "")} which branch runs. Why does the caller have to choose the behaviour with a parameter?");
}
