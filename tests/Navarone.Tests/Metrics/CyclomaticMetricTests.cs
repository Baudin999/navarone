using Navarone.Engine.Configuration;
using Navarone.Engine.Facts;
using Navarone.Engine.Findings;
using Navarone.Metrics.Cyclomatic;
using Navarone.Tests.Support;

namespace Navarone.Tests.Metrics;

public sealed class CyclomaticMetricTests
{
    [Theory]
    [InlineData("return 0;", 1)]
    [InlineData("if (a > 0) return 1; else if (a < 0) return -1; else return 0;", 3)]
    [InlineData("for (var i = 0; i < a; i++) { } foreach (var c in s) { } while (a > 0) a--; do a++; while (a < 3); return a;", 5)]
    [InlineData("switch (a) { case 1: case 2: return 1; case 3: return 3; default: return 0; }", 4)]
    [InlineData("return a switch { 1 => 1, > 5 => 2, _ => 0 };", 3)]
    [InlineData("return a > 0 && s.Length > 0 || s == \"x\" ? 1 : 0;", 4)]
    [InlineData("try { return int.Parse(s); } catch (FormatException) { return 0; } catch (OverflowException) { return 1; }", 3)]
    [InlineData("return a is 1 or 2 ? 1 : 0;", 3)]
    [InlineData("Func<int, int> f = x => x > 0 ? x : -x; return f(a);", 2)]
    [InlineData("return Local(a); static int Local(int x) { if (x > 0) return x; return 0; }", 2)]
    [InlineData("if (a > 0) goto done; return 0; done: return 1;", 2)]
    [InlineData("return s?.Length ?? a;", 3)]
    [InlineData("s ??= \"x\"; return s.Length;", 2)]
    [InlineData("if (s == null) s = \"x\"; return s.Length;", 2)]
    [InlineData("return s?.Trim()?.Length > 0 ? 1 : 0;", 4)]
    [InlineData("return s is { Length: > 0 } ? 1 : 0;", 3)]
    [InlineData("return s is { Length: var n } ? n : 0;", 2)]
    [InlineData("return !(s is { Length: 0 }) ? 1 : 0;", 3)]
    [InlineData("return (a, s) is (> 0, null) ? 1 : 0;", 4)]
    [InlineData("return (a, s) is (var x, _) ? x : 0;", 2)]
    [InlineData("object o = s; return o switch { string { Length: 0 } => 1, string t when t.Length > a && a > 0 => 2, _ => 0 };", 6)]
    [InlineData("switch (a) { case > 0 when s.Length > 0: return 1; default: return 0; }", 3)]
    [InlineData("try { return int.Parse(s); } catch (FormatException) when (a > 0) { return 0; }", 3)]
    [InlineData("return a switch { > 0 => 1, _ when s.Length > 0 => 2, _ => 0 };", 3)]
    [InlineData("return a switch { > 0 => 1, var x when x < -5 => 2, var x => x };", 3)]
    [InlineData("switch (a) { case > 0: return 1; case var _: return 0; }", 2)]
    [InlineData("switch (a) { case var x when x > 0: return 1; default: return 0; }", 2)]
    [InlineData("return a is > 0 and < 9 ? 1 : 0;", 3)]
    [InlineData("return s is not null ? 1 : 0;", 2)]
    [InlineData("return a is not (1 or 2) ? 1 : 0;", 3)]
    [InlineData("return a is > 0 and not 5 ? 1 : 0;", 3)]
    [InlineData("return s is { Length: not 0 } ? 1 : 0;", 3)]
    [InlineData("return s is [] ? 1 : 0;", 3)]
    [InlineData("return s is [..] ? 1 : 0;", 2)]
    [InlineData("return s is [var c, .. var rest] ? c : 0;", 3)]
    [InlineData("return s is ['a', _, ..] ? 1 : 0;", 4)]
    [InlineData("return s is [not 'a'] ? 1 : 0;", 4)]
    [InlineData("return s is ['a' or 'b', .. ['c']] ? 1 : 0;", 8)]
    [InlineData("return new[] { new[] { 1 } } is [[1]] ? 1 : 0;", 6)]
    [InlineData("return s switch { [] => 0, ['a', ..] => 1, _ => 2 };", 6)]
    public async Task CountsTheDecisionsPlusOne(string body, int expected)
    {
        var facts = await CSharpSnippet.FactsAsync($$"""
            using System;
            class C
            {
                int M(int a, string s)
                {
                    {{body}}
                }
            }
            """);

        Assert.Equal(expected, CyclomaticMetric.ValueOf(facts.Method("C.M(int, string)")));
    }

