using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Navarone.Engine.Configuration;
using Navarone.Engine.Facts;

namespace Navarone.Languages.CSharp.Facts;

/// <summary>
/// Gathers one type's facts across its declarations, since a partial class is declared in more than one file.
/// A declaration's code leaves out the `// navarone: &lt;id&gt;` comments written in it.
/// A synthesized type, such as the Program of top-level statements, has methods but no declaration.
/// </summary>
internal sealed class TypeBuilder(SymbolId symbol, string name, SymbolId? containingType, string space, string assembly, IReadOnlyList<FieldFacts> fields, bool isInterface, bool isTest)
{
    private readonly List<(SourceLocation Location, string Code, Written Written, IReadOnlyList<string> Ids)> declarations = [];

    /// <summary>
    /// A builder for the given type, with its fields, its namespace, its assembly and whether it is a test class. A field declared in a file the
    /// given ignores match is left out.
    /// </summary>
    // navarone: 0000084
    public static TypeBuilder Of(INamedTypeSymbol type, string? directory, Ignores ignores)
    {
        SymbolId? outer = null;
        if (type.ContainingType is { } containing)
        {
            outer = Symbols.Id(containing);
        }

        var space = string.Empty;
        if (!type.ContainingNamespace.IsGlobalNamespace)
        {
            space = type.ContainingNamespace.ToDisplayString();
        }

        var fields = FieldsOf(type, directory, ignores);
        var isInterface = type.TypeKind == TypeKind.Interface;
        var isTest = TestClass.Is(type);
        return new TypeBuilder(Symbols.Id(type), type.Name, outer, space, type.ContainingAssembly.Name, fields, isInterface, isTest);
    }

    /// <summary>The methods gathered so far.</summary>
    public List<MethodFacts> Methods { get; } = [];

    /// <summary>The methods declared without a body. One stands in only while no declaration of it with a body was read.</summary>
    public List<MethodFacts> Bodiless { get; } = [];

    /// <summary>Adds one declaration of the type, with its code as written in its file.</summary>
    public void AddDeclaration(SourceLocation location, IReadOnlyList<string> ids, string code, Written written) =>
        declarations.Add((location, code, written, ids));

    /// <summary>The type's facts. Its code is its declarations in file order; its location is the first one's.</summary>
    public TypeFacts Build()
    {
        var ordered = declarations.OrderBy(declaration => declaration.Location.Path, StringComparer.Ordinal)
            .ThenBy(declaration => declaration.Location.Line)
            .ToList();
        // A synthesized type has no declaration, so it is where its first method is.
        SourceLocation location;
        Written written;
        if (ordered.Count > 0)
        {
            location = ordered[0].Location;
            written = ordered[0].Written;
        }
        else
        {
            location = Methods[0].Location;
            written = Methods[0].Written;
        }

        List<MethodFacts> methods = [.. Methods.DistinctBy(method => method.Symbol)];
        var standIns = Bodiless.Where(bodiless => methods.TrueForAll(method => method.Symbol != bodiless.Symbol)).DistinctBy(method => method.Symbol);
        methods.AddRange(standIns);
        var read = methods.Where(method => !IsInitializer(method)).SelectMany(method => method.FieldUses).ToHashSet();
        List<MethodFacts> measured = [.. methods.Select(method => WithItsMember(method, read))];
        var code = string.Join('\n', ordered.Select(declaration => declaration.Code));
        List<string> ids = [.. ordered.SelectMany(declaration => declaration.Ids)];
        return new TypeFacts(symbol, name, containingType, space, assembly, location, fields, measured, isInterface, isTest, code, written, ids);
    }

    // An initializer carries its member's symbol. It writes that member, so it uses it as a field when another method
    // reads the member: a table the initializer builds and a method dispatches through is one job. A member nothing
    // else reads leaves the initializer to its own calls, so it never makes a piece of its own.
    private MethodFacts WithItsMember(MethodFacts method, HashSet<SymbolId> read)
    {
        if (!IsInitializer(method))
        {
            return method;
        }

        if (!read.Contains(method.Symbol))
        {
            return method;
        }

        return method with { FieldUses = [.. method.FieldUses, method.Symbol] };
    }

    private bool IsInitializer(MethodFacts method) => fields.Any(field => field.Symbol == method.Symbol);

    // A backing field stands for its auto-property or field-like event, so a use of either is a use of that field. A
    // parameter of a class's or struct's primary constructor is state its members capture, so it is a field too. A field
    // the compiler declares, which no code is written for, is left out: no code can name it.
    private static List<FieldFacts> FieldsOf(INamedTypeSymbol type, string? directory, Ignores ignores)
    {
        var symbols = new List<ISymbol>();
        foreach (var field in type.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.IsConst)
            {
                continue;
            }

            symbols.Add(field.AssociatedSymbol ?? field);
        }

        foreach (var constructor in type.InstanceConstructors)
        {
            if (SymbolUses.CapturesItsParameters(constructor))
            {
                symbols.AddRange(constructor.Parameters);
            }
        }

        var fields = new List<FieldFacts>();
        foreach (var symbol in symbols)
        {
            var field = FieldOf(symbol, directory, ignores);
            if (field is not null)
            {
                fields.Add(field);
            }
        }

        return fields;
    }

    // A field is declared by its whole field declaration, an auto-property by its property and a captured or positional
    // parameter by the parameter. A field declared in an ignored file, or with no declaration, is no field of the facts.
    private static FieldFacts? FieldOf(ISymbol symbol, string? directory, Ignores ignores)
    {
        if (symbol.DeclaringSyntaxReferences is not [var reference, ..])
        {
            return null;
        }

        if (ignores.IgnoresFile(reference.SyntaxTree.FilePath))
        {
            return null;
        }

        var syntax = reference.GetSyntax();
        var declaration = syntax as ParameterSyntax ?? syntax.FirstAncestorOrSelf<MemberDeclarationSyntax>() ?? syntax;
        var file = syntax.SyntaxTree.FilePath;
        var path = SourcePlaces.PathOf(directory, file, Path.GetFileName(file));
        return new FieldFacts(
            Symbols.Id(symbol),
            symbol.Name,
            symbol.IsStatic,
            symbol.DeclaredAccessibility == Accessibility.Public,
            symbol is IPropertySymbol,
            Symbols.Signature(symbol),
            SourcePlaces.LocationOf(symbol, declaration, path),
            SourcePlaces.WrittenOf(declaration, declaration));
    }
}
