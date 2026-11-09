using Navarone.Engine.Configuration;
using Navarone.Metrics.ControlCoupling;
using Navarone.Tests.Support;

namespace Navarone.Tests.Metrics;

public sealed class ControlCouplingMetricTests
{
    [Theory]
    [InlineData("bool fast", "return fast ? 1 : 2;", 1)]
    [InlineData("bool fast", "if (!fast) return 1; return 2;", 1)]
    [InlineData("bool fast, bool loud", "if (fast && loud) return 1; return 2;", 2)]
    [InlineData("bool? fast", "while (fast == true) { } return 1;", 1)]
    [InlineData("Mode mode", "switch (mode) { case Mode.A: return 1; default: return 2; }", 1)]
    [InlineData("Mode mode", "return mode switch { Mode.A => 1, _ => 2 };", 1)]
    [InlineData("Mode mode", "if (mode == Mode.B) return 1; return 2;", 1)]
    [InlineData("int count", "if (count == 3) { return 1; } else { return 2; }", 1)]
    [InlineData("string kind", "if (kind == \"a\") { return 1; } else { return 2; }", 1)]
    [InlineData("int count", "if (count > 3) return 1; else return 2;", 1)]
    [InlineData("int count", "return count is 1 or 2 ? 1 : 0;", 1)]
    [InlineData("int count", "switch (count) { case 1: return 1; default: return 2; }", 1)]
    [InlineData("string text", "if (text is null) throw new ArgumentNullException(nameof(text)); return 1;", 0)]
    [InlineData("string text", "if (text == null) { throw new ArgumentNullException(nameof(text)); } return 1;", 0)]
    [InlineData("string text", "ArgumentNullException.ThrowIfNull(text); return 1;", 0)]
    [InlineData("int count", "if (count < 0) return 0; return 1;", 0)]
    [InlineData("int count", "if (count == 0) { return 0; } return 1;", 0)]
    [InlineData("int count", "if (count != 0) throw new ArgumentException(\"x\"); return 1;", 0)]
    [InlineData("int count", "if (count is 0) return 0; return 1;", 0)]
    [InlineData("string text", "if (text == \"\") return 0; return 1;", 0)]
    [InlineData("string text", "if (text is \"\") return 0; return 1;", 0)]
    [InlineData("int count", "if (count == 0) { return 0; } else { return 1; }", 1)]
    [InlineData("int count", "if (count == 1) return 0; return 1;", 1)]
    [InlineData("string kind", "if (kind == \"a\") return 1; return 2;", 1)]
    [InlineData("string kind", "if (kind != \"a\") throw new ArgumentException(\"x\"); return 2;", 1)]
    [InlineData("int count", "if (count is 3) return 1; return 2;", 1)]
    [InlineData("int count", "if (count is > 3) return 1; return 2;", 0)]
    [InlineData("int count", "if (count >= 3) throw new ArgumentException(\"x\"); return 2;", 0)]
    [InlineData("string text", "if (text != null) return 1; return 2;", 0)]
    [InlineData("string text", "return text is not null ? 1 : 2;", 0)]
    [InlineData("bool? flag", "if (flag == null) throw new ArgumentNullException(nameof(flag)); return 1;", 0)]
    [InlineData("Mode? mode", "if (mode is null) return 0; return 1;", 0)]
    [InlineData("Mode? mode", "return mode is not null ? 1 : 2;", 0)]
    [InlineData("Mode? mode", "return mode is null or Mode.A ? 1 : 2;", 1)]
    [InlineData("object o, int count", "return o switch { string when count > 3 => 1, _ => 2 };", 1)]
    [InlineData("object o, string kind", "switch (o) { case string when kind == \"a\": return 1; default: return 2; }", 1)]
    [InlineData("bool fast", "return Other(fast);", 0)]
    [InlineData("bool fast", "if (Other(fast)) return 1; return 2;", 0)]
    [InlineData("string text", "if (text.Length > 3) return 1; return 2;", 0)]
    [InlineData("int count", "var local = count; if (local > 3) return 1; return 2;", 0)]
    [InlineData("int count", "var local = count; return local > 3 ? 1 : 2;", 1)]
    [InlineData("bool bold", "var factor = bold; return factor ? 1 : 2;", 1)]
    [InlineData("bool bold", "var copy = bold; var factor = (copy); if (factor) { return 1; } else { return 2; }", 1)]
    [InlineData("bool bold", "var factor = bold; factor = !factor; return factor ? 1 : 2;", 0)]
    [InlineData("bool bold", "var factor = bold; Flip(ref factor); return factor ? 1 : 2;", 0)]
    [InlineData("bool bold", "var factor = bold && Other(true) > 0; return factor ? 1 : 2;", 0)]
    [InlineData("object o", "return o is string { Length: > 1 } ? 1 : 2;", 0)]
    [InlineData("object o", "return o switch { string { Length: 3 } => 1, _ => 2 };", 0)]
    [InlineData("int[] items", "return items is [1, 2] ? 1 : 2;", 0)]
    [InlineData("(int, int) pair", "return pair is (1, _) ? 1 : 2;", 0)]
    [InlineData("int count", "return count is not 3 and not 4 ? 1 : 2;", 1)]
    public async Task CountsTheParametersThatSteer(string parameters, string body, int expected)
    {
        var facts = await Facts(parameters, body);

        var findings = new ControlCouplingMetric().Measure(facts, CheckLimits.Defaults);
        Assert.Equal(expected, findings.SingleOrDefault()?.Value ?? 0);
    }

    [Fact]
    public async Task TheQuestionNamesTheSteeringParameters()
    {
        var facts = await Facts("bool fast, int count, bool loud", "if (fast && loud) return count; return 0;");

        var finding = Assert.Single(new ControlCouplingMetric().Measure(facts, CheckLimits.Defaults));
        Assert.Equal("control-coupling", finding.Check);
        Assert.Equal(2, finding.Value);
        Assert.Contains("fast, loud steer which branch runs", finding.Question, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFindingNeedsTheValueAboveTheLimit()
    {
        var facts = await Facts("bool fast", "return fast ? 1 : 2;");

        Assert.Empty(new ControlCouplingMetric().Measure(facts, new CheckLimits(new Dictionary<string, int> { ["control-coupling"] = 1 })));
    }

    private static Task<Navarone.Engine.Facts.CodebaseFacts> Facts(string parameters, string body) => CSharpSnippet.FactsAsync($$"""
        using System;
        enum Mode { A, B }
        class C
        {
            int Other(bool b) => 0;
            static void Flip(ref bool b) => b = !b;
            int M({{parameters}})
            {
                {{body}}
            }
        }
        """);
}
