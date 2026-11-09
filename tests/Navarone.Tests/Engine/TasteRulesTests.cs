using Navarone.Engine.Clashes;
using Navarone.Engine.Events;
using Navarone.Tests.Support;

namespace Navarone.Tests.Engine;

/// <summary>The rules of NAVARONE.md a validator's replace names by number, read from the rules heading alone.</summary>
public sealed class TasteRulesTests : IDisposable
{
    private readonly TempTaste taste = new();

    public void Dispose() => taste.Dispose();

    [Fact]
    public void ARuleRunsFromItsNumberedLineThroughTheIndentedLinesUnderIt()
    {
        var rules = TasteRules.Load(taste.Path);

        Assert.Equal(new TasteRule(1, "Imperative: statements that do one step after another."), rules.Of("1"));
        Assert.Equal(new TasteRule(2, TempTaste.Second), rules.Of("2"));
        Assert.Equal(new TasteRule(3, "Plain over clever."), rules.Of("3"));
    }

    [Theory]
    [InlineData("4")]
    [InlineData("0")]
    [InlineData("two")]
    public void ANumberUnderAnotherHeadingOrNoNumberIsNoRule(string number)
    {
        var rules = TasteRules.Load(taste.Path);

        var refused = Assert.Throws<InvalidDataException>(() => rules.Of(number));

        Assert.Equal($"replace names the rule the code breaks by its number in NAVARONE.md, 1 to 3, not \"{number}\": navarone <id> replace <rule> \"<finding>\"", refused.Message);
    }

    [Fact]
    public void ANavaroneMdWithoutRulesOrNoneAtAllIsAnError()
    {
        File.WriteAllText(taste.Path, "# NAVARONE.md\n\n## The process\n\n1. Not a rule.\n");

        Assert.Contains("lists no numbered rule", Assert.Throws<InvalidDataException>(() => TasteRules.Load(taste.Path)).Message, StringComparison.Ordinal);
        Assert.Contains("cannot be read", Assert.Throws<InvalidDataException>(() => TasteRules.Load(taste.Path + ".gone")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCheckoutsOwnNavaroneMdNumbersItsNineRules()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "Navarone.slnx")))
        {
            root = root.Parent!;
        }

        var rules = TasteRules.Load(Path.Combine(root.FullName, "rules", "NAVARONE.md"));

        Assert.Equal(new TasteRule(3, "One line does one thing: no crammed condition, no step hidden in an argument, no maths on ternaries."), rules.Of("3"));
        Assert.StartsWith("Imperative: statements that do one step after another and read top to bottom like a story. An expression", rules.Of("1").Text, StringComparison.Ordinal);
        var refused = Assert.Throws<InvalidDataException>(() => rules.Of("10"));

        Assert.Equal("replace names the rule the code breaks by its number in NAVARONE.md, 1 to 9, not \"10\": navarone <id> replace <rule> \"<finding>\"", refused.Message);
    }

    [Fact]
    public void ANavaroneMdWithCrlfLineEndsParsesAsOneWithLf()
    {
        File.WriteAllText(taste.Path, "# NAVARONE.md\r\n\r\n## The rules\r\n\r\n1. First.\r\n2. Second\r\n   over two lines.\r\n\r\n## The process\r\n");

        var rules = TasteRules.Load(taste.Path);

        Assert.Equal(new TasteRule(1, "First."), rules.Of("1"));
        Assert.Equal(new TasteRule(2, "Second over two lines."), rules.Of("2"));
    }

    [Fact]
    public void RulesUnderSubheadingsAreOneSectionInNumberOrder()
    {
        File.WriteAllText(taste.Path, "# NAVARONE.md\n\n## The rules\n\n### First\n\n2. Two.\n\n### Second\n\n1. One.\n\n## The process\n");

        var rules = TasteRules.Load(taste.Path);

        Assert.Equal(new TasteRule(1, "One."), rules.Of("1"));
        Assert.Equal(new TasteRule(2, "Two."), rules.Of("2"));
        var refused = Assert.Throws<InvalidDataException>(() => rules.Of("3"));
        Assert.Equal("replace names the rule the code breaks by its number in NAVARONE.md, 1 to 2, not \"3\": navarone <id> replace <rule> \"<finding>\"", refused.Message);
    }
}
