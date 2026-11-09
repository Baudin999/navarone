using Navarone.Engine.Lenses;

namespace Navarone.Tests.Lenses;

public sealed class LensParserTests
{
    [Fact]
    public void ALensIsAKindANameAndViewsWithValuesAndViewsOfTheirOwnToAnyDepth()
    {
        var lens = Lens.Parse("method Burns.Apply callers:3,lines:5 [expand:1 [class:short] class] expand:2");

        Assert.Equal("method", lens.Kind.Text);
        Assert.Equal("Burns.Apply", lens.Name.Text);
        Assert.Equal(["callers:3,lines:5", "expand:2"], lens.Views.Select(view => view.Written));
        var callers = lens.Views[0];
        Assert.Equal([(null, "3"), ("lines", "5")], callers.Values.Select(value => (value.Name?.Text, value.Value.Text)));
        Assert.Equal(["expand:1", "class"], callers.Views.Select(view => view.Written));
        Assert.Equal("class:short", Assert.Single(callers.Views[0].Views).Written);
    }

    [Fact]
    public void ANameTakesTheNumberOfAnOverloadAfterAColon()
    {
        var lens = Lens.Parse("method A.M:2 expand");

        Assert.Equal("A.M", lens.Name.Text);
        Assert.Equal(("2", 12), (lens.Overload?.Text, lens.Overload?.Column));
        Assert.Null(Lens.Parse("method A.M expand").Overload);
    }

    [Theory]
    [InlineData("method A.M callers:2 [expand:1]")]
    [InlineData("method A.M callers:2 [ expand:1 ]")]
    [InlineData("method A.M callers:2[expand:1]")]
    [InlineData("  method   A.M\tcallers:2  [expand:1  ]  ")]
    public void SpacesInAndAroundBracketsDoNotMatter(string text)
    {
        var lens = Lens.Parse(text);

        var callers = Assert.Single(lens.Views);
        Assert.Equal("callers:2", callers.Written);
        Assert.Equal("expand:1", Assert.Single(callers.Views).Written);
    }

    [Fact]
    public void EachWordKnowsTheColumnItStartsAt()
    {
        var lens = Lens.Parse("method A.M callers:2,lines:5");

        Assert.Equal(8, lens.Name.Column);
        Assert.Equal(12, lens.Views[0].Word.Column);
        Assert.Equal(22, lens.Views[0].Values[1].Column);
        Assert.Equal(28, lens.Views[0].Values[1].Value.Column);
    }

    [Theory]
    [InlineData("", "expected a kind, such as method, not the end of the lens, at column 1")]
    [InlineData("method", "expected a name, such as Type.Method, not the end of the lens, at column 7")]
    [InlineData("method A.M callers:", "expected a value after :, not the end of the lens, at column 20")]
    [InlineData("method A.M callers:2,", "expected a value after ,, not the end of the lens, at column 22")]
    [InlineData("method A.M callers:lines:", "expected a value for lines after :, not the end of the lens, at column 26")]
    [InlineData("method A.M callers:2 [expand:1", "expected a view, or ] to close the [ at column 22, not the end of the lens, at column 31")]
    [InlineData("method A.M callers:2 ]", "expected a view, not ], at column 22")]
    [InlineData("method A.M:", "expected an overload number after :, not the end of the lens, at column 12")]
    [InlineData("method A.M:2:3", "expected a view, not :, at column 13")]
    [InlineData("method [ A.M", "expected a name, such as Type.Method, not [, at column 8")]
    public void ALensThatBreaksTheGrammarSaysWhatWasExpectedAndWhere(string text, string message)
    {
        var error = Assert.Throws<InvalidDataException>(() => Lens.Parse(text));

        Assert.StartsWith($"{message} of the lens", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnErrorShowsTheLensWithACaretUnderWhereItIsWrong()
    {
        var error = Assert.Throws<InvalidDataException>(() => Lens.Parse("method A.M callers:2 ]"));

        Assert.Equal(["expected a view, not ], at column 22 of the lens", "  method A.M callers:2 ]", "                       ^"], error.Message.Split('\n'));
    }

    [Theory]
    [InlineData("class Burns", "the kind class is not built yet; only method is, at column 1")]
    [InlineData("module Navarone.Engine", "the kind module is not built yet; only method is, at column 1")]
    [InlineData("function A.M", "unknown kind function; the kinds are method, class and module, and only method is built, at column 1")]
    [InlineData("method Apply", "a method is named Type.Method, such as Parser.Parse, not Apply, at column 8")]
    [InlineData("method A.", "a method is named Type.Method, such as Parser.Parse, not A., at column 8")]
    [InlineData("method Outer.Inner.Run", "a method is named Type.Method, such as Parser.Parse, not Outer.Inner.Run, at column 8")]
    [InlineData("method A.M callers [pipe]", "unknown view pipe; a method takes callers, expand and class, at column 21")]
    [InlineData("method A.M callers:0", "count of callers takes a whole number from 1, at column 20")]
    [InlineData("method A.M callers:3,lines:-1", "lines of callers takes a whole number from 0, at column 28")]
    [InlineData("method A.M callers:3,lines:2,lines:5", "callers gives lines twice, at column 30")]
    [InlineData("method A.M callers:3,depth:2", "callers takes count, then lines, at column 22")]
    [InlineData("method A.M callers:3,4,5", "callers takes count, then lines, at column 24")]
    [InlineData("method A.M expand:x", "depth of expand takes a whole number from 1, at column 19")]
    [InlineData("method A.M class:full", "form of class takes short, the one form built, at column 18")]
    [InlineData("method A.M class [expand]", "class takes no views in brackets, at column 19")]
    public void AKindOrViewNotBuiltOrAValueAViewDoesNotTakeIsAnError(string text, string message)
    {
        var error = Assert.Throws<InvalidDataException>(() => Lens.Parse(text));

        Assert.StartsWith($"{message} of the lens", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AValueAloneIsTheParameterAtItsPlace()
    {
        var lens = Lens.Parse("method A.M callers:3,5");

        Assert.Equal("callers:3,5", Assert.Single(lens.Views).Written);
    }
}
