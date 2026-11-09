using System.Text.Json.Nodes;
using Navarone.Engine;
using Navarone.Engine.Lenses;
using Navarone.Tests.Support;

namespace Navarone.Tests.Lenses;

public sealed class MethodLensTests
{
    // The root the lens reads the code of: in no repository, so the lens keeps no facts cache.
    private static readonly string Root = Path.GetTempPath();

    private const string Shop = """
        class Shop
        {
            private readonly int tax;
            private int unused;
            public int Rate { get; set; }
            public int Total => tax + Rate;

            public int Price(int amount)
            {
                var raw = Base(amount);
                return raw + tax + Total;
            }

            public int Price(int amount, int discount) => Price(amount) - discount;

            private int Base(int amount) => Round(amount * Rate);

            private static int Round(int value) => System.Math.Abs(value);
        }

        class Till
        {
            private readonly Shop shop = new();

            public int Ring(int amount)
            {
                var first = 1;
                var second = 2;
                var price = shop.Price(amount);
                var third = 3;
                return price + first + second + third;
            }
        }
        """;

    [Fact]
    public async Task ShowsTheSubjectThenASectionPerViewWithEachPieceOfCodeUnderItsPathAndStartLine()
    {
        var shown = await ShowAsync("method Shop.Base expand", Shop);

        Assert.Equal(
            """
            # method Shop.Base(int), overload 1 of 1

            `Snippet.cs:16`

            ```fake
                private int Base(int amount) => Round(amount * Rate);
            ```

            ## expand

            ### Shop.Round(int)

            Called by Shop.Base(int).

            `Snippet.cs:18`

            ```fake
                private static int Round(int value) => System.Math.Abs(value);
            ```


            """,
            shown);
    }

    [Fact]
    public async Task ANameThatMatchesOverloadsShowsEachInTurnInLineOrder()
    {
        var shown = await ShowAsync("method Shop.Price", Shop);

        Assert.Equal(["# method Shop.Price(int), overload 1 of 2", "# method Shop.Price(int, int), overload 2 of 2"], Headings(shown));
    }

    [Fact]
    public async Task ANumberAfterTheNamePicksOneOverloadFromOne()
    {
        var second = await ShowAsync("method Shop.Price:2 expand:1", Shop);
        var only = await ShowAsync("method Shop.Round:1", Shop);

        Assert.Equal(["# method Shop.Price(int, int), overload 2 of 2", "## expand:1", "### Shop.Price(int)"], Headings(second));
        Assert.Equal(["# method Shop.Round(int), overload 1 of 1"], Headings(only));
    }

    [Theory]
    [InlineData("method Shop.Price:3", "Shop.Price has 2 overloads, not 3, at column 19")]
    [InlineData("method Shop.Round:2", "Shop.Round has 1 overload, not 2, at column 19")]
    [InlineData("method Shop.Price:0", "an overload is a whole number from 1, such as Show:1, at column 19")]
    [InlineData("method Shop.Price:two", "an overload is a whole number from 1, such as Show:1, at column 19")]
    public async Task ANumberWithNoOverloadIsAnErrorWithTheCaretUnderIt(string text, string message)
    {
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => ShowAsync(text, Shop));

