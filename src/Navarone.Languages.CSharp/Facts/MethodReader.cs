using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Navarone.Engine.Facts;

namespace Navarone.Languages.CSharp.Facts;

/// <summary>
/// Reads the methods a type declares: a method, constructor, destructor, operator or accessor with a body, an
/// expression-bodied property or indexer, a method declared without a body, a primary constructor and the top-level
/// statements of a program. An initializer that holds logic is InitializerReader's.
/// </summary>
internal static class MethodReader
{
    /// <summary>The methods with a body the given member declares: itself, or each accessor of a property, indexer or event.</summary>
    public static List<MethodFacts> MethodsOf(MemberDeclarationSyntax member, SemanticModel model, string path, CancellationToken cancellationToken)
    {
        var methods = new List<MethodFacts>();
        if (member is BaseMethodDeclarationSyntax declaration)
        {
            var method = WithBody(declaration, model, path, cancellationToken);
            if (method is not null)
            {
                methods.Add(method);
            }
        }

        if (member is BasePropertyDeclarationSyntax property)
        {
            methods.AddRange(AccessorsOf(property, model, path, cancellationToken));
        }

        return methods;
    }

    /// <summary>
    /// The method the given member declares without a body, such as an abstract method or a partial method a source
    /// generator implements, or null. It is called like any other: it has no control flow and uses nothing.
    /// </summary>
    public static MethodFacts? BodilessOf(MemberDeclarationSyntax member, SemanticModel model, string path, CancellationToken cancellationToken)
    {
        if (member is not MethodDeclarationSyntax { Body: null, ExpressionBody: null } declaration)
        {
            return null;
        }

        if (model.GetDeclaredSymbol(declaration, cancellationToken) is not { } method)
        {
            return null;
        }

        var code = NavaroneComments.CodeWithout(declaration);
        var written = SourcePlaces.WrittenOf(declaration, declaration);
        return Of(method, declaration, [], code, written, model, path);
    }

    /// <summary>
    /// The primary constructor of the given declaration, or null. It sets each of its parameters: a record's parameter
    /// sets the property of its name, and a class's or struct's is a field of its own. Those are its field uses, so a
    /// factory that calls it uses them too.
    /// </summary>
    public static MethodFacts? PrimaryConstructorOf(INamedTypeSymbol type, TypeDeclarationSyntax declaration, SemanticModel model, string path)
    {
        if (declaration.ParameterList is not { } parameters)
        {
            return null;
        }

        var primary = PrimaryOf(type, declaration);
        if (primary is null)
        {
            return null;
        }

        var set = new List<SymbolId>();
        foreach (var parameter in primary.Parameters)
        {
            set.Add(SetBy(type, parameter));
        }

        var code = NavaroneComments.CodeWithout(parameters);
        var written = SourcePlaces.WrittenOf(parameters, parameters);
        var constructor = Of(primary, parameters, [], code, written, model, path);
        return constructor with { FieldUses = set };
    }

    /// <summary>The program's top-level statements, as the method the compiler declares for them.</summary>
    public static MethodFacts MainOf(IMethodSymbol main, IReadOnlyList<GlobalStatementSyntax> globals, SemanticModel model, string path)
    {
        var code = string.Join('\n', globals);
        var written = SourcePlaces.WrittenOf(globals[0], globals[^1]);
        return Of(main, globals[0], globals, code, written, model, path);
    }

    // A method with a body, a constructor's initializer read before its body, or null for one without a body.
    private static MethodFacts? WithBody(BaseMethodDeclarationSyntax declaration, SemanticModel model, string path, CancellationToken cancellationToken)
    {
        SyntaxNode? body = declaration.Body;
        body ??= declaration.ExpressionBody;
        if (body is null)
        {
            return null;
        }

        if (model.GetDeclaredSymbol(declaration, cancellationToken) is not IMethodSymbol method)
        {
            return null;
        }

        var bodies = new List<SyntaxNode>();
        if (declaration is ConstructorDeclarationSyntax { Initializer: { } initializer })
        {
            bodies.Add(initializer);
        }

        bodies.Add(body);
        var code = NavaroneComments.CodeWithout(declaration);
        var written = SourcePlaces.WrittenOf(declaration, declaration);
        return Of(method, declaration, bodies, code, written, model, path);
    }

