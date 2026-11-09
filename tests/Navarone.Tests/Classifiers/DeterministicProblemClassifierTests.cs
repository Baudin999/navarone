using Navarone.Classifiers.Deterministic;
using Navarone.Engine.Classification;
using Navarone.Engine.Configuration;
using Navarone.Engine.Facts;
using Navarone.Engine.Findings;
using Navarone.Metrics.Lcom4;
using Navarone.Tests.Support;

namespace Navarone.Tests.Classifiers;

public sealed class DeterministicProblemClassifierTests
{
    [Theory]
    [InlineData(Dispatch.None)]
    [InlineData(Dispatch.Flat)]
    [InlineData(Dispatch.Mapping)]
    public void NoRuleCallsASplitComplexityProblemAFalsePositive(Dispatch dispatch)
    {
        Assert.Null(FalsePositive("split-complexity", dispatch));
    }

    [Theory]
    [InlineData("cyclomatic", Dispatch.Flat)]
    [InlineData("cognitive", Dispatch.Flat)]
    [InlineData("cyclomatic", Dispatch.Mapping)]
    [InlineData("cognitive", Dispatch.Mapping)]
    public void AComplexityProblemOnAFlatDispatchIsAFalsePositive(string check, Dispatch dispatch)
    {
        Assert.Equal("the method is a flat dispatch: every branch is a single return or expression", FalsePositive(check, dispatch));
    }

    [Fact]
    public void AControlCouplingProblemOnAMappingIsAFalsePositive()
    {
        Assert.Equal("the method is a mapping: a flat dispatch whose every branch only returns a value", FalsePositive("control-coupling", Dispatch.Mapping));
    }

    [Theory]
    [InlineData("cyclomatic", Dispatch.None)]
    [InlineData("cognitive", Dispatch.None)]
    [InlineData("control-coupling", Dispatch.None)]
    [InlineData("control-coupling", Dispatch.Flat)]
    [InlineData("lcom4", Dispatch.Mapping)]
    public void NoRuleCallsAnyOtherProblemAFalsePositive(string check, Dispatch dispatch)
    {
        Assert.Null(FalsePositive(check, dispatch));
    }

    [Theory]
    [InlineData("""
        class C
        {
            private readonly string code;
            private readonly int max;
            public string Code => this.code;
            public int Max => max;
            public C(string code) { this.code = code; max = code.Length; }
        }
        """)]
    [InlineData("""
        class C
        {
            private System.DateTime created;
            public System.DateTime Created { get { return created; } set { created = value; } }
            private System.DateTime modified;
            public System.DateTime Modified { get { return modified; } set { modified = value; } }
            public string Name { get; set; } = "";
        }
        """)]
    public async Task AnLcom4ProblemOnAValueClassIsAFalsePositive(string code)
    {
        Assert.Equal("the class is a value class: every method is a property accessor over its own field, so each piece is data, not a separate job", await Lcom4FalsePositiveAsync(code));
    }

    [Theory]
    [InlineData("class C { int a; int b; public int A => a; public int B => b; public int Sum() => a; }")]
    [InlineData("class C { int a; int b; int c; public int A => a; public int B => b + c; }")]
    [InlineData("class C { int a; int b; public int A => a; public int B => b + Twice(); static int Twice() => 2; }")]
    [InlineData("class C { int a; int b; public int A => a; public int B => b; public static int Zero() => 0; }")]
    [InlineData("class C { System.DateTime a; System.DateTime b; public System.DateTime A { get => a; set => a = System.DateTime.SpecifyKind(value, System.DateTimeKind.Utc); } public System.DateTime B => b; }")]
    [InlineData("""
        using System;
        using System.Collections.Generic;
        using System.IO;
        using System.Net.Http;
        class TwoJobs
        {
            HttpClient http = new();
            List<int> orders = new();
            public string Page { get { var t = http.GetStringAsync("u").Result; if (t.Length > 100) t = t[..100]; File.WriteAllText("/tmp/x", t); return t; } }
            public int Revenue { get { var n = 0; foreach (var o in orders) { if (o > 0) n += o * 2; else n -= 1; } Console.WriteLine(n); return n; } }
        }
        """)]
    [InlineData("""
        using System.Net.Http;
        class TwoJobs
        {
            HttpClient http = new();
            System.Collections.Generic.List<int> rows = new();
            public string Page => http.GetStringAsync("u").Result;
            public int Rows => rows.IndexOf(1);
        }
        """)]
    [InlineData("class C { int a; int b; public int A { get { if (a > 0) return a; return 0; } } public int B => b; }")]
    public async Task NoRuleCallsAnLcom4ProblemOnAClassWithAnythingButAccessorsOverOneFieldEachAFalsePositive(string code)
    {
        Assert.Null(await Lcom4FalsePositiveAsync(code));
    }

    private static async Task<string?> Lcom4FalsePositiveAsync(string code)
    {
        var facts = await CSharpSnippet.FactsAsync(code);
        var limits = new CheckLimits(new Dictionary<string, int>(StringComparer.Ordinal) { ["lcom4"] = 1 });
        var finding = Assert.Single(new Lcom4Metric().Measure(facts, limits));
        return new DeterministicProblemClassifier().FalsePositive(new ProblemCase(finding, facts));
    }

    private static string? FalsePositive(string check, Dispatch dispatch)
    {
        var codebase = FakeFacts.With(("M", 11, "m"));
        var facts = new CodebaseFacts([.. codebase.Types.Select(type => type with { Methods = [.. type.Methods.Select(method => method with { Dispatch = dispatch })] })], Complete: true);
        var finding = new Finding(check, new SymbolId("M"), SubjectKind.Method, new SourceLocation("T.cs", 1, 1), 11, 10, "why?");
        return new DeterministicProblemClassifier().FalsePositive(new ProblemCase(finding, facts));
    }
}
