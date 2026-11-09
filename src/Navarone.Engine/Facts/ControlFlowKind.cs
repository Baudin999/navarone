namespace Navarone.Engine.Facts;

/// <summary>The language agnostic kinds of control flow a provider maps its syntax onto.</summary>
public enum ControlFlowKind
{
    /// <summary>An if, or an else if.</summary>
    Branch,

    /// <summary>A plain else.</summary>
    Else,

    /// <summary>A switch statement or expression.</summary>
    Switch,

    /// <summary>One case or arm of a switch.</summary>
    SwitchCase,

    /// <summary>
    /// The default case of a switch, or a case or arm whose pattern tests nothing, such as a discard. A guard on it is a
    /// fact of its own.
    /// </summary>
    Default,

    /// <summary>
    /// A guard that narrows a case or a catch, such as a when clause. It can fail after the pattern matched.
    /// </summary>
    Guard,

    /// <summary>
    /// A test inside a pattern: a subpattern or a list element that tests a value, such as a property's constant, range
    /// or type, or the length a list pattern tests. A bare binding tests nothing and is none.
    /// </summary>
    PatternTest,

    /// <summary>A for, foreach, while or do loop.</summary>
    Loop,

    /// <summary>A catch clause.</summary>
    Catch,

    /// <summary>A conditional expression.</summary>
    Conditional,

    /// <summary>A short circuit and.</summary>
    LogicalAnd,

    /// <summary>A short circuit or.</summary>
    LogicalOr,

    /// <summary>A jump to a label.</summary>
    Jump,

    /// <summary>A short circuit on null: a null-conditional member access, or a null-coalescing operator or assignment.</summary>
    NullCheck,
}
