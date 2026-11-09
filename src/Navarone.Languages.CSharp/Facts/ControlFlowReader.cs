using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Navarone.Engine.Facts;

namespace Navarone.Languages.CSharp.Facts;

/// <summary>
/// Maps the C# constructs that break the straight line of a body onto the engine's control flow kinds. Lambdas and
/// local functions belong to the method they are written in, so their control flow counts there.
/// </summary>
internal static class ControlFlowReader
{
    /// <summary>
    /// The control flow facts of the given body. Each names its construct as C# writes it, spans its head in the file,
    /// and is part of the construct around it, unless a statement lies between them or it is a statement itself. A node
    /// that is two constructs, such as a list element that is an or, gives two facts, the second a part of the first.
    /// </summary>
    // navarone: 0000028
    public static IEnumerable<ControlFlowFact> Read(SyntaxNode body)
    {
        var facts = new Dictionary<SyntaxNode, ControlFlowFact>();
        foreach (var node in body.DescendantNodesAndSelf())
        {
            var constructs = ConstructsOf(node).ToList();
            if (constructs.Count == 0)
            {
                continue;
            }

            var depth = NestingDepth(node, body);
            var continues = Continues(node);
            ControlFlowFact? around = null;
            if (PartOf(node, body) is { } whole)
            {
                around = facts[whole];
            }

            foreach (var construct in constructs)
            {
                var head = SpanOf(node.SyntaxTree, construct.Head);
                var fact = new ControlFlowFact(construct.Kind, depth, head, continues, construct.Word, around);
                facts[node] = fact;
                around = fact;
                yield return fact;
            }
        }
    }

    /// <summary>Whether the node holds any construct that breaks the straight line, the node itself included.</summary>
    public static bool Breaks(SyntaxNode node) => KindsIn(node).Any();

    /// <summary>The kinds of every construct in the node that breaks the straight line, the node itself included.</summary>
    public static IEnumerable<ControlFlowKind> KindsIn(SyntaxNode node) =>
        node.DescendantNodesAndSelf().SelectMany(ConstructsOf).Select(construct => construct.Kind);

    // The constructs a node is, most often none or one. A list element that tests a value is a decision of its own
    // before what its pattern is, so `'a' or 'b'` as an element is the element and, as a part of it, the or.
    private static IEnumerable<Construct> ConstructsOf(SyntaxNode node)
    {
        if (ElementOf(node) is { } element)
        {
            yield return element;
        }

        if (ConstructOf(node) is { } construct)
        {
            yield return construct;
        }
    }

    // An element of a list pattern that tests a value, as a subpattern does, whatever it holds; what it holds counts
    // on its own. The element of a slice is the slice's pattern, so the slice itself is none.
    private static Construct? ElementOf(SyntaxNode node)
    {
        if (node is not PatternSyntax { Parent: ListPatternSyntax or SlicePatternSyntax } pattern)
        {
            return null;
        }

        if (pattern is SlicePatternSyntax)
        {
            return null;
        }

        if (TestsNothing(pattern))
        {
            return null;
        }

        return new(ControlFlowKind.PatternTest, "element", node.Span);
    }

    // A discard or a var pattern matches any value: it binds at most and tests nothing.
    private static bool TestsNothing(PatternSyntax pattern) => pattern is DiscardPatternSyntax or VarPatternSyntax;

