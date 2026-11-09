using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Navarone.Engine.Facts;

namespace Navarone.Languages.CSharp.Facts;

/// <summary>
/// Names a Roslyn symbol as a SymbolId: its assembly, its containing type in full, then its own name with its
/// parameter types by name only, such as <c>Compiler::Compiler.Parser.Parse(Token, string)</c>. The assembly keeps two
/// programs' <c>Program</c> apart, and the parameter types keep overloads apart. A parameter type is named without its
/// namespace, so a symbol reads the same whether or not the package that declares the type was restored. A use of a
/// generic or an extension method names its declaration. A member's signature is how the language declares it.
/// </summary>
internal static class Symbols
{
    // A symbol's name, held while its compilation lives. A call site names its callee, so one symbol is named many times.
    private static readonly ConditionalWeakTable<ISymbol, string> Ids = [];

    private static readonly SymbolDisplayFormat MemberFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeExplicitInterface,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeParamsRefOut,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly SymbolDisplayFormat SignatureFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeAccessibility
            | SymbolDisplayMemberOptions.IncludeModifiers
            | SymbolDisplayMemberOptions.IncludeType
            | SymbolDisplayMemberOptions.IncludeParameters
            | SymbolDisplayMemberOptions.IncludeRef
            | SymbolDisplayMemberOptions.IncludeExplicitInterface,
        propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType
            | SymbolDisplayParameterOptions.IncludeName
            | SymbolDisplayParameterOptions.IncludeParamsRefOut
            | SymbolDisplayParameterOptions.IncludeDefaultValue
            | SymbolDisplayParameterOptions.IncludeExtensionThis,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>
    /// The member's declaration without its body, such as <c>public static int Parse(string text)</c>. An accessor
    /// shows its property, such as <c>public int Count { get; }</c>.
    /// </summary>
    public static string Signature(ISymbol symbol) => symbol switch
    {
        IMethodSymbol { AssociatedSymbol: IPropertySymbol property } => property.ToDisplayString(SignatureFormat),
        _ => symbol.ToDisplayString(SignatureFormat),
    };

    /// <summary>The SymbolId of the given symbol, named once per symbol.</summary>
    public static SymbolId Id(ISymbol symbol) => new(Ids.GetValue(symbol, NameOf));

    private static string NameOf(ISymbol symbol) => symbol switch
    {
        IParameterSymbol parameter => $"{Id(parameter.ContainingSymbol).Value}#{parameter.Name}",
        IMethodSymbol { ReducedFrom: { } extension } => Id(extension).Value,
        _ => $"{symbol.OriginalDefinition.ContainingAssembly?.Name}::{Display(symbol.OriginalDefinition)}",
    };

    private static string Display(ISymbol symbol) => symbol switch
    {
        INamedTypeSymbol type => type.ToDisplayString(),
        { ContainingType: { } container } => $"{container.ToDisplayString()}.{symbol.ToDisplayString(MemberFormat)}",
        _ => symbol.ToDisplayString(),
    };
}
