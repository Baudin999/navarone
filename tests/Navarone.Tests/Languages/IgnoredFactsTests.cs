using Navarone.Engine.Configuration;
using Navarone.Tests.Support;

namespace Navarone.Tests.Languages;

public sealed class IgnoredFactsTests
{
    private const string Shop = "class Shop { int total; public int Price(int count) => count * total; }";
    private const string ShopTests = "class ShopTests { public void PricesOne() { } }";

    [Fact]
    public async Task AnIgnoredProjectYieldsNoFacts()
    {
        var ignores = new Ignores("/snippet", [], ["*.Tests"]);

        var facts = await CSharpSnippet.FactsAsync(ignores, ("Shop", "Shop.cs", Shop), ("Shop.Tests", "ShopTests.cs", ShopTests));

        Assert.Equal(["Shop"], facts.Types.Select(type => type.Name));
    }

    [Fact]
    public async Task AnIgnoredFileYieldsNoFacts()
    {
        var ignores = new Ignores("/snippet", ["**/Generated/*.cs"], []);

        var facts = await CSharpSnippet.FactsAsync(ignores, ("Shop", "Shop.cs", Shop), ("Shop", "Generated/Order.cs", "class Order { void Pay() { } }"));

        Assert.Equal(["Shop"], facts.Types.Select(type => type.Name));
    }

    [Fact]
    public async Task AFieldDeclaredInAnIgnoredFileIsNoFieldOfItsType()
    {
        var ignores = new Ignores("/snippet", ["Shop/Shop.g.cs"], []);

        var facts = await CSharpSnippet.FactsAsync(
            ignores,
            ("Shop", "Shop.cs", "partial class Shop { int total; int Price() => total; }"),
            ("Shop", "Shop.g.cs", "partial class Shop { int generated; int Made() => generated; }"));

        var shop = Assert.Single(facts.Types);
        Assert.Equal(["total"], shop.Fields.Select(field => field.Name));
        Assert.Equal(["Price()"], shop.Methods.Select(method => method.Name));
    }

    [Fact]
    public async Task NothingIgnoredReadsEveryProject()
    {
        var facts = await CSharpSnippet.FactsAsync(Ignores.Nothing("/snippet"), ("Shop", "Shop.cs", Shop), ("Shop.Tests", "ShopTests.cs", ShopTests));

        Assert.Equal(["Shop", "ShopTests"], facts.Types.Select(type => type.Name).Order(StringComparer.Ordinal));
    }
}
