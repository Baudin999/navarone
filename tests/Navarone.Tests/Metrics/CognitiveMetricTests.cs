using Navarone.Engine.Configuration;
using Navarone.Engine.Facts;
using Navarone.Metrics.Cognitive;
using Navarone.Tests.Support;

namespace Navarone.Tests.Metrics;

public sealed class CognitiveMetricTests
{
    [Theory]
    [InlineData("return 0;", 0)]
    // white paper: sumOfPrimes, with a goto for the labelled continue: for +1, for +2, if +3, goto +1
    [InlineData("var t = 0; for (var i = 1; i <= a; i++) { for (var j = 2; j < i; j++) { if (i % j == 0) goto next; } t += i; next:; } return t;", 7)]
    // white paper: getWords, a switch is one however many cases
    [InlineData("switch (a) { case 1: return \"one\"; case 2: return \"two\"; case 3: return \"three\"; default: return \"lots\"; }", 1)]
    // white paper: the try, if, for, if, catch, if example
    [InlineData("try { if (a > 0) { for (var i = 0; i < a; i++) { if (i == 2) { a++; } } } } catch (Exception) { if (a > 1) { a--; } } return a;", 9)]
    [InlineData("if (a > 0) return 1; else if (a < 0) return -1; else return 0;", 3)]
    [InlineData("if (a > 0) { if (a > 1) return 1; else return 2; } return 0;", 4)]
    [InlineData("return a > 0 ? (a > 5 ? 2 : 1) : 0;", 3)]
    [InlineData("while (a > 0) { a--; } do { a++; } while (a < 3); foreach (var c in s) { } return a;", 3)]
    [InlineData("if (a > 0 && a < 5 && a != 3 || a == 9 || a == 8 && a != 1) return 1; return 0;", 4)]
    [InlineData("if (a > 0 && (a < 5 && a != 3)) return 1; return 0;", 2)]
    [InlineData("if (a > 0 && a < 5 || a == 9) return 1; return 0;", 3)]
    [InlineData("return a is 1 or 2 or 3;", 1)]
    [InlineData("return a switch { 1 => 1, > 5 => 2, _ => 0 };", 1)]
    [InlineData("Func<int, int> f = x => x > 0 ? x : -x; return f(a);", 2)]
    [InlineData("if (a > 0) { Func<int, int> f = x => x > 0 ? x : -x; return f(a); } return 0;", 4)]
    [InlineData("return Local(a); static int Local(int x) { if (x > 0) return x; return 0; }", 2)]
    [InlineData("return a > 0 ? M(a - 1, s) : 0;", 2)]
    [InlineData("s ??= \"x\"; return s?.Length ?? a;", 0)]
    [InlineData("return a switch { > 0 when a < 9 => 1, _ => 0 };", 2)]
    [InlineData("return s is { Length: > 0 };", 1)]
    [InlineData("return a is > 0 and < 9;", 1)]
    [InlineData("return s is not null;", 0)]
    [InlineData("return a is not (1 or 2);", 1)]
    [InlineData("return s is [..];", 0)]
    [InlineData("return s is [var c, ..];", 1)]
    [InlineData("return s is ['a', _, ..];", 2)]
    [InlineData("return s is { Length: var n } ? n : 0;", 1)]
    [InlineData("if (s is { Length: > 0 } && a > 0) { return 1; } return 0;", 3)]
    public async Task CountsTheBreaksInTheFlowWeightedByNesting(string body, int expected)
    {
        var facts = await CSharpSnippet.FactsAsync($$"""
            using System;
            class C
            {
                dynamic M(int a, string s)
                {
                    {{body}}
                }
            }
            """);

        Assert.Equal(expected, ValueOf(facts, "C.M(int, string)"));
    }

    [Fact]
    public async Task CountsARecursiveMethodOnce()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                int Factorial(int n)
                {
                    if (n <= 1) return 1;
                    return n * Factorial(n - 1) + Factorial(n - 2);
                }
            }
            """);

        Assert.Equal(2, ValueOf(facts, "C.Factorial(int)"));
    }

    [Fact]
    public async Task CountsEachMethodOfAnIndirectCycleButNotOneThatOnlyCallsIntoIt()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                void A() { B(); }
                void B() { A(); }
                void Entry() { A(); }
                void Chain1() { Chain2(); }
                void Chain2() { }
            }
            """);

        Assert.Equal(1, ValueOf(facts, "C.A()"));
        Assert.Equal(1, ValueOf(facts, "C.B()"));
        Assert.Equal(0, ValueOf(facts, "C.Entry()"));
        Assert.Equal(0, ValueOf(facts, "C.Chain1()"));
    }

    [Fact]
    public async Task RaisesAFindingOnlyAboveTheLimit()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                int Low(int a) { if (a > 0) { if (a > 1) { return 1; } } return 0; }
                int High(int a) { if (a > 0) { if (a > 1) { if (a > 2) { return 1; } } } return 0; }
            }
            """);
        var limits = new CheckLimits(new Dictionary<string, int>(StringComparer.Ordinal) { ["cognitive"] = 3 });

        var finding = Assert.Single(new CognitiveMetric().Measure(facts, limits));

        Assert.EndsWith("C.High(int)", finding.Subject.Value, StringComparison.Ordinal);
        Assert.Equal(6, finding.Value);
        Assert.Equal(3, finding.Limit);
        Assert.Equal("cognitive", finding.Check);
    }

    [Fact]
    public async Task TakesTheDefaultLimitOfFifteenWhenNoneIsSet()
    {
        var nested = string.Concat(Enumerable.Range(0, 5).Select(i => $"if (a > {i}) {{ "));
        var closing = new string('}', 5);
        var facts = await CSharpSnippet.FactsAsync($"class C {{ int At15(int a) {{ {nested} return 1; {closing} return 0; }} int At16(int a) {{ {nested} if (a > 9 || a < 2) return 1; {closing} return 0; }} }}");

        Assert.Equal(15, ValueOf(facts, "C.At15(int)"));
        var finding = Assert.Single(new CognitiveMetric().Measure(facts, new CheckLimits(new Dictionary<string, int>())));
        Assert.EndsWith("C.At16(int)", finding.Subject.Value, StringComparison.Ordinal);
    }

    private static int ValueOf(CodebaseFacts facts, string symbolEnd) =>
        CognitiveMetric.ValuesOf(facts).Single(entry => entry.Method.Symbol.Value.EndsWith(symbolEnd, StringComparison.Ordinal)).Value;
}