    // An expression-bodied property's or indexer's getter, or each accessor with a body. An accessor's code starts with
    // its property's signature, so a change to the property's type voids it too.
    // navarone: 0000080
    private static List<MethodFacts> AccessorsOf(BasePropertyDeclarationSyntax property, SemanticModel model, string path, CancellationToken cancellationToken)
    {
        var accessors = new List<MethodFacts>();
        var expressionBody = property switch
        {
            PropertyDeclarationSyntax declaration => declaration.ExpressionBody,
            IndexerDeclarationSyntax declaration => declaration.ExpressionBody,
            _ => null,
        };
        if (expressionBody is not null)
        {
            if (model.GetDeclaredSymbol(property, cancellationToken) is IPropertySymbol { GetMethod: { } getter })
            {
                var code = NavaroneComments.CodeWithout(property);
                var written = SourcePlaces.WrittenOf(property, property);
                accessors.Add(Of(getter, property, [expressionBody], code, written, model, path));
            }

            return accessors;
        }

        var signature = NavaroneComments.CodeWithout(property.WithAccessorList(null));
        foreach (var accessor in property.AccessorList?.Accessors ?? [])
        {
            SyntaxNode? body = accessor.Body;
            body ??= accessor.ExpressionBody;
            if (body is null)
            {
                continue;
            }

            if (model.GetDeclaredSymbol(accessor, cancellationToken) is not { } symbol)
            {
                continue;
            }

            var code = $"{signature}\n{NavaroneComments.CodeWithout(accessor)}";
            var written = SourcePlaces.WrittenOf(property, accessor);
            accessors.Add(Of(symbol, accessor, [body], code, written, model, path));
        }

        return accessors;
    }

    // The constructor the type declares by its parameter list, or null.
    private static IMethodSymbol? PrimaryOf(INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        foreach (var constructor in type.InstanceConstructors)
        {
            foreach (var reference in constructor.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax() == declaration)
                {
                    return constructor;
                }
            }
        }

        return null;
    }

    // What a primary constructor's parameter sets: a record's property or field of its name, or else the parameter,
    // which is a field of its own.
    private static SymbolId SetBy(INamedTypeSymbol type, IParameterSymbol parameter)
    {
        if (!type.IsRecord)
        {
            return Symbols.Id(parameter);
        }

        var member = type.GetMembers(parameter.Name).FirstOrDefault(member => member is IPropertySymbol or IFieldSymbol);
        if (member is null)
        {
            return Symbols.Id(parameter);
        }

        return Symbols.Id(member);
    }

    // navarone: 0000083
    private static MethodFacts Of(
        IMethodSymbol method,
        SyntaxNode declaration,
        IReadOnlyList<SyntaxNode> bodies,
        string code,
        Written written,
        SemanticModel model,
        string path)
    {
        var isConstructor = method.MethodKind is MethodKind.Constructor or MethodKind.StaticConstructor;
        var isAccessor = method.MethodKind is MethodKind.PropertyGet or MethodKind.PropertySet;
        var isPrivate = method.DeclaredAccessibility == Accessibility.Private;
        var contracts = ContractsOf(method);
        var steering = ParameterSteering.Of(bodies, method.Parameters, model);
        var parameters = new List<ParameterFacts>();
        foreach (var parameter in method.Parameters)
        {
            parameters.Add(new ParameterFacts(Symbols.Id(parameter), parameter.Name, steering.Contains(parameter)));
        }

        var body = BodyFacts.Of(bodies, model);
        var location = SourcePlaces.LocationOf(method, declaration, path);
        var ids = IdsOf(declaration);
        return new MethodFacts(
            Symbols.Id(method),
            NameOf(method),
            location,
            isConstructor,
            method.IsStatic,
            isAccessor,
            isPrivate,
            contracts,
            parameters,
            body.ControlFlow,
            body.Dispatch,
            body.CallSites,
            body.FieldUses,
            Symbols.Signature(method),
            code,
            written,
            ids,
            body.Tokens);
    }

    // The ids written at the declaration. An accessor's ids start with its property's.
    private static List<string> IdsOf(SyntaxNode declaration)
    {
        var ids = NavaroneComments.IdsAt(declaration);
        if (declaration is not AccessorDeclarationSyntax { Parent.Parent: { } property })
        {
            return ids;
        }

        return [.. NavaroneComments.IdsAt(property), .. ids];
    }

    // The types that declare what the method overrides, up the chain, and the interfaces whose members it implements,
    // explicitly or not, an accessor's included.
    private static List<SymbolId> ContractsOf(IMethodSymbol method)
    {
        var contracts = new List<INamedTypeSymbol>();
        for (var overridden = method.OverriddenMethod; overridden is not null; overridden = overridden.OverriddenMethod)
        {
            contracts.Add(overridden.ContainingType);
        }

        var type = method.ContainingType;
        foreach (var contract in type.AllInterfaces)
        {
            var implementations = contract.GetMembers().Select(type.FindImplementationForInterfaceMember);
            if (implementations.Contains(method, SymbolEqualityComparer.Default))
            {
                contracts.Add(contract);
            }
        }

        return [.. contracts.Select(Symbols.Id).Distinct()];
    }

    // The short name a question shows: `Parse(string)`, and `Total.get` for a property's accessor.
    private static string NameOf(IMethodSymbol method)
    {
        if (method.AssociatedSymbol is IPropertySymbol property)
        {
            var side = "set";
            if (SymbolEqualityComparer.Default.Equals(property.GetMethod, method))
            {
                side = "get";
            }

            return $"{property.Name}.{side}";
        }

        var types = method.Parameters.Select(parameter => parameter.Type.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat));
        var parameters = string.Join(", ", types);
        return $"{method.Name}({parameters})";
    }
}
