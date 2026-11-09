using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Navarone.Engine.Facts;

namespace Navarone.Languages.CSharp.Facts;

/// <summary>
/// Decides whether a method body is a flat dispatch: one switch statement or switch expression, a chain of else ifs, or
/// a chain of conditional expressions whose every else is the next condition, whose every branch is a single return,
/// throw or expression with no control flow of its own. A test, the governing expression, a condition, a case label or
/// a pattern, is one test: an &amp;&amp;, an ||, a property or positional pattern, a list pattern that tests, a lambda,
/// a loop, an if, a ?:, a switch or a catch in a test makes the method not flat, and so does a when clause. A pattern
/// may join constants or types with and and or, which names the cases of one branch. A null check, ?., ?? or ??=, is
/// allowed in a test and in a branch. A chain may be a run of ifs when every branch of each returns or throws, and one
/// return or throw may follow the dispatch. A flat dispatch is a mapping when it returns a value, no branch is an
/// expression or a return without a value, every throw throws a new object, and nothing in it, a throw included, calls
/// a method, assigns, increments, decrements or awaits. A nameof is no call.
/// </summary>
internal static class FlatDispatch
{
    /// <summary>Whether the given body, a block or an expression body, is a flat dispatch, and whether it is a mapping.</summary>
    // navarone: 0000030
    public static Dispatch Of(SyntaxNode body, SemanticModel model)
    {
        if (!Is(body))
        {
            return Dispatch.None;
        }

        return IsMapping(body, model) ? Dispatch.Mapping : Dispatch.Flat;
    }

    private static bool Is(SyntaxNode body) => body switch
    {
        ArrowExpressionClauseSyntax arrow => IsExpressionDispatch(arrow.Expression),
        BlockSyntax block => IsDispatch([.. block.Statements]),
        _ => false,
    };

    // A throw of a new object refuses an input the mapping has no value for; a throw of anything else may act.
    private static bool IsMapping(SyntaxNode body, SemanticModel model) =>
        body.DescendantNodesAndSelf().Any(node => node is ArrowExpressionClauseSyntax or ReturnStatementSyntax { Expression: not null })
        && !body.DescendantNodesAndSelf().Any(node => IsAction(node, model));

    private static bool IsAction(SyntaxNode node, SemanticModel model) => node switch
    {
        ThrowStatementSyntax statement => statement.Expression is not BaseObjectCreationExpressionSyntax,
        ThrowExpressionSyntax expression => expression.Expression is not BaseObjectCreationExpressionSyntax,
        InvocationExpressionSyntax call => model.GetOperation(call) is not INameOfOperation,
        _ => IsWrite(node),
    };

    private static bool IsWrite(SyntaxNode node) => node is ExpressionStatementSyntax
        or ReturnStatementSyntax { Expression: null }
        or AssignmentExpressionSyntax
        or AwaitExpressionSyntax
        or PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression }
        or PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PostIncrementExpression or (int)SyntaxKind.PostDecrementExpression };

    private static bool IsDispatch(List<StatementSyntax> statements)
    {
        var dispatch = statements.Count > 1 && IsLeaf(statements[^1], exits: true) ? statements[..^1] : statements;
        return dispatch switch
        {
            [SwitchStatementSyntax statement] => IsTest(statement.Expression) && statement.Sections.All(IsFlatSection),
            [ReturnStatementSyntax { Expression: { } expression }] => IsExpressionDispatch(expression),
            [IfStatementSyntax chain] => IsChain(chain, exits: false),
            [_, _, ..] => dispatch.All(statement => statement is IfStatementSyntax chain && IsChain(chain, exits: true)),
            _ => false,
        };
    }

    private static bool IsExpressionDispatch(ExpressionSyntax expression) => expression switch
    {
        SwitchExpressionSyntax dispatch => IsTest(dispatch.GoverningExpression)
            && dispatch.Arms.All(arm => IsTest(arm.Pattern) && arm.WhenClause is null && !Branches(arm.Expression)),
        ConditionalExpressionSyntax chain => IsConditionalChain(chain),
        _ => false,
    };

    // `a ? x : b ? y : z`: each condition is a test, each value a leaf, and only the last else is not a condition.
    private static bool IsConditionalChain(ConditionalExpressionSyntax chain) =>
        IsTest(chain.Condition) && !Branches(chain.WhenTrue) && (chain.WhenFalse is ConditionalExpressionSyntax next ? IsConditionalChain(next) : !Branches(chain.WhenFalse));

    // A test is one test: it holds no lambda and no control flow, && and || included, and no property or positional
    // pattern, which tests more than one value, nor a list pattern that tests, whose tests the provider reads as pattern
    // tests. A pattern may join constants or types with and and or, such as `'b' or 'c'`, which only names the cases of
    // one branch.
    private static bool IsTest(SyntaxNode test) =>
        !test.DescendantNodesAndSelf().Any(node => node is AnonymousFunctionExpressionSyntax or RecursivePatternSyntax
            || node.Kind() is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression
            || (node is BinaryPatternSyntax pattern && !IsCases(pattern)))
        && ControlFlowReader.KindsIn(test).All(kind => kind is ControlFlowKind.LogicalAnd or ControlFlowKind.LogicalOr or ControlFlowKind.NullCheck);

    private static bool IsCases(PatternSyntax pattern) => pattern switch
    {
        ConstantPatternSyntax or TypePatternSyntax => true,
        ParenthesizedPatternSyntax inner => IsCases(inner.Pattern),
        BinaryPatternSyntax binary => IsCases(binary.Left) && IsCases(binary.Right),
        _ => false,
    };

    // A null check, `?.`, `??` or `??=`, is no branch of its own here: a leaf or a test may hold one.
    private static bool Branches(SyntaxNode node) => ControlFlowReader.KindsIn(node).Any(kind => kind is not ControlFlowKind.NullCheck);

    // Each branch of an if and its else ifs is a leaf, and so is a final plain else.
    private static bool IsChain(IfStatementSyntax chain, bool exits) =>
        IsTest(chain.Condition) && IsLeaf(chain.Statement, exits) && chain.Else switch
        {
            null => true,
            { Statement: IfStatementSyntax next } => IsChain(next, exits),
            { Statement: var last } => IsLeaf(last, exits),
        };

    // A section is one return or throw, one expression followed by a break, or a break alone, braces aside.
    private static bool IsFlatSection(SwitchSectionSyntax section)
    {
        if (!section.Labels.All(IsFlatLabel))
        {
            return false;
        }

        List<StatementSyntax> statements = section.Statements is [BlockSyntax block] ? [.. block.Statements] : [.. section.Statements];
        return statements switch
        {
            [BreakStatementSyntax] => true,
            [var only] => IsLeaf(only, exits: true),
            [ExpressionStatementSyntax expression, BreakStatementSyntax] => !Branches(expression),
            _ => false,
        };
    }

    private static bool IsFlatLabel(SwitchLabelSyntax label) => label switch
    {
        CaseSwitchLabelSyntax value => IsTest(value.Value),
        CasePatternSwitchLabelSyntax pattern => IsTest(pattern.Pattern) && pattern.WhenClause is null,
        _ => true,
    };

    // One return or throw, or one expression when the branch need not exit, braces aside, with no control flow in it.
    private static bool IsLeaf(StatementSyntax statement, bool exits) => statement switch
    {
        BlockSyntax { Statements: [var only] } => IsLeaf(only, exits),
        ReturnStatementSyntax or ThrowStatementSyntax => !Branches(statement),
        ExpressionStatementSyntax => !exits && !Branches(statement),
        _ => false,
    };
}
