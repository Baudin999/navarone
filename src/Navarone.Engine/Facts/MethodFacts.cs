namespace Navarone.Engine.Facts;

/// <summary>
/// One method, constructor or accessor with a body, or an initializer that holds logic, under its member's symbol. A
/// method declared without a body, such as an abstract or partial one, is one too, with no control flow and no uses,
/// and so is a primary constructor, whose field uses are the fields its parameters set.
/// Name is the short name a question shows, parameter types included, such as `Parse(string)`.
/// IsStatic is true for a method that has no instance.
/// IsAccessor is true for a property's or indexer's getter or setter: the provider decides, so a classifier never sees
/// the language.
/// IsPrivate is true for a method only its own type may call, so all its callers are in the type: the provider decides.
/// IsPublic is true for a method declared public, part of its type's API: the provider decides.
/// Contracts are the types that declare the members the method overrides or implements, an interface's or a base
/// type's: a method that fulfils a contract is called through it, so a caller the provider resolves is not all its
/// callers.
/// Dispatch says whether the method's body is one flat dispatch, a switch, a chain of else ifs or a chain of
/// conditional expressions whose every branch is a single return or a single expression without control flow of its
/// own, and whether it is a mapping, whose every branch only returns a value: the provider decides, so a classifier
/// never sees the language.
/// CallSites are its calls in the order they are written, each with the resolved symbol it calls and its line; a
/// callee called twice on one line is one site. Calls and FieldUses name the resolved symbol, so a metric never guesses
/// what a name refers to.
/// Signature is the member's declaration without its body, as the language writes it, such as
/// `public static int Parse(string text)`; an accessor's is its property's.
/// Code is the method's declaration as written, name and signature included. The engine computes the
/// fingerprint a reason carries from it, so any change to the method, a rename included, voids its reason.
/// Written is its code as it stands in its file, for an accessor from its property's signature on.
/// NavaroneIds are the ids written in `// navarone: &lt;id&gt;` comments at the method, as written.
/// Tokens are its body's tokens in the order written, each with the shape the provider normalised it to, so a
/// duplication search compares shapes and never sees the language.
/// </summary>
public sealed record MethodFacts(
    SymbolId Symbol,
    string Name,
    SourceLocation Location,
    bool IsConstructor,
    bool IsStatic,
    bool IsAccessor,
    bool IsPrivate,
    bool IsPublic,
    IReadOnlyList<SymbolId> Contracts,
    IReadOnlyList<ParameterFacts> Parameters,
    IReadOnlyList<ControlFlowFact> ControlFlow,
    Dispatch Dispatch,
    IReadOnlyList<CallSite> CallSites,
    IReadOnlyList<SymbolId> FieldUses,
    string Signature,
    string Code,
    Written Written,
    IReadOnlyList<string> NavaroneIds,
    IReadOnlyList<CodeToken> Tokens)
{
    /// <summary>The method's calls in the order they are written. Setting them builds Calls, so the two always agree.</summary>
    public IReadOnlyList<CallSite> CallSites
    {
        get;
        init
        {
            field = value;
            Calls = CallsOf(value);
        }
    } = CallSites;

    /// <summary>Every symbol the method calls, once each, in the order of its first call. Built when CallSites is set.</summary>
    public IReadOnlyList<SymbolId> Calls { get; private init; } = CallsOf(CallSites);

    private static IReadOnlyList<SymbolId> CallsOf(IReadOnlyList<CallSite> sites) => [.. sites.Select(site => site.Callee).Distinct()];
}
