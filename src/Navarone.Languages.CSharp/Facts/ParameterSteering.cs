using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Navarone.Languages.CSharp.Facts;

/// <summary>
/// Decides which parameters of a method steer a branch: a branch condition reads them as a flag. The conditions are
/// those of an if, a conditional expression, a loop, a switch statement, a switch expression and the when clause of a case or an arm. A parameter steers when
/// its type is bool or an enum and a condition reads it directly, or when a condition compares it to a non-null
/// constant or tests it against a constant or relational pattern. A guard, an if with no else whose body only throws or
/// returns, counts a bool or enum parameter and an equality with a constant, but not a range check or an empty-input check against zero or the empty string; a comparison with null never steers. A parameter handed to
/// another method inside a condition, or tested through a property, positional or list pattern, does not steer. A local
/// declared from a parameter alone and never written again reads as that parameter. Lambdas and local functions belong
/// to the method they are written in.
/// </summary>
internal static class ParameterSteering
{
    /// <summary>The given parameters that steer a branch in the given bodies.</summary>
    public static HashSet<IParameterSymbol> Of(IReadOnlyList<SyntaxNode> bodies, IReadOnlyList<IParameterSymbol> parameters, SemanticModel model)
    {
        var steering = new HashSet<IParameterSymbol>(SymbolEqualityComparer.Default);
        foreach (var node in bodies.SelectMany(body => body.DescendantNodesAndSelf()))
        {
            foreach (var (condition, guard, switched) in ConditionsOf(node, model))
            {
                foreach (var name in condition.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
                {
                    if (ParameterOf(model.GetSymbolInfo(name).Symbol, bodies, model) is { } parameter
                        && parameters.Contains(parameter, SymbolEqualityComparer.Default)
                        && Steers(name, condition, parameter, (guard, switched), model))
                    {
                        steering.Add(parameter);
                    }
                }
            }
        }

        return steering;
    }

    // The parameter a name reads: the parameter itself, or a local declared from it alone and never written again, so
    // `var factor = bold;` reads bold. Only such simple aliases are followed, a chain of them included.
    private static IParameterSymbol? ParameterOf(ISymbol? symbol, IReadOnlyList<SyntaxNode> bodies, SemanticModel model) => symbol switch
    {
        IParameterSymbol { IsImplicitlyDeclared: false } parameter => parameter,
        ILocalSymbol local when local.DeclaringSyntaxReferences is [var reference]
            && reference.GetSyntax() is VariableDeclaratorSyntax { Initializer.Value: var value }
            && !IsWrittenAgain(local, bodies, model)
            => ParameterOf(model.GetSymbolInfo(Unparenthesized(value)).Symbol, bodies, model),
        _ => null,
    };

    private static ExpressionSyntax Unparenthesized(ExpressionSyntax expression) =>
        expression is ParenthesizedExpressionSyntax parenthesized ? Unparenthesized(parenthesized.Expression) : expression;

    // A local is written by an assignment, a deconstruction, an increment or decrement, or a ref or out argument.
    private static bool IsWrittenAgain(ILocalSymbol local, IReadOnlyList<SyntaxNode> bodies, SemanticModel model) =>
        bodies.SelectMany(body => body.DescendantNodes()).OfType<IdentifierNameSyntax>()
            .Any(name => name.Identifier.ValueText == local.Name && IsWrite(name) && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name).Symbol, local));

    private static bool IsWrite(ExpressionSyntax name)
    {
        SyntaxNode target = name;
        while (target.Parent is ParenthesizedExpressionSyntax || target.Parent is ArgumentSyntax { Parent: TupleExpressionSyntax })
        {
            target = target.Parent is ArgumentSyntax { Parent: TupleExpressionSyntax tuple } ? tuple : target.Parent;
        }

        return target.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == target,
            PrefixUnaryExpressionSyntax unary => unary.Kind() is SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression,
            PostfixUnaryExpressionSyntax unary => unary.Kind() is SyntaxKind.PostIncrementExpression or SyntaxKind.PostDecrementExpression,
            ArgumentSyntax argument => argument.RefKindKeyword.Kind() is SyntaxKind.RefKeyword or SyntaxKind.OutKeyword,
            _ => false,
        };
    }

    // The branch conditions a node holds. Guard marks an if with no else whose body only exits: only an equality with a constant counts there.
    // Switched marks the governing expression of a switch whose labels are constants, which any parameter read alone steers.
    private static IEnumerable<(ExpressionSyntax Condition, bool Guard, bool Switched)> ConditionsOf(SyntaxNode node, SemanticModel model)
    {
        switch (node)
        {
            case IfStatementSyntax statement:
                yield return (statement.Condition, statement.Else is null && Exits(statement.Statement), false);
                break;
            case WhileStatementSyntax statement:
                yield return (statement.Condition, false, false);
                break;
            case DoStatementSyntax statement:
                yield return (statement.Condition, false, false);
                break;
            case ForStatementSyntax { Condition: { } condition }:
                yield return (condition, false, false);
                break;
            case WhenClauseSyntax clause:
                yield return (clause.Condition, false, false);
                break;
            case ConditionalExpressionSyntax expression:
                yield return (expression.Condition, false, false);
                break;
            case SwitchStatementSyntax statement when statement.Sections.SelectMany(section => section.Labels).Any(label => IsLiteralLabel(label, model)):
                yield return (statement.Expression, false, true);
                break;
            case SwitchExpressionSyntax expression when expression.Arms.Any(arm => IsLiteralPattern(arm.Pattern, model)):
                yield return (expression.GoverningExpression, false, true);
                break;
        }
    }

    // A switch on a bool or an enum steers whatever its labels are, so its labels are not asked.
    private static bool IsLiteralLabel(SwitchLabelSyntax label, SemanticModel model) => label switch
    {
        CaseSwitchLabelSyntax constant => HasValue(constant.Value, model),
        CasePatternSwitchLabelSyntax pattern => IsLiteralPattern(pattern.Pattern, model),
        _ => false,
    };

    private static bool Exits(StatementSyntax statement) => statement switch
    {
        BlockSyntax { Statements: [var only] } => Exits(only),
        ThrowStatementSyntax or ReturnStatementSyntax => true,
        _ => false,
    };

    // The name is read as a flag when the condition is the name itself, or the name climbs to the condition through
    // parentheses, not, and, or, a comparison and a pattern test, and never through an argument or a member access.
    private static bool Steers(IdentifierNameSyntax name, ExpressionSyntax condition, IParameterSymbol parameter, (bool Guard, bool Switched) mode, SemanticModel model)
    {
        var isFlag = IsFlagType(parameter.Type);
        SyntaxNode node = name;
        while (node != condition)
        {
            var parent = node.Parent;
            switch (parent)
            {
                case ParenthesizedExpressionSyntax or PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression }:
                case BinaryExpressionSyntax binary when binary.Kind() is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression:
                    break;
                case BinaryExpressionSyntax comparison when IsComparison(comparison):
                    var other = comparison.Left == node ? comparison.Right : comparison.Left;
                    if (IsNull(other, model))
                    {
                        return false;
                    }

                    if (HasValue(other, model))
                    {
                        return isFlag || !mode.Guard || comparison.Kind() is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression && !IsEmpty(other, model);
                    }

                    break;
                case IsPatternExpressionSyntax test when test.Expression == node:
                    if (IsLiteralPattern(test.Pattern, model))
                    {
                        return isFlag || !mode.Guard || !HasRelational(test.Pattern) && !Tests(test.Pattern).OfType<ConstantPatternSyntax>().All(constant => IsEmpty(constant.Expression, model));
                    }

                    if (Tests(test.Pattern).Any(pattern => pattern is ConstantPatternSyntax constant && IsNull(constant.Expression, model)))
                    {
                        return false;
                    }

                    break;
                default:
                    return false;
            }

            node = parent;
        }

        return isFlag || mode.Switched;
    }

    private static bool IsComparison(BinaryExpressionSyntax binary) => binary.Kind() is
        SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression or SyntaxKind.LessThanExpression
        or SyntaxKind.LessThanOrEqualExpression or SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression;

    private static bool IsFlagType(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? IsFlagType(nullable.TypeArguments[0])
            : type.SpecialType == SpecialType.System_Boolean || type.TypeKind == TypeKind.Enum;

    // A pattern tests a flag when it holds a relational pattern or a constant pattern that is not null, on the value itself.
    private static bool IsLiteralPattern(PatternSyntax pattern, SemanticModel model) =>
        Tests(pattern).Any(node => node is RelationalPatternSyntax || node is ConstantPatternSyntax constant && HasValue(constant.Expression, model));

    // The patterns that test the value itself, through not, and, or and parentheses. A property, positional or list
    // pattern tests a member or an element of the value, which reads it through a member.
    private static IEnumerable<PatternSyntax> Tests(PatternSyntax pattern) => pattern switch
    {
        BinaryPatternSyntax binary => [.. Tests(binary.Left), .. Tests(binary.Right)],
        UnaryPatternSyntax unary => Tests(unary.Pattern),
        ParenthesizedPatternSyntax parenthesized => Tests(parenthesized.Pattern),
        _ => [pattern],
    };

    // The constant an empty-input check compares with: zero or the empty string.
    private static bool IsEmpty(ExpressionSyntax expression, SemanticModel model) =>
        model.GetConstantValue(expression) is { HasValue: true, Value: "" or 0 or 0L or 0u or 0ul or (short)0 or (ushort)0 or (byte)0 or (sbyte)0 or 0.0 or 0f or 0m };

    private static bool HasRelational(PatternSyntax pattern) => Tests(pattern).Any(node => node is RelationalPatternSyntax);

    private static bool IsNull(ExpressionSyntax expression, SemanticModel model) =>
        model.GetConstantValue(expression) is { HasValue: true, Value: null };

    private static bool HasValue(ExpressionSyntax expression, SemanticModel model) =>
        model.GetConstantValue(expression) is { HasValue: true, Value: not null };
}
