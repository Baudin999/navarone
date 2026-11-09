using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Navarone.Engine.Facts;

namespace Navarone.Languages.CSharp.Facts;

/// <summary>
/// Reads the initializers a type declares that hold logic: a lambda or anything that breaks the straight line. Each
/// is measured as a method under its member's symbol and named by its member's name, so a body moved into a field
/// initializer is measured all the same. An initializer has no parameters, no contracts, and is neither a constructor
/// nor an accessor.
/// </summary>
internal static class InitializerReader
{
    /// <summary>The initializers that hold logic in the given member: a property's, or each variable's of a field or field-like event.</summary>
    // navarone: 0000079
    public static List<MethodFacts> InitializersOf(MemberDeclarationSyntax member, SemanticModel model, string path, CancellationToken cancellationToken)
    {
        var initializers = new List<MethodFacts>();
        if (member is PropertyDeclarationSyntax { Initializer.Value: { } initial } property)
        {
            if (!HoldsLogic(initial))
            {
                return initializers;
            }

            if (model.GetDeclaredSymbol(property, cancellationToken) is not { } initializedProperty)
            {
                return initializers;
            }

            initializers.Add(Of(initializedProperty, property, initial, model, path));
            return initializers;
        }

        if (member is not BaseFieldDeclarationSyntax field)
        {
            return initializers;
        }

        foreach (var variable in field.Declaration.Variables)
        {
            if (variable.Initializer?.Value is not { } value)
            {
                continue;
            }

            if (!HoldsLogic(value))
            {
                continue;
            }

            if (model.GetDeclaredSymbol(variable, cancellationToken) is not { } initialized)
            {
                continue;
            }

            initializers.Add(Of(initialized, field, value, model, path));
        }

        return initializers;
    }

    private static bool HoldsLogic(ExpressionSyntax value)
    {
        if (ControlFlowReader.Breaks(value))
        {
            return true;
        }

        return value.DescendantNodesAndSelf().Any(node => node is AnonymousFunctionExpressionSyntax);
    }

    // The initializer as a method of the member's symbol, written and identified by the member's whole declaration.
    private static MethodFacts Of(ISymbol member, MemberDeclarationSyntax declaration, ExpressionSyntax value, SemanticModel model, string path)
    {
        var body = BodyFacts.Of([value], model);
        var location = SourcePlaces.LocationOf(member, declaration, path);
        var isPrivate = member.DeclaredAccessibility == Accessibility.Private;
        var isPublic = member.DeclaredAccessibility == Accessibility.Public;
        var code = NavaroneComments.CodeWithout(declaration);
        var written = SourcePlaces.WrittenOf(declaration, declaration);
        var ids = NavaroneComments.IdsAt(declaration);
        return new MethodFacts(
            Symbols.Id(member),
            member.Name,
            location,
            IsConstructor: false,
            member.IsStatic,
            IsAccessor: false,
            isPrivate,
            isPublic,
            Contracts: [],
            Parameters: [],
            body.ControlFlow,
            body.Dispatch,
            body.CallSites,
            body.FieldUses,
            Symbols.Signature(member),
            code,
            written,
            ids,
            body.Tokens);
    }
}
