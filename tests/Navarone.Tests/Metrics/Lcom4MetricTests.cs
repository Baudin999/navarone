using Navarone.Engine.Configuration;
using Navarone.Engine.Facts;
using Navarone.Engine.Reasons;
using Navarone.Metrics.Lcom4;
using Navarone.Tests.Support;

namespace Navarone.Tests.Metrics;

public sealed class Lcom4MetricTests
{
    [Fact]
    public async Task ACohesiveClassIsOnePiece()
    {
        Assert.Equal(1, await ValueAsync("""
            class C
            {
                int a, b;
                void X() { a++; b++; }
                void Y() { b--; }
            }
            """));
    }

    [Fact]
    public async Task AClassOfTwoJobsIsTwoPiecesAndTheQuestionNamesThem()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                int a, b;
                void Fill() { a++; }
                int ReadA() => a;
                void Drain(int n) { b = n; }
            }
            """);

        var finding = Assert.Single(new Lcom4Metric().Measure(facts, CheckLimits.Defaults));
        Assert.Equal(2, finding.Value);
        Assert.Equal("Snippet::C", finding.Subject.Value);
        Assert.Contains("piece 1 holds Fill(), ReadA() over a", finding.Question, StringComparison.Ordinal);
        Assert.Contains("piece 2 holds Drain(int) over b", finding.Question, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePiecesOfAClassAreThoseItsQuestionNamesBySymbol()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                int a, b;
                void Fill() { a++; }
                int ReadA() => a;
                void Drain(int n) { b = n; }
                void Quiet() { }
            }
            """);

        var pieces = new Lcom4Metric().Pieces(facts.Types.Single(type => type.Name == "C"));

        Assert.NotNull(pieces);
        Assert.Equal(
            [("Snippet::C.Fill() Snippet::C.ReadA()", "Snippet::C.a"), ("Snippet::C.Drain(int)", "Snippet::C.b")],
            pieces.Select(piece => (string.Join(' ', piece.Methods.Select(method => method.Value)), string.Join(' ', piece.Fields.Select(field => field.Value)))));
    }

    [Fact]
    public async Task AnInterfaceHasNoPieces()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            interface I { int M() => 1; }
            """);

        Assert.Null(new Lcom4Metric().Pieces(Assert.Single(facts.Types)));
    }

    [Fact]
    public async Task ACallJoinsTwoPieces()
    {
        Assert.Equal(1, await ValueAsync("""
            class C
            {
                int a, b;
                void X() { a++; Y(); }
                void Y() { b++; }
            }
            """));
    }

    [Fact]
    public async Task AnInitializerJoinsTheMethodsItCalls()
    {
        Assert.Equal(1, await ValueAsync("""
            using System;
            using System.Collections.Generic;
            static class Verbs
            {
                static readonly Dictionary<string, Func<int, int>> Commands = new()
                {
                    ["run"] = n => n > 0 ? Run(n) : 0,
                    ["parse"] = n => Parse(n),
                };
                static int Run(int n) => Log(n);
                static int Log(int n) => n;
                static int Parse(int n) => Check(n);
                static int Check(int n) => n;
            }
            """));
    }

    [Fact]
    public async Task AnInitializerJoinsTheMethodsThatReadItsMember()
    {
        Assert.Equal(1, await ValueAsync("""
            using System;
            using System.Collections.Generic;
            class Router
            {
                readonly Dictionary<string, Func<int, int>> _routes = new() { ["a"] = n => n > 0 ? A(n) : 0, ["b"] = n => n < 0 ? B(n) : 1 };
                int _count;
                public int Run(string k, int n) { _count++; return _routes[k](n) + _count; }
                static int A(int n) => n + 1;
                static int B(int n) => n - 1;
            }
            """));
    }

    [Fact]
    public async Task AnInitializerNoMethodReadsIsNoPieceOfItsOwn()
    {
        Assert.Equal(1, await ValueAsync("class C(int[]? list) { int a; int[] Items { get; } = list ?? []; int X() => a; int Y() => a + 1; }"));
    }

    [Fact]
    public async Task APrimaryConstructorParameterIsStateTheMethodsShare()
    {
        Assert.Equal(1, await ValueAsync("class C(int seed) { int a, b; int X() => a + seed; int Y() => b + seed; }"));
    }

    [Fact]
    public async Task AnUnusedFieldIsNoPieceAndTwoMethodsOnTheirOwnFieldsAreTwo()
    {
        Assert.Equal(2, await ValueAsync("class C { int used, unused, other; void X() { used++; } void Y() { other++; } }"));
        Assert.Equal(1, await ValueAsync("class C { int used, unused; void X() { used++; } }"));
    }

    [Fact]
    public async Task AnAutoPropertyIsAFieldAndAPropertyWithABodyIsMethods()
    {
        Assert.Equal(2, await ValueAsync("class C { int A { get; set; } int B { get; set; } int X() => A; int Y() => B; }"));
        Assert.Equal(1, await ValueAsync("class C { int A { get; set; } int X() => A; int Y() => A; }"));
        Assert.Equal(1, await ValueAsync("class C { int f; int P { get { return f; } set { f = value; } } void X() { P = 1; } int Y() => P; }"));
        Assert.Equal(2, await ValueAsync("class C { int f, g; int P { get { return f; } set { g = value; } } int X() => P; void Y() { P = 1; } }"));
    }

    [Fact]
    public async Task AConstructorDoesNotJoinThePieces()
    {
        Assert.Equal(2, await ValueAsync("class C { int a, b; C() { a = 1; b = 2; } void X() { a++; } void Y() { b++; } }"));
    }

    [Fact]
    public async Task AMethodThatUsesNoFieldAndCallsNothingIsLeftOut()
    {
        Assert.Equal(1, await ValueAsync("class C { int a; int P => 1; void X() { a++; } public override int GetHashCode() => base.GetHashCode(); int Y() => 1; }"));
        Assert.Equal(1, await ValueAsync("class C { int a; void X() { a++; } int F(int n) => n < 1 ? 0 : F(n - 1); }"));
        Assert.Equal(1, await ValueAsync("class C { int a; void X() { a++; F(2); } int F(int n) => n < 1 ? 0 : F(n - 1); }"));
        Assert.Equal(1, await ValueAsync("class C { int a; C() { a = H(); } void X() { a++; } static int H() => 1; }"));
        Assert.Equal(0, await ValueAsync("class C { int Y() => 1; int Z() => 2; }"));
        Assert.Equal(1, await ValueAsync("class C { int a, b; void X() { a++; H(); } void Y() { b++; H(); } void H() { } }"));
        Assert.Equal(2, await ValueAsync("class C { int a, b; void X() { a++; } void Y() { b++; } void Z() { } }"));
    }

    [Fact]
    public async Task StaticMethodsCountAndAStaticFieldJoinsNothing()
    {
        Assert.Equal(2, await ValueAsync("class C { int a; void X() { a++; } static int S(int n) { return H(n) + 1; } static int H(int n) => n; }"));
        Assert.Equal(1, await ValueAsync("class C { int a; void X() { a++; S(); } static void S() { } }"));
        Assert.Equal(0, await ValueAsync("class C { static int log; static int A() => log; static int B() => log + 1; }"));
        Assert.Equal(2, await ValueAsync("class C { static int log; static int A() { return B() + log; } static int B() => 1; static int C2() { return D(); } static int D() => 1; }"));
        Assert.Equal(2, await ValueAsync("class C { static int log; int a, b; void X() { a++; log++; } void Y() { b++; log++; } }"));
        Assert.Equal(1, await ValueAsync("class C { static int s; static void A() { s++; } static void B() { A(); } }"));
        Assert.Equal(0, await ValueAsync("interface I { int A() => 1; int B() => 2; }"));
        Assert.Equal(0, await ValueAsync("record R(int A, int B);"));
    }

    [Fact]
    public async Task ATestClassIsNotMeasured()
    {
        const string attributes = "class FactAttribute : System.Attribute { } class TheoryAttribute : System.Attribute { } class TestAttribute : System.Attribute { } class TestMethodAttribute : System.Attribute { } class SkippableFactAttribute : FactAttribute { }";
        foreach (var attribute in new[] { "Fact", "Theory", "Test", "TestMethod", "SkippableFact" })
        {
            Assert.Equal(0, await ValueAsync($"{attributes} class C {{ int a, b; [{attribute}] void X() {{ a++; }} void Y() {{ b++; }} }}", "Snippet::C"));
        }

        Assert.Equal(2, await ValueAsync($"{attributes} class C {{ int a, b; [System.Obsolete] void X() {{ a++; }} void Y() {{ b++; }} }}", "Snippet::C"));
    }

    [Fact]
    public async Task ATestClassWhoseTestFrameworkDoesNotResolveIsStillATestClass()
    {
        // An unresolved attribute binds to an error type that carries the name as written.
        Assert.Equal(0, await ValueAsync("class C { int a, b; [Fact] void X() { a++; } void Y() { b++; } }", "Snippet::C"));
    }

    [Fact]
    public async Task APartialClassIsOneClass()
    {
        var facts = await CSharpSnippet.FactsAsync(
            ("A.cs", "partial class C { int a; void X() { a++; } }"),
            ("B.cs", "partial class C { void Y() { X(); } }"));

        Assert.Empty(new Lcom4Metric().Measure(facts, CheckLimits.Defaults));
    }

    [Fact]
    public async Task OnlyAValueAboveTheLimitIsAFinding()
    {
        var facts = await CSharpSnippet.FactsAsync("class C { int a, b; void X() { a++; } void Y() { b++; } }");

        Assert.Empty(new Lcom4Metric().Measure(facts, new CheckLimits(new Dictionary<string, int> { ["lcom4"] = 2 })));
        Assert.Single(new Lcom4Metric().Measure(facts, CheckLimits.Defaults));
    }

    [Fact]
    public async Task ANavaroneCommentAnywhereInTheClassLeavesItsFingerprintAlone()
    {
        const string plain = "class C\n{\n    int a;\n    void X() { a++; }\n}";
        var original = await FingerprintAsync(plain);

        Assert.Equal(original, await FingerprintAsync("// navarone: 0000001\nclass C\n{\n    int a;\n    void X() { a++; }\n}"));
        Assert.Equal(original, await FingerprintAsync("class C\n{\n    // navarone: 0000001\n    int a;\n    void X() { a++; } // navarone: 0000002\n}"));
        Assert.Equal(original, await FingerprintAsync("class C\n{\n    int a;\n    void X() { a++; }\n    // navarone: 0000001\n}"));
        Assert.NotEqual(original, await FingerprintAsync("class C\n{\n    int a;\n    void X() { a += 2; }\n}"));
        Assert.NotEqual(original, await FingerprintAsync("class C\n{\n    int a;\n    // a note\n    void X() { a++; }\n}"));
        Assert.NotEqual(original, await FingerprintAsync("class D\n{\n    int a;\n    void X() { a++; }\n}", "Snippet::D"));
    }

    [Fact]
    public async Task ACommentAfterTheClosingBraceIsAnIdAndLeavesTheFingerprintAlone()
    {
        var facts = await CSharpSnippet.FactsAsync("class C\n{\n    int a;\n    void X() { a++; }\n} // navarone: 0000001\n");

        Assert.Equal(["0000001"], facts.Types.Single().NavaroneIds);
        Assert.Equal(await FingerprintAsync("class C\n{\n    int a;\n    void X() { a++; }\n}"), await FingerprintAsync("class C\n{\n    int a;\n    void X() { a++; }\n} // navarone: 0000001\n"));
    }

    [Fact]
    public async Task ANullForgivingReadOfAPropertyDoesNotCallItsSetter()
    {
        Assert.Equal(2, await ValueAsync("class C { string? f; int g; string? P { get { return f; } set { g = 1; } } string? X() { return P!; } int Y() { return g; } }"));
    }

    [Fact]
    public async Task AnIdWrittenAnywhereInTheClassIsOneOfItsIds()
    {
        var facts = await CSharpSnippet.FactsAsync("// navarone: 0000001\nclass C\n{\n    void X() { } // navarone: 0000002\n}");

        Assert.Equal(["0000001", "0000002"], facts.Types.Single().NavaroneIds);
    }

    [Theory]
    [InlineData("class C { int a; C(int a) { this.a = a; } static C Open() { Check(); return new C(1); } static void Check() { } int A() => a; }")]
    [InlineData("class C { int a; C(int a) { this.a = a; } C() : this(0) { } static C Open() { Check(); return new(); } static void Check() { } int A() => a; }")]
    [InlineData("record C(int A) { static C Open() { Check(); return new C(1); } static void Check() { } public int Twice() => A * 2; }")]
    [InlineData("class C(int a) { static C Open() { Check(); return new C(1); } static void Check() { } public int A() => a; }")]
    public async Task AStaticFactoryUsesTheFieldsItsConstructorSets(string code)
    {
        Assert.Equal(1, await ValueAsync(code, "Snippet::C"));
    }

    [Fact]
    public async Task AMethodWithoutABodyJoinsItsCallers()
    {
        Assert.Equal(1, await ValueAsync("""
            using System.Text.RegularExpressions;
            static partial class C
            {
                public static bool Valid(string s) => Pattern().IsMatch(s);
                public static string[] Parts(string s) => Pattern().Split(s);
                [GeneratedRegex("a+")]
                private static partial Regex Pattern();
            }
            """, "Snippet::C"));
    }

    private static async Task<CodeFingerprint> FingerprintAsync(string code, string symbol = "Snippet::C")
    {
        var type = (await CSharpSnippet.FactsAsync(code)).Types.Single(type => type.Symbol.Value == symbol);
        return CodeFingerprint.Of(type.Symbol, type.Code);
    }

    private static async Task<int> ValueAsync(string code, string? symbol = null)
    {
        var facts = await CSharpSnippet.FactsAsync(code);
        var findings = new Lcom4Metric().Measure(facts, new CheckLimits(new Dictionary<string, int> { ["lcom4"] = 0 }));
        return findings.Count == 0 ? 0 : findings.Single(finding => symbol is null || finding.Subject.Value == symbol).Value;
    }
}