    // What the node is as control flow: its kind, its word as C# writes it, and its head, the part that decides. A head
    // starts where its construct starts, so the line of a fact is the line its construct begins on: an else if's head
    // starts at its if, and a do while's head is its do, whose condition comes after its body. A case or arm whose
    // pattern is a discard or a var pattern tests nothing, so it is a default; its when is a guard of its own. A list
    // pattern tests its length, a constant or a range, unless a lone slice such as `[..]` takes any length. A not is
    // no construct: like a `!`, it flips a test that is already counted.
    // navarone: 0000003
    // navarone: 0000053
    private static Construct? ConstructOf(SyntaxNode node) => node switch
    {
        IfStatementSyntax { Parent: ElseClauseSyntax } statement => new(ControlFlowKind.Branch, "else if", From(statement.IfKeyword, statement.CloseParenToken)),
        IfStatementSyntax statement => new(ControlFlowKind.Branch, "if", From(statement.IfKeyword, statement.CloseParenToken)),
        ElseClauseSyntax { Statement: not IfStatementSyntax } clause => new(ControlFlowKind.Else, "else", clause.ElseKeyword.Span),
        SwitchStatementSyntax statement => new(ControlFlowKind.Switch, "switch", HeadOf(statement)),
        SwitchExpressionSyntax expression => new(ControlFlowKind.Switch, "switch", TextSpan.FromBounds(expression.SpanStart, expression.SwitchKeyword.Span.End)),
        CasePatternSwitchLabelSyntax label when TestsNothing(label.Pattern) => new(ControlFlowKind.Default, "case", node.Span),
        CaseSwitchLabelSyntax or CasePatternSwitchLabelSyntax => new(ControlFlowKind.SwitchCase, "case", node.Span),
        DefaultSwitchLabelSyntax => new(ControlFlowKind.Default, "default", node.Span),
        SwitchExpressionArmSyntax { Pattern: DiscardPatternSyntax } arm => new(ControlFlowKind.Default, "_", HeadOf(arm)),
        SwitchExpressionArmSyntax { Pattern: VarPatternSyntax } arm => new(ControlFlowKind.Default, "var", HeadOf(arm)),
        SwitchExpressionArmSyntax arm => new(ControlFlowKind.SwitchCase, "arm", HeadOf(arm)),
        WhenClauseSyntax or CatchFilterClauseSyntax => new(ControlFlowKind.Guard, "when", node.Span),
        SubpatternSyntax subpattern when !TestsNothing(subpattern.Pattern) => new(ControlFlowKind.PatternTest, "subpattern", node.Span),
        ListPatternSyntax { Patterns: not [SlicePatternSyntax] } => new(ControlFlowKind.PatternTest, "list", node.Span),
        ForStatementSyntax statement => new(ControlFlowKind.Loop, "for", From(statement.ForKeyword, statement.CloseParenToken)),
        CommonForEachStatementSyntax statement => new(ControlFlowKind.Loop, "foreach", From(statement.ForEachKeyword, statement.CloseParenToken)),
        WhileStatementSyntax statement => new(ControlFlowKind.Loop, "while", From(statement.WhileKeyword, statement.CloseParenToken)),
        DoStatementSyntax statement => new(ControlFlowKind.Loop, "do while", statement.DoKeyword.Span),
        CatchClauseSyntax clause => new(ControlFlowKind.Catch, "catch", HeadOf(clause)),
        ConditionalExpressionSyntax => new(ControlFlowKind.Conditional, "?:", node.Span),
        BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalAndExpression) => new(ControlFlowKind.LogicalAnd, "&&", node.Span),
        BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalOrExpression) => new(ControlFlowKind.LogicalOr, "||", node.Span),
        BinaryPatternSyntax pattern when pattern.IsKind(SyntaxKind.AndPattern) => new(ControlFlowKind.LogicalAnd, "and", node.Span),
        BinaryPatternSyntax pattern when pattern.IsKind(SyntaxKind.OrPattern) => new(ControlFlowKind.LogicalOr, "or", node.Span),
        GotoStatementSyntax => new(ControlFlowKind.Jump, "goto", node.Span),
        ConditionalAccessExpressionSyntax => new(ControlFlowKind.NullCheck, "?.", node.Span),
        BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.CoalesceExpression) => new(ControlFlowKind.NullCheck, "??", node.Span),
        AssignmentExpressionSyntax assignment when assignment.IsKind(SyntaxKind.CoalesceAssignmentExpression) => new(ControlFlowKind.NullCheck, "??=", node.Span),
        _ => null,
    };

    private static TextSpan From(SyntaxToken first, SyntaxToken last) => TextSpan.FromBounds(first.SpanStart, last.Span.End);

    // A switch statement's head runs from its keyword to its governing expression. A switch on a tuple, `switch (a, b)`,
    // writes no parentheses of its own, so its close paren is missing and has no width; the head ends at whichever of
    // the expression and the close paren ends last.
    private static TextSpan HeadOf(SwitchStatementSyntax statement)
    {
        var end = Math.Max(statement.Expression.Span.End, statement.CloseParenToken.Span.End);
        return TextSpan.FromBounds(statement.SwitchKeyword.SpanStart, end);
    }

    // A catch's head is its keyword and the exception it declares; a bare catch declares none.
    private static TextSpan HeadOf(CatchClauseSyntax clause)
    {
        if (clause.Declaration is null)
        {
            return clause.CatchKeyword.Span;
        }

        return TextSpan.FromBounds(clause.CatchKeyword.SpanStart, clause.Declaration.Span.End);
    }

    // An arm's head is its pattern and its when, without the value it gives.
    private static TextSpan HeadOf(SwitchExpressionArmSyntax arm)
    {
        if (arm.WhenClause is null)
        {
            return arm.Pattern.Span;
        }

        return TextSpan.FromBounds(arm.SpanStart, arm.WhenClause.Span.End);
    }

    // The construct a node is a part of: the nearest construct around it, up to the body. A statement is a part of
    // none, and a statement between the two ends the search, so an if's body is not a part of the if.
    private static SyntaxNode? PartOf(SyntaxNode node, SyntaxNode body)
    {
        if (node is StatementSyntax)
        {
            return null;
        }

        for (var child = node; child != body && child.Parent is { } parent; child = parent)
        {
            if (ConstructsOf(parent).Any())
            {
                return parent;
            }

            if (parent is StatementSyntax)
            {
                return null;
            }
        }

        return null;
    }

    // An else if continues the flow of its if. A && or || continues the run of the same operator it sits in, brackets aside.
    private static bool Continues(SyntaxNode node)
    {
        if (node is IfStatementSyntax)
        {
            return node.Parent is ElseClauseSyntax;
        }

        if (node is not (BinaryExpressionSyntax or BinaryPatternSyntax))
        {
            return false;
        }

        var parent = node.Parent;
        while (parent is ParenthesizedExpressionSyntax or ParenthesizedPatternSyntax)
        {
            parent = parent.Parent;
        }

        return parent is not null && parent.IsKind(node.Kind());
    }

    // The levels of nesting around a node, counted up to the body. An else if sits at the level of its if, and a
    // condition sits at the level of the statement it steers.
    private static int NestingDepth(SyntaxNode node, SyntaxNode body)
    {
        var depth = 0;
        for (var child = node; child != body && child.Parent is { } parent; child = parent)
        {
            if (Nests(parent, child))
            {
                depth++;
            }
        }

        return depth;
    }

    private static bool Nests(SyntaxNode parent, SyntaxNode child) => parent switch
    {
        IfStatementSyntax statement => child == statement.Statement,
        ElseClauseSyntax clause => clause.Statement is not IfStatementSyntax,
        SwitchStatementSyntax statement => child != statement.Expression,
        SwitchExpressionSyntax expression => child != expression.GoverningExpression,
        ForStatementSyntax statement => child == statement.Statement,
        CommonForEachStatementSyntax statement => child == statement.Statement,
        WhileStatementSyntax statement => child == statement.Statement,
        DoStatementSyntax statement => child == statement.Statement,
        CatchClauseSyntax => true,
        ConditionalExpressionSyntax expression => child != expression.Condition,
        AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax => true,
        _ => false,
    };

    // The span's lines and columns in its file, from 1, and its text as written.
    private static SourceSpan SpanOf(SyntaxTree tree, TextSpan span)
    {
        var lines = tree.GetLineSpan(span);
        return new SourceSpan(lines.StartLinePosition.Line + 1, lines.StartLinePosition.Character + 1, lines.EndLinePosition.Line + 1, lines.EndLinePosition.Character, tree.GetText().ToString(span));
    }

    private sealed record Construct(ControlFlowKind Kind, string Word, TextSpan Head);
}
