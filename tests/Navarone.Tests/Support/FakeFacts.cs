using Navarone.Engine.Facts;

namespace Navarone.Tests.Support;

/// <summary>Hand built facts for engine tests, which a FakeProvider hands the engine per path.</summary>
internal static class FakeFacts
{
    // Each method of a fake codebase starts this many lines below the one before, the first at line 1.
    private const int LinesApart = 10;

    /// <summary>These facts with the given ids written in a `// navarone:` comment at the method with the given symbol.</summary>
    public static CodebaseFacts WithIds(CodebaseFacts facts, string symbol, params string[] ids) => facts with
    {
        Types =
        [
            .. facts.Types.Select(type => type with
            {
                Methods = [.. type.Methods.Select(method => method.Symbol.Value == symbol ? method with { NavaroneIds = ids } : method)],
            }),
        ],
    };

    /// <summary>A codebase of one type, T, with one method per given symbol, each with the given number of branches. The methods use no field and call nothing, so lcom4 leaves them alone.</summary>
    public static CodebaseFacts With(params (string Symbol, int Branches, string Code)[] methods) => With("T", methods);

    /// <summary>A codebase of one type with one method per given symbol, each with the given number of branches.</summary>
    public static CodebaseFacts With(string type, params (string Symbol, int Branches, string Code)[] methods)
    {
        var here = new SourceLocation("T.cs", 1, 1);
        return new CodebaseFacts(
        [
            new TypeFacts(new SymbolId(type), type, null, string.Empty, "Fake", here, [], [
                .. methods.Select((method, at) => new MethodFacts(
                    new SymbolId(method.Symbol),
                    method.Symbol,
                    new SourceLocation("T.cs", LineOf(at), 1),
                    false,
                    true,
                    false,
                    false,
                    false,
                    [],
                    [],
                    [.. Enumerable.Repeat(new ControlFlowFact(ControlFlowKind.Branch, 0, new SourceSpan(LineOf(at), 1, LineOf(at), 2, "if"), false, "if", null), method.Branches)],
                    Dispatch.None,
                    [],
                    [],
                    method.Symbol,
                    method.Code,
                    new Written(LineOf(at), method.Code),
                    [],
                    [])),
            ], false, false, $"class {type}", new Written(1, $"class {type}"), []),
        ],
        Complete: true);
    }

    // The line the method at the given place in a fake codebase starts at.
    private static int LineOf(int place) => 1 + (place * LinesApart);
}
