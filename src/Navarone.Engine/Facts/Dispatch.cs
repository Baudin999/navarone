namespace Navarone.Engine.Facts;

/// <summary>Whether a method's body is one flat dispatch, and whether that dispatch only maps its input to a value.</summary>
public enum Dispatch
{
    /// <summary>The body is no flat dispatch.</summary>
    None,

    /// <summary>
    /// The body is one switch, chain of else ifs or chain of conditional expressions whose every branch is a single
    /// return, throw or expression without control flow of its own.
    /// </summary>
    Flat,

    /// <summary>
    /// A flat dispatch whose every branch returns a value or throws, and that calls, assigns, increments or awaits
    /// nothing outside a throw: a function of its input, not a choice of behaviour.
    /// </summary>
    Mapping,
}
