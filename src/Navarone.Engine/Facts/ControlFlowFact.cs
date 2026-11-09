namespace Navarone.Engine.Facts;

/// <summary>
/// One construct that breaks the straight line of a method.
/// NestingDepth is 0 at the top level of the method body.
/// Span is the construct's head in its file, the part that decides: an if's keyword and condition without its body, a
/// case's label, a short circuit operator with both its sides.
/// Continues is true when the construct carries on a flow already begun: an else if after its if, or a short circuit
/// operator in the same unbroken run of the same operator as the one before it.
/// Construct names it as the language writes it, such as `if`, `case` or `&amp;&amp;`: the provider decides, so a
/// metric never sees the language.
/// PartOf is the construct this one is a part of, such as the if whose condition holds this `&amp;&amp;`, or null at
/// the top of a statement.
/// </summary>
public sealed record ControlFlowFact(
    ControlFlowKind Kind,
    int NestingDepth,
    SourceSpan Span,
    bool Continues,
    string Construct,
    ControlFlowFact? PartOf);