        Assert.StartsWith($"{message} of the lens\n  {text}\n  {new string(' ', 18)}^", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CodeAlreadyShownIsReferredToNotPrintedAgain()
    {
        var shown = await ShowAsync("method Shop.Price callers:1 [expand:1 class:short] expand:1 class:short", Shop);

        Assert.Equal(1, Count(shown, "    public int Price(int amount)\n    {"));
        Assert.Equal(1, Count(shown, "private int unused"));
        Assert.Contains("Shown above at `Snippet.cs:8`.", shown, StringComparison.Ordinal);
        Assert.Contains("Shown above at `Snippet.cs:1`.", shown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACallersExcerptIsAlwaysPrintedEvenWhenTheMethodWasShown()
    {
        var shown = await ShowAsync("method Shop.Price callers:1", Shop);

        Assert.Equal(2, Count(shown, "Price(amount) - discount;"));
    }

    [Fact]
    public async Task ExpandShowsWhatTheMethodCallsInTheCodebaseToTheDepthGivenEachOnce()
    {
        var shown = await ShowAsync("method Shop.Price expand:2", Shop);

        Assert.Equal(
            [
                "# method Shop.Price(int), overload 1 of 2", "## expand:2", "### Shop.Base(int)", "### Shop.Total.get", "### Shop.Round(int)",
                "# method Shop.Price(int, int), overload 2 of 2", "## expand:2", "### Shop.Price(int)", "### Shop.Base(int)", "### Shop.Total.get",
            ],
            Headings(shown));
        Assert.Contains("### Shop.Round(int)\n\nCalled by Shop.Base(int).", shown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallersShowsEachCallSiteWithTheLinesAroundItAndAppliesItsViewsToTheCallingMethod()
    {
        var shown = await ShowAsync("method Shop.Price callers:2,lines:1 [class:short]", Shop);

        Assert.Equal(
            [
                "# method Shop.Price(int), overload 1 of 2", "## callers:2,lines:1",
                "### Shop.Price(int, int)", "#### class:short", "##### Shop", "###### Fields and properties it uses",
                "### Till.Ring(int)", "#### class:short", "##### Till", "###### Fields and properties it uses",
                "# method Shop.Price(int, int), overload 2 of 2", "## callers:2,lines:1",
            ],
            Headings(shown));
        Assert.Contains(
            """
            Calls it at `Snippet.cs:29`.

            `Snippet.cs:28`

            ```fake
                    var second = 2;
                    var price = shop.Price(amount);
                    var third = 3;
            ```
            """,
            shown,
            StringComparison.Ordinal);
        Assert.Contains("    private readonly Shop shop = new();", shown, StringComparison.Ordinal);
        Assert.EndsWith("## callers:2,lines:1\n\nNo method in the codebase calls it.\n\n", shown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallersShowsAtMostTheCountGivenAndThreeLinesAroundByDefault()
    {
        var shown = await ShowAsync("method Shop.Price callers", Shop);

        Assert.Equal(["# method Shop.Price(int), overload 1 of 2", "## callers", "### Shop.Price(int, int)", "# method Shop.Price(int, int), overload 2 of 2", "## callers"], Headings(shown));
        Assert.Contains("`Snippet.cs:14`\n\n```fake\n    public int Price(int amount, int discount) => Price(amount) - discount;\n```", shown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClassShortShowsTheTypesMemberSignaturesAndTheFieldsAndPropertiesTheMethodUses()
    {
        var shown = await ShowAsync("method Shop.Base class:short", Shop);

        Assert.Contains(
            """
            ### Shop

            `Snippet.cs:1`

            ```fake
            private readonly int tax
            private int unused
            public int Rate { get; set; }
            public int Total { get; }
            public int Price(int amount)
            public int Price(int amount, int discount)
            private int Base(int amount)
            private static int Round(int value)
            ```

            #### Fields and properties it uses

            `Snippet.cs:5`

            ```fake
                public int Rate { get; set; }
            ```
            """,
            shown,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClassShortShowsAComputedPropertyTheMethodReadsAndSaysWhenItUsesNothing()
    {
        var price = await ShowAsync("method Shop.Price class", Shop);
        var round = await ShowAsync("method Shop.Round class", Shop);

        Assert.Contains("`Snippet.cs:3`\n\n```fake\n    private readonly int tax;\n```\n\n`Snippet.cs:6`\n\n```fake\n    public int Total => tax + Rate;\n```", price, StringComparison.Ordinal);
        Assert.Contains("It uses none of Shop's fields or properties.", round, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMethodNothingCallsAndOneThatCallsNothingInTheCodebaseSaySo()
    {
        var ring = await ShowAsync("method Till.Ring callers", Shop);
        var round = await ShowAsync("method Shop.Round expand", Shop);

        Assert.EndsWith("## callers\n\nNo method in the codebase calls it.\n\n", ring, StringComparison.Ordinal);
        Assert.EndsWith("## expand\n\nIt calls no method in the codebase.\n\n", round, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATypeIsNamedByItsOwnNameEvenWhenItIsNested()
    {
        var shown = await ShowAsync("method Inner.Run", "class Outer { class Inner { void Run() { } } }");

        Assert.Equal(["# method Inner.Run(), overload 1 of 1"], Headings(shown));
    }

    [Theory]
    [InlineData("method Shop.Pricey", "no method Shop.Pricey in the codebase, at column 8")]
    [InlineData("method Till.Price", "no method Till.Price in the codebase, at column 8")]
    public async Task ANameThatMatchesNoMethodIsAnError(string text, string message)
    {
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => ShowAsync(text, Shop));

        Assert.StartsWith($"{message} of the lens", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheJsonHasASectionPerHeadingOfTheMarkdownWithTheSubjectOpen()
    {
        const string Text = "method Shop.Price callers:1 [expand:1 class:short] expand:1 class:short";
        var shown = await ShowAsync(Text, Shop);

        var sections = Flat(await JsonAsync(Text, Shop));

        Assert.Equal(Headings(shown).Select(heading => heading.TrimStart('#', ' ')), sections.Select(section => (string?)section!["title"]));
        Assert.Equal(sections.Count, sections.Select(section => (string?)section!["id"]).Distinct().Count());
        Assert.Equal(
            ["method Shop.Price(int), overload 1 of 2", "method Shop.Price(int, int), overload 2 of 2"],
            sections.Where(section => (bool)section!["open"]!).Select(section => (string?)section!["title"]));
        Assert.Equal("    public int Price(int amount, int discount) => Price(amount) - discount;", (string?)Code(Section(sections, "method Shop.Price(int, int), overload 2 of 2"))["code"]);
    }

    [Fact]
    public async Task ALineThatCallsAMethodALensCanNameIsAMarkWhoseQueryMakesItTheSubject()
    {
        var sections = (await JsonAsync("method Shop.Price:1", Shop))["sections"]!.AsArray();

        var code = Code(Assert.Single(sections)!);
        var mark = Assert.Single(code["marks"]!.AsArray())!;

        Assert.Equal(8, (int)code["startLine"]!);
        Assert.Equal(((10, 9), (10, 32)), (Position(mark["start"]!), Position(mark["end"]!)));
        Assert.Equal(("call", 0), ((string?)mark["label"], (int)mark["weight"]!));
        Assert.Equal([("Shop.Base", "method Shop.Base class:short")], Queries(mark));
    }

    [Fact]
    public async Task ALineWithSeveralCallsCarriesAQueryForEachAndAPublicMethodsQueryAddsItsCallers()
    {
        const string Sum = """
            class Sum
            {
                public int Both(int a) => One(a) + Two(a) + One(a + 1);
                private int One(int a) => a;
                public int Two(int a) => a;
            }
            """;

        var sections = (await JsonAsync("method Sum.Both", Sum))["sections"]!.AsArray();
        var mark = Assert.Single(Code(Assert.Single(sections)!)["marks"]!.AsArray())!;

        Assert.Equal([("Sum.One", "method Sum.One class:short"), ("Sum.Two", "method Sum.Two class:short callers:3")], Queries(mark));
    }

    [Fact]
    public async Task TheCallingLineOfACallersExcerptOffersTheCallerFirstThenWhatItCalls()
    {
        var sections = Flat(await JsonAsync("method Shop.Price:1 callers:2", Shop));

        var ring = Assert.Single(Code(Section(sections, "Till.Ring(int)"))["marks"]!.AsArray())!;
        var price = Assert.Single(Code(Section(sections, "Shop.Price(int, int)"))["marks"]!.AsArray())!;

        Assert.Equal((29, "caller"), ((int)ring["start"]!["line"]!, (string?)ring["label"]));
        Assert.Equal([("Till.Ring", "method Till.Ring class:short callers:3"), ("Shop.Price:1", "method Shop.Price:1 class:short callers:3")], Queries(ring));
        Assert.Equal([("Shop.Price:2", "method Shop.Price:2 class:short callers:3"), ("Shop.Price:1", "method Shop.Price:1 class:short callers:3")], Queries(price));
    }

    [Fact]
    public async Task CodeShownAboveIsItsFirstLineMarkedWithTheSectionThatShowsItAndNoQuery()
    {
        var sections = Flat(await JsonAsync("method Shop.Base class:short expand:1 [class:short]", Shop));

        var shops = sections.Where(section => (string?)section!["title"] == "Shop").ToList();
        var above = Code(shops[1]!);
        var mark = Assert.Single(above["marks"]!.AsArray())!;

        Assert.Equal(2, shops.Count);
        Assert.Equal(("private readonly int tax", 1), ((string?)above["code"], (int)above["startLine"]!));
        Assert.Equal((string?)shops[0]!["id"], (string?)mark["section"]);
        Assert.Equal(("shown above", "Shown above at Snippet.cs:1."), ((string?)mark["label"], (string?)mark["note"]));
        Assert.Null(mark["queries"]);
    }

    [Fact]
    public async Task EachSectionHoldsTheSectionsUnderItAViewItsResultsAndAResultItsOwnViews()
    {
        var page = await JsonAsync("method Shop.Base expand:1 [class:short]", Shop);

        var subject = Assert.Single(page["sections"]!.AsArray())!;
        var view = Assert.Single(subject["sections"]!.AsArray())!;
        var round = Assert.Single(view["sections"]!.AsArray())!;
        var outline = Assert.Single(round["sections"]!.AsArray())!;
        var shop = Assert.Single(outline["sections"]!.AsArray())!;
        var used = Assert.Single(shop["sections"]!.AsArray())!;

        Assert.Equal(
            ["method Shop.Base(int), overload 1 of 1", "expand:1", "Shop.Round(int)", "class:short", "Shop", "Fields and properties it uses"],
            new[] { subject, view, round, outline, shop, used }.Select(section => (string?)section["title"]));
        Assert.Null(used["sections"]);
        Assert.Equal(["id", "title", "open", "blocks", "sections"], subject.AsObject().Select(pair => pair.Key));
    }

    // Every section of the page, each before the sections under it, in the order the markdown writes their headings.
    private static JsonArray Flat(JsonNode page)
    {
        var flat = new JsonArray();
        Add(page["sections"]!.AsArray());
        return flat;

        void Add(JsonArray sections)
        {
            foreach (var section in sections)
            {
                flat.Add(section!.DeepClone());
                if (section["sections"] is JsonArray under)
                {
                    Add(under);
                }
            }
        }
    }

    private static async Task<JsonNode> JsonAsync(string text, string code)
    {
        var provider = new FakeProvider();
        provider.Codebases["a.sln"] = await CSharpSnippet.FactsAsync(code);
        var page = await Lens.Parse(text).PageAsync(new Code(provider, Root, ["a.sln"]), TestContext.Current.CancellationToken);
        return page.Json("abc");
    }

    private static JsonNode Section(JsonArray sections, string title) => sections.Single(section => (string?)section!["title"] == title)!;

    // The section's one code block.
    private static JsonNode Code(JsonNode section) => section["blocks"]!.AsArray().Single(block => (string?)block!["type"] == "code")!;

    private static (int Line, int Col) Position(JsonNode position) => ((int)position["line"]!, (int)position["col"]!);

    private static List<(string?, string?)> Queries(JsonNode mark) =>
        [.. mark["queries"]!.AsArray().Select(query => ((string?)query!["label"], (string?)query["query"]))];

    private static async Task<string> ShowAsync(string text, string code)
    {
        var provider = new FakeProvider();
        provider.Codebases["a.sln"] = await CSharpSnippet.FactsAsync(code);
        return await Lens.Parse(text).ShowAsync(new Code(provider, Root, ["a.sln"]), TestContext.Current.CancellationToken);
    }

    private static int Count(string text, string part) => text.Split(part).Length - 1;

    private static List<string> Headings(string markdown) =>
        [.. markdown.Split('\n').Where(line => line.StartsWith('#'))];
}
