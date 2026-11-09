using Navarone.Cli;
using Navarone.Engine;
using Navarone.Engine.Configuration;
using Navarone.Engine.Facts;
using Navarone.Engine.Reasons;
using Navarone.Metrics.Cyclomatic;
using Navarone.Metrics.SplitComplexity;
using Navarone.Tests.Support;

namespace Navarone.Tests.Metrics;

public sealed class SplitComplexityMetricTests
{
    // Cyclomatic limit 3 and split limit 4, so a method of two ifs is 3, and a method of one if with two such helpers
    // inlined is 6.
    private static readonly CheckLimits Limits = new(new Dictionary<string, int>(StringComparer.Ordinal) { ["cyclomatic"] = 3, ["split-complexity"] = 4 });

    [Fact]
    public async Task ASplitOverTheLimitListsTheHelpersThatAddDecisionsWithTheDecisionsEachAdds()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                void Run(int a) { if (a > 0) { } Left(a); Right(a); Flat(); }
                void Left(int a) { if (a > 1) { } if (a > 2) { } }
                void Right(int a) { if (a > 3) { } if (a > 4) { } }
                void Flat() { }
            }
            """);

        var finding = Assert.Single(new SplitComplexityMetric().Measure(facts, Limits));

        Assert.Equal("split-complexity", finding.Check);
        Assert.EndsWith("C.Run(int)", finding.Subject.Value, StringComparison.Ordinal);
        Assert.Equal(6, finding.Value);
        Assert.Equal(4, finding.Limit);
        Assert.Equal([("Flat", 0), ("Left", 2), ("Right", 2)], finding.Helpers!.Select(helper => (helper.Name, helper.Decisions)));
        Assert.Contains("Run(int) (2)", finding.Question, StringComparison.Ordinal);
        Assert.Contains("With its helpers inlined it would have cyclomatic 6.", finding.Question, StringComparison.Ordinal);
        Assert.EndsWith("A reason names each helper that adds decisions by its method name: Left(int) adds 2, Right(int) adds 2.", finding.Question, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASplitWhoseHelpersAddNoDecisionsHasNoneToName()
    {
        var limits = new CheckLimits(new Dictionary<string, int>(StringComparer.Ordinal) { ["cyclomatic"] = 3, ["split-complexity"] = 2 });
        var facts = await CSharpSnippet.FactsAsync("class C { void Run(int a) { if (a > 0) { } if (a > 1) { } Help(); } void Help() { } }");

        var finding = Assert.Single(new SplitComplexityMetric().Measure(facts, limits));

        Assert.EndsWith("by its method name: none.", finding.Question, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASplitUnderTheLimitIsQuiet()
    {
        var facts = await CSharpSnippet.FactsAsync("class C { void Run(int a) { if (a > 0) { } Help(a); } void Help(int a) { if (a > 1) { } } }");

        Assert.Empty(new SplitComplexityMetric().Measure(facts, Limits));
    }

    [Fact]
    public async Task TheDefaultLimitIsTen()
    {
        var facts = await CSharpSnippet.FactsAsync("class C { void Run(int a) { if (a > 0) { } Help(a); } void Help(int a) { if (a > 1) { } } }");

        Assert.Empty(new SplitComplexityMetric().Measure(facts, CheckLimits.Defaults));
    }

    [Fact]
    public async Task AHelperWithASecondCallerIsNotPartOfTheSplit()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                void Run(int a) { if (a > 0) { } Shared(a); }
                void Other(int a) { Shared(a); }
                void Shared(int a) { if (a > 1) { } if (a > 2) { } if (a > 3) { } }
            }
            """);

        Assert.Empty(new SplitComplexityMetric().Measure(facts, Limits));
    }

    [Fact]
    public async Task AHelperAnotherMethodNamesInANameofIsStillPartOfTheSplit()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                int a;
                void Run() { if (a > 0) { } _ = Left; Right(); }
                bool Left { get { if (a > 1) { } if (a > 2) { } return true; } }
                void Right() { if (a > 3) { } if (a > 4) { } }
                string Other() => nameof(Left) + nameof(Right);
            }
            """);

        var finding = Assert.Single(new SplitComplexityMetric().Measure(facts, Limits));

        Assert.Equal([("Left", 2), ("Right", 2)], finding.Helpers!.Select(helper => (helper.Name, helper.Decisions)));
    }

    [Fact]
    public async Task AHelperAnInitializerAlsoCallsIsNotPartOfTheSplit()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                private readonly System.Func<int, int> other = a => a > 0 ? Left(a) : 0;
                void Run(int a) { if (a > 0) { } Left(a); Right(a); }
                static int Left(int a) { if (a > 1) { } if (a > 2) { } return a; }
                void Right(int a) { if (a > 3) { } if (a > 4) { } }
            }
            """);

        Assert.Empty(new SplitComplexityMetric().Measure(facts, Limits));
    }

    [Fact]
    public async Task EveryHelperAddsItsDecisionsAMappingOrAFlatDispatchIncluded()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            enum K { A, B, C }
            class C
            {
                void Run(K k) { if (k == K.A) { } Label(k); Pick(k); }
                static string Label(K k) => k switch { K.A => "a", K.B => "b", _ => "c" };
                int Pick(K k) => k == K.A ? One() : 0;
                int One() => 1;
            }
            """);

        var finding = Assert.Single(new SplitComplexityMetric().Measure(facts, Limits));

        Assert.Equal(5, finding.Value);
        Assert.EndsWith("by its method name: Label(K) adds 2, Pick(K) adds 1.", finding.Question, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HelpersThatHideTheirTestsInAConditionalScoreAsThePlainOnes()
    {
        const string Probe = """
            public static class Hidden
            {
                public static int Decide(int a, int b, int c)
                {
                    if (a > b) { a--; }
                    if (b > c) { b--; }
                    if (P1(a, b, c)) { return 1; }
                    if (P2(a, b, c)) { return 2; }
                    return P3(a, b, c) ? 3 : 4;
                }

                private static bool P1(int a, int b, int c) => a > 0 && b > 0 && c > 0 && a < 10 && b < 10 && c < 10 && a != b && b != c ? true : false;
                private static bool P2(int a, int b, int c) => a > 1 && b > 1 && c > 1 && a < 11 && b < 11 && c < 11 && a != c && b != a ? true : false;
                private static bool P3(int a, int b, int c) => a > 2 && b > 2 && c > 2 && a < 12 && b < 12 && c < 12 && c != b && c != a ? true : false;
            }
            """;
        var hidden = await CSharpSnippet.FactsAsync(Probe);
        var plain = await CSharpSnippet.FactsAsync(Probe.Replace(" ? true : false", "", StringComparison.Ordinal));

        var finding = Assert.Single(new SplitComplexityMetric().Measure(hidden, CheckLimits.Defaults));

        Assert.Equal(Assert.Single(new SplitComplexityMetric().Measure(plain, CheckLimits.Defaults)).Value + 3, finding.Value);
        Assert.All(hidden.Types.Single().Methods.Where(method => method.Name.StartsWith('P')), helper => Assert.Equal(Dispatch.None, helper.Dispatch));
    }

    [Theory]
    [InlineData("interface I { void Left(int a); } class C : I { void Run(int a) { if (a > 0) { } Left(a); Right(a); } public void Left(int a) { if (a > 1) { } if (a > 2) { } } void Right(int a) { if (a > 3) { } } }")]
    [InlineData("abstract class B { protected abstract void Left(int a); } class C : B { void Run(int a) { if (a > 0) { } Left(a); Right(a); } protected override void Left(int a) { if (a > 1) { } if (a > 2) { } } void Right(int a) { if (a > 3) { } } }")]
    [InlineData("interface I { int Left { get; } } class C : I { void Run(int a) { if (a > 0) { } _ = Left; Right(a); } public int Left { get { if (Right(1) > 1) { } if (Right(2) > 2) { } return 0; } } int Right(int a) { if (a > 3) { } return a; } }")]
    public async Task AMethodThatImplementsAContractDeclaredOutsideItsTypeIsNoHelper(string code)
    {
        var facts = await CSharpSnippet.FactsAsync(code);

        Assert.Empty(new SplitComplexityMetric().Measure(facts, Limits));
    }

    [Theory]
    [InlineData("class C : C.I { private interface I { void Left(int a); } void Run(int a) { if (a > 0) { } Left(a); Right(a); } public void Left(int a) { if (a > 1) { } if (a > 2) { } } void Right(int a) { if (a > 3) { } } }")]
    [InlineData("class C { void Run(int a) { if (a > 0) { } new D().Left(a); } private interface I { void Left(int a); } private sealed class D : I { public void Left(int a) { if (a > 1) { } if (a > 2) { } Right(a); } void Right(int a) { if (a > 3) { } } } }")]
    public async Task AContractDeclaredInsideTheRootsTypeHidesNoHelper(string code)
    {
        var facts = await CSharpSnippet.FactsAsync(code);

        Assert.Equal(5, Assert.Single(new SplitComplexityMetric().Measure(facts, Limits)).Value);
    }

    [Fact]
    public async Task AMethodGroupReferenceCountsAsACallAndSeveralCallSitesAreOneCaller()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                void Run(int[] a) { if (a.Length > 0) { } if (a.Length > 1) { } System.Array.ForEach(a, Each); Each(1); }
                void Each(int x) { if (x > 1) { } if (x > 2) { } }
            }
            """);

        var finding = Assert.Single(new SplitComplexityMetric().Measure(facts, Limits));
        Assert.Equal(5, finding.Value);
        Assert.Single(finding.Helpers!);
    }

    [Fact]
    public async Task HelpersAreFollowedTransitively()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                void Run(int a) { if (a > 0) { } Mid(a); }
                void Mid(int a) { if (a > 1) { } Leaf(a); }
                void Leaf(int a) { if (a > 2) { } if (a > 3) { } }
            }
            """);

        var finding = Assert.Single(new SplitComplexityMetric().Measure(facts, Limits));

        Assert.EndsWith("C.Run(int)", finding.Subject.Value, StringComparison.Ordinal);
        Assert.Equal(5, finding.Value);
        Assert.Equal(2, finding.Helpers!.Count);
    }

    [Fact]
    public async Task RecursionBetweenHelpersStaysInTheSplit()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                void Run(int a) { if (a > 0) { } Ping(a); }
                void Ping(int a) { if (a > 1) { } Pong(a); }
                void Pong(int a) { if (a > 2) { } if (a > 3) { } Ping(a); Pong(a); }
            }
            """);

        var finding = Assert.Single(new SplitComplexityMetric().Measure(facts, Limits));
        Assert.Equal(5, finding.Value);
    }

    [Fact]
    public async Task ARecursiveHelperThatAnotherMethodAlsoCallsIsOutButItsOwnHelperStillSplits()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                void Run(int a) { if (a > 0) { } Ping(a); }
                void Other(int a) { Ping(a); }
                void Ping(int a) { if (a > 1) { } if (a > 2) { } Pong(a); }
                void Pong(int a) { if (a > 2) { } if (a > 3) { } Ping(a); }
            }
            """);

        var finding = Assert.Single(new SplitComplexityMetric().Measure(facts, Limits));
        Assert.EndsWith("C.Ping(int)", finding.Subject.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMethodAlreadyRedForCyclomaticIsNotAlsoASplitAndAHelperOverTheLimitIsNotHidden()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                void Big(int a) { if (a > 0) { } if (a > 1) { } if (a > 2) { } Help(a); }
                void Help(int a) { if (a > 3) { } if (a > 4) { } }
                void Run(int a) { if (a > 0) { } Heavy(a); }
                void Heavy(int a) { if (a > 1) { } if (a > 2) { } if (a > 3) { } if (a > 4) { } }
            }
            """);

        Assert.Empty(new SplitComplexityMetric().Measure(facts, Limits));
    }

    [Fact]
    public async Task AHelperThatIsItselfASplitBelongsToTheOuterFindingOnly()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                void Run(int a) { if (a > 0) { } Mid(a); }
                void Mid(int a) { if (a > 1) { } if (a > 2) { } Leaf(a); }
                void Leaf(int a) { if (a > 3) { } if (a > 4) { } }
            }
            """);

        var finding = Assert.Single(new SplitComplexityMetric().Measure(facts, Limits));
        Assert.EndsWith("C.Run(int)", finding.Subject.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadsTheCyclomaticLimitFromTheLimitsSoACyclomaticRedMethodIsNotASplit()
    {
        var facts = await CSharpSnippet.FactsAsync("class C { void Run(int a) { if (a > 0) { } Help(a); } void Help(int a) { if (a > 1) { } if (a > 2) { } } }");
        var strict = new CheckLimits(new Dictionary<string, int>(StringComparer.Ordinal) { ["cyclomatic"] = 2, ["split-complexity"] = 3 });

        Assert.Empty(new SplitComplexityMetric().Measure(facts, strict));
    }

    [Fact]
    public async Task TwoMethodsThatOnlyCallEachOtherAreOneSplitReportedOnce()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                public void Parse(int a) { if (a > 0) { } if (a > 1) { } ParseExpr(a); }
                void ParseExpr(int a) { if (a > 2) { } if (a > 3) { } Parse(a); }
            }
            """);

        var finding = Assert.Single(new SplitComplexityMetric().Measure(facts, Limits));
        Assert.EndsWith("C.Parse(int)", finding.Subject.Value, StringComparison.Ordinal);
        Assert.Equal(5, finding.Value);
    }

    [Theory]
    [InlineData("public")]
    [InlineData("internal")]
    [InlineData("protected")]
    [InlineData("private")]
    public async Task AHelperOfAnyAccessibilityIsPartOfTheSplit(string access)
    {
        var facts = await CSharpSnippet.FactsAsync($$"""
            class C
            {
                void Run(int a) { if (a > 0) { } Left(a); Right(a); }
                void Left(int a) { if (a > 1) { } if (a > 2) { } }
                {{access}} void Right(int a) { if (a > 3) { } if (a > 4) { } }
            }
            """);

        var finding = Assert.Single(new SplitComplexityMetric().Measure(facts, Limits));
        Assert.Equal(6, finding.Value);
        Assert.Equal(2, finding.Helpers!.Count);
    }

    [Fact]
    public async Task AHelperInATypeNestedInTheRootsTypeIsPartOfTheSplit()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                void Run(int a) { if (a > 0) { } Inner.Left(a); Inner.Deeper.Right(a); }
                private static class Inner
                {
                    internal static void Left(int a) { if (a > 1) { } if (a > 2) { } }
                    internal static class Deeper { internal static void Right(int a) { if (a > 3) { } if (a > 4) { } } }
                }
            }
            """);

        var finding = Assert.Single(new SplitComplexityMetric().Measure(facts, Limits));
        Assert.EndsWith("C.Run(int)", finding.Subject.Value, StringComparison.Ordinal);
        Assert.Equal(6, finding.Value);
    }

    [Fact]
    public async Task AMethodOfTheTypeANestedRootSitsInIsAHandOffNotAHelper()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                static void Left(int a) { if (a > 1) { } if (a > 2) { } }
                static void Right(int a) { if (a > 3) { } if (a > 4) { } }
                class Inner { void Run(int a) { if (a > 0) { } Left(a); Right(a); } }
            }
            """);

        Assert.Empty(new SplitComplexityMetric().Measure(facts, Limits));
    }

    [Fact]
    public async Task ASplitScoresWhatTheMethodWithItsHelpersInlinedScoresForCyclomatic()
    {
        var split = await CSharpSnippet.FactsAsync("""
            class C
            {
                void Run(int a) { if (a > 0) { } Left(a); Right(a); }
                void Left(int a) { if (a > 1) { } if (a > 2) { } }
                void Right(int a) { if (a > 3) { } if (a > 4) { } }
            }
            """);
        var inlined = await CSharpSnippet.FactsAsync("class C { void Run(int a) { if (a > 0) { } if (a > 1) { } if (a > 2) { } if (a > 3) { } if (a > 4) { } } }");

        Assert.Equal(CyclomaticMetric.ValueOf(inlined.Method("C.Run(int)")), Assert.Single(new SplitComplexityMetric().Measure(split, Limits)).Value);
    }

    [Fact]
    public async Task HelpersWithoutDecisionsAddNothing()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                void Run(int a) { if (a > 0) { } if (a > 1) { } One(a); Two(a); Three(a); Four(a); }
                void One(int a) { }
                void Two(int a) { }
                void Three(int a) { }
                void Four(int a) { }
            }
            """);

        Assert.Empty(new SplitComplexityMetric().Measure(facts, Limits));
    }

    [Fact]
    public async Task AMethodOfAnotherClassIsAHandOffNotAHelper()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C { void Run(int a) { if (a > 0) { } if (a > 9) { } new D().Help(a); } }
            class D { public void Help(int a) { if (a > 1) { } if (a > 2) { } } }
            """);

        Assert.Empty(new SplitComplexityMetric().Measure(facts, Limits));
    }

    [Fact]
    public async Task TheFingerprintFollowsAChangeToAHelper()
    {
        const string Source = """
            class C
            {
                void Run(int a) { if (a > 0) { } Left(a); Right(a); }
                void Left(int a) { if (a > 1) { } if (a > 2) { } }
                void Right(int a) { if (a > 3) { } if (a > 4) { } }
            }
            """;
        var first = await FingerprintAsync(Source);

        Assert.Equal(first, await FingerprintAsync(Source));
        Assert.NotEqual(first, await FingerprintAsync(Source.Replace("a > 4", "a > 5", StringComparison.Ordinal)));
    }

    private static async Task<CodeFingerprint> FingerprintAsync(string source)
    {
        using var repo = new TempRepo();
        using var events = new TempEvents();
        await File.WriteAllTextAsync(Path.Combine(repo.Root, NavaroneConfig.FileName), """{ "limits": { "cyclomatic": 3, "split-complexity": 5 } }""", TestContext.Current.CancellationToken);
        var provider = new FakeProvider();
        provider.Codebases["a.sln"] = await CSharpSnippet.FactsAsync(source);
        var setup = events.Isolate(Wiring.For(repo.Root, events.Handler)) with { Provider = provider };
        var run = await Analysis.RunAsync(setup, ["a.sln"], Coverage.Full, TestContext.Current.CancellationToken);
        return Assert.Single(run.Verdicts, verdict => verdict.Problem.Check == "split-complexity").Problem.Fingerprint;
    }
}