    [Fact]
    public async Task TheEvidenceShowsAnIfAsOneItemWithItsConditionsAsPartsEachWithItsSpanAndWeight()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                int M(bool a, bool b, int c)
                {
                    if ((a || b) && c != 12) { return 1; }
                    return 0;
                }
            }
            """);

        var evidence = CyclomaticMetric.EvidenceOf(facts.Method("C.M(bool, bool, int)"));

        Assert.Equal(new CodePiece("Snippet.cs", 3, 7, "    int M(bool a, bool b, int c)\n    {\n        if ((a || b) && c != 12) { return 1; }\n        return 0;\n    }"), evidence.Code);
        Assert.Equal(1, evidence.Base);
        var branch = Assert.Single(evidence.Proof);
        Assert.Equal(("if", 1, "a branch adds one path"), (branch.Kind, branch.Adds, branch.Why));
        Assert.Equal(new SourceSpan(5, 9, 5, 32, "if ((a || b) && c != 12)"), branch.Span);
        var and = Assert.Single(branch.Parts);
        Assert.Equal(("&&", 1, "an and adds one path: it joins a second condition"), (and.Kind, and.Adds, and.Why));
        Assert.Equal(new SourceSpan(5, 13, 5, 31, "(a || b) && c != 12"), and.Span);
        var or = Assert.Single(and.Parts);
        Assert.Equal(("||", 1), (or.Kind, or.Adds));
        Assert.Equal(new SourceSpan(5, 14, 5, 19, "a || b"), or.Span);
        Assert.Empty(or.Parts);
        Assert.Equal(4, evidence.Value());
    }

    [Fact]
    public async Task TheEvidenceShowsWhatTheRuleDoesNotCountWithAddsZeroAndWhy()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                int M(int a)
                {
                    switch (a) { case 1 when a > 0: return 1; default: break; }
                    if (a > 2) { return 2; } else { return a switch { 3 => 3, _ => 4 }; }
                }
            }
            """);

        var evidence = CyclomaticMetric.EvidenceOf(facts.Method("C.M(int)"));

        Assert.Equal("switch 0 [case 1 [when 1], default 0], if 1 [else 0], switch 0 [arm 1, _ 0]", Shape(evidence.Proof));
        Assert.Equal(5, evidence.Value());
    }

    [Fact]
    public async Task TheEvidenceShowsEachTestInAnArmAsItsOwnPartAndACallOrANotAddsNothing()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            record R(object Inner);
            class C
            {
                int M(object o, bool b) => o switch
                {
                    R { Inner: string inner } r when Check(inner) && !b => 1,
                    _ => 0,
                };
                bool Check(string s) => s.Length > 0;
            }
            """);

        var evidence = CyclomaticMetric.EvidenceOf(facts.Method("C.M(object, bool)"));

        Assert.Equal("switch 0 [arm 1 [subpattern 1, when 1 [&& 1]], _ 0]", Shape(evidence.Proof));
        var arm = evidence.Proof[0].Parts[0];
        Assert.Equal(4, Flattened([arm]).Sum(item => item.Adds));
        var subpattern = arm.Parts[0];
        Assert.Equal(new SourceSpan(6, 13, 6, 31, "Inner: string inner"), subpattern.Span);
        Assert.Equal("a subpattern, list element or list length that tests a value adds one path: the match fails when the value does not fit", subpattern.Why);
        var guard = arm.Parts[1];
        Assert.Equal(new SourceSpan(6, 37, 6, 59, "when Check(inner) && !b"), guard.Span);
        Assert.Equal("a when adds one path: its case or catch can fail after its pattern matched", guard.Why);
        Assert.Equal(new SourceSpan(6, 42, 6, 59, "Check(inner) && !b"), guard.Parts[0].Span);
        Assert.Equal(5, evidence.Value());
    }

    [Fact]
    public async Task TheEvidenceShowsAListAsItsLengthTestWithEachElementThatTestsAValueAsAPart()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                int M(string s) => s is ['a' or 'b', _, .. ['c']] ? 1 : 0;
            }
            """);

        var evidence = CyclomaticMetric.EvidenceOf(facts.Method("C.M(string)"));

        Assert.Equal("?: 1 [list 1 [element 1 [or 1], element 1 [list 1 [element 1]]]]", Shape(evidence.Proof));
        var element = evidence.Proof[0].Parts[0].Parts[0];
        Assert.Equal(new SourceSpan(3, 30, 3, 39, "'a' or 'b'"), element.Span);
        Assert.Equal(element.Span, element.Parts[0].Span);
        Assert.Equal(8, evidence.Value());
    }

    [Fact]
    public async Task OverNavaronesOwnCodeTheProofHoldsEveryFactOnceSumsToTheValueAndSpansItsTextInTheFile()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "Navarone.slnx")))
        {
            root = root.Parent!;
        }

        var files = Directory.EnumerateFiles(Path.Combine(root.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"))
            .ToDictionary(file => Path.GetRelativePath(root.FullName, file), File.ReadAllText);
        var facts = await CSharpSnippet.FactsAsync([.. files.Select(file => (file.Key, file.Value))]);
        var methods = facts.Types.SelectMany(type => type.Methods).ToList();

        var findings = new CyclomaticMetric().Measure(facts, new CheckLimits(new Dictionary<string, int>(StringComparer.Ordinal) { ["cyclomatic"] = 0 }));

        Assert.True(methods.Sum(method => method.ControlFlow.Count) > 500);
        Assert.Equal(methods.Count, findings.Count);
        foreach (var (method, finding) in methods.Zip(findings))
        {
            var evidence = finding.Evidence!;
            var items = Flattened(evidence.Proof).ToList();
            Assert.Equal(method.ControlFlow.Count, items.Count);
            Assert.Equal(finding.Value, evidence.Base + items.Sum(item => item.Adds));
            Assert.Equal(finding.Value, 1 + method.ControlFlow.Count(fact => fact.Kind is not (ControlFlowKind.Else or ControlFlowKind.Switch or ControlFlowKind.Default or ControlFlowKind.Jump)));
            Assert.All(items, item => Assert.Equal(item.Span.Text, TextAt(files[method.Location.Path], item.Span)));
        }
    }

    private static IEnumerable<ProofItem> Flattened(IEnumerable<ProofItem> items) =>
        items.SelectMany(item => Flattened(item.Parts).Prepend(item));

    // The text of the file at the span's lines and columns, the last column included.
    private static string TextAt(string file, SourceSpan span)
    {
        var lines = file.Split('\n');
        var start = lines.Take(span.StartLine - 1).Sum(line => line.Length + 1) + span.StartCol - 1;
        var end = lines.Take(span.EndLine - 1).Sum(line => line.Length + 1) + span.EndCol;
        return file[start..end];
    }

    private static string Shape(IEnumerable<ProofItem> items) =>
        string.Join(", ", items.Select(item => item.Parts.Count == 0 ? $"{item.Kind} {item.Adds}" : $"{item.Kind} {item.Adds} [{Shape(item.Parts)}]"));

    [Fact]
    public async Task CountsAccessorsConstructorsAndTopLevelStatementsAsMethods()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            var n = args.Length > 0 ? 1 : 0;
            System.Console.WriteLine(n);

            class C
            {
                private int value;
                C(int v) { if (v > 0) value = v; }
                int Positive => value > 0 ? value : 0;
                int Value { get { return value; } set { if (value > 0) this.value = value; } }
            }
            """);

        Assert.Equal(2, CyclomaticMetric.ValueOf(facts.Method("Program.<top-level-statements-entry-point>")));
        Assert.Equal(2, CyclomaticMetric.ValueOf(facts.Method("C.C(int)")));
        Assert.Equal(2, CyclomaticMetric.ValueOf(facts.Method("C.Positive.get")));
        Assert.Equal(1, CyclomaticMetric.ValueOf(facts.Method("C.Value.get")));
        Assert.Equal(2, CyclomaticMetric.ValueOf(facts.Method("C.Value.set")));
    }

    [Fact]
    public void RaisesAFindingOnlyAboveTheLimit()
    {
        var atLimit = Method("AtLimit", 3);
        var aboveLimit = Method("AboveLimit", 4);
        var facts = new CodebaseFacts([new TypeFacts(new SymbolId("T"), "T", null, Here, [], [atLimit, aboveLimit], false, false, "", new Written(1, ""), [])], Complete: true);
        var limits = new CheckLimits(new Dictionary<string, int>(StringComparer.Ordinal) { ["cyclomatic"] = 4 });

        var finding = Assert.Single(new CyclomaticMetric().Measure(facts, limits));

        Assert.Equal(new SymbolId("AboveLimit"), finding.Subject);
        Assert.Equal(5, finding.Value);
        Assert.Equal(4, finding.Limit);
        Assert.Equal("cyclomatic", finding.Check);
    }

    [Fact]
    public void TakesTheDefaultLimitOfTenWhenNoneIsSet()
    {
        var facts = new CodebaseFacts([new TypeFacts(new SymbolId("T"), "T", null, Here, [], [Method("Ten", 9), Method("Eleven", 10)], false, false, "", new Written(1, ""), [])], Complete: true);

        var finding = Assert.Single(new CyclomaticMetric().Measure(facts, new CheckLimits(new Dictionary<string, int>())));

        Assert.Equal(new SymbolId("Eleven"), finding.Subject);
    }

    private static readonly SourceLocation Here = new("T.cs", 1, 1);

    private static readonly SourceSpan Head = new(1, 1, 1, 2, "if");

    private static MethodFacts Method(string symbol, int branches) => new(
        new SymbolId(symbol),
        symbol,
        Here,
        false,
        false,
        false,
        false,
        [],
        [],
        [.. Enumerable.Repeat(new ControlFlowFact(ControlFlowKind.Branch, 0, Head, false, "if", null), branches)
            .Append(new ControlFlowFact(ControlFlowKind.Else, 0, Head, false, "else", null))
            .Append(new ControlFlowFact(ControlFlowKind.Switch, 0, Head, false, "switch", null))],
        Dispatch.None,
        [],
        [],
        symbol,
        symbol,
        new Written(1, symbol),
        [],
        []);
}
