using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Navarone.Engine.Facts;

namespace Navarone.Languages.CSharp.Facts;

/// <summary>
/// Reads the properties and indexers a type declares with a body, so a property shows as one member with its accessors.
/// MethodReader reads each accessor with a body as a method; this ties those methods to their property.
/// </summary>
internal static class PropertyReader
{
    /// <summary>
    /// The property or indexer the given member declares, with each of its accessors among the given methods read from
    /// it, the getter first, or null when the member is no property or indexer or none of its accessors has a body.
    /// </summary>
    public static PropertyFacts? PropertyOf(MemberDeclarationSyntax member, IReadOnlyList<MethodFacts> read, SemanticModel model, string path, CancellationToken cancellationToken)
    {
        if (member is not BasePropertyDeclarationSyntax declaration)
        {
            return null;
        }

        if (model.GetDeclaredSymbol(declaration, cancellationToken) is not IPropertySymbol property)
        {
            return null;
        }

        var bodied = read.Select(method => method.Symbol).ToHashSet();
        var accessors = new List<AccessorFacts>();
        foreach (var accessor in new[] { property.GetMethod, property.SetMethod }.OfType<IMethodSymbol>())
        {
            var id = Symbols.Id(accessor);
            if (!bodied.Contains(id))
            {
                continue;
            }

            // An accessor with a body is written in the code, so it has a declaration: the accessor, or the expression
            // body of an expression-bodied property.
            var syntax = accessor.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken);
            accessors.Add(new AccessorFacts(id, SideOf(accessor), SourcePlaces.WrittenOf(syntax, syntax)));
        }

        if (accessors.Count == 0)
        {
            return null;
        }

        var isPublic = property.DeclaredAccessibility == Accessibility.Public;
        var location = SourcePlaces.LocationOf(property, declaration, path);
        var written = SourcePlaces.WrittenOf(declaration, declaration);
        return new PropertyFacts(Symbols.Id(property), property.Name, isPublic, location, written, accessors);
    }

    // The side of a property's accessor: get, init or set.
    private static string SideOf(IMethodSymbol accessor)
    {
        if (accessor.MethodKind == MethodKind.PropertyGet)
        {
            return "get";
        }

        if (accessor.IsInitOnly)
        {
            return "init";
        }

        return "set";
    }
}
