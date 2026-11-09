using System.Text.Json.Serialization;

namespace Navarone.Engine.Events;

/// <summary>
/// One thing Navarone made happen, a line of its event log. Type names it in the log. Id is the problem it is about,
/// or null for an event about no one problem, such as a run. Only NavaroneEventHandler writes an event to the log and
/// reads it back.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RunEvent), "run")]
[JsonDerivedType(typeof(RaisedEvent), "raised")]
[JsonDerivedType(typeof(FixedEvent), "fixed")]
[JsonDerivedType(typeof(ResolvedEvent), "resolved")]
[JsonDerivedType(typeof(RefusedFixEvent), "refused-fix")]
[JsonDerivedType(typeof(RefusedReasonEvent), "refused-reason")]
[JsonDerivedType(typeof(AcceptedEvent), "accepted")]
[JsonDerivedType(typeof(LargeChangeEvent), "large")]
[JsonDerivedType(typeof(SettledEvent), "settled")]
[JsonDerivedType(typeof(CostEvent), "cost")]
public interface INavaroneEvent
{
    /// <summary>The id of the problem the event is about, or null.</summary>
    string? Id { get; }
}
