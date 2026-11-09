using Navarone.Engine.Facts;
using Navarone.Engine.Reasons;
using Navarone.Tests.Support;

namespace Navarone.Tests.Languages;

public sealed class FactExtractorTests
{
    [Theory]
    [InlineData("class C { Missing m; }")]
    [InlineData("using Not.There; class C { }")]
    [InlineData("class C { int M() => System.Gone.Value; }")]
    [InlineData("class C { void M() { var x = new Absent(); } }")]
    public async Task CodeThatMissesATypeOrNamespaceItReferencesGivesIncompleteFacts(string code)
    {
        Assert.False((await CSharpSnippet.FactsAsync(code)).Complete);
    }

    [Theory]
    [InlineData("class C { int m; int M() => m; }")]
    [InlineData("class C { int M() { return \"no int\"; } }")]
    public async Task CodeWhoseReferencesAllResolveGivesCompleteFactsEvenWithOtherErrors(string code)
    {
        Assert.True((await CSharpSnippet.FactsAsync(code)).Complete);
    }

    [Fact]
    public async Task APropertyOrIndexerGetterOrSetterIsAnAccessorAndAnythingElseIsNot()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                int a;
                int A { get => a; set => a = value; }
                int B => a;
                int this[int i] { get { return a + i; } }
                event System.Action E { add { } remove { } }
                C() { a = 1; }
                int M() => a;
            }
            """);

        var accessors = facts.Types.Single(type => type.Symbol.Value == "Snippet::C").Methods.Where(method => method.IsAccessor).Select(method => method.Name);
        Assert.Equal(["A.get", "A.set", "B.get", "this[].get"], accessors.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AMethodOnlyItsOwnTypeCanCallIsPrivate()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                void Unmarked() { }
                private void Marked() { }
                internal void Inside() { }
                protected void Derived() { }
                private protected void Narrow() { }
                public void Open() { }
            }
            """);

        var privates = facts.Types.Single(type => type.Symbol.Value == "Snippet::C").Methods.Where(method => method.IsPrivate).Select(method => method.Name);
        Assert.Equal(["Marked()", "Unmarked()"], privates.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AMethodOrFieldAnyCodeMayUseIsPublicAndAnAutoPropertyIsAProperty()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                private int hidden;
                public int shown;
                public int Auto { get; set; }
                internal int Inner { get; }
                void Unmarked() { }
                internal void Inside() { }
                public void Open() { }
                public int Total => hidden;
            }
            """);

        var type = facts.Types.Single(type => type.Symbol.Value == "Snippet::C");
        Assert.Equal(["Open()", "Total.get"], type.Methods.Where(method => method.IsPublic).Select(method => method.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["Auto", "shown"], type.Fields.Where(field => field.IsPublic).Select(field => field.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["Auto", "Inner"], type.Fields.Where(field => field.IsProperty).Select(field => field.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task APropertyOrIndexerWithABodyHoldsItsAccessorsWithABodyAndAnAutoPropertyIsNone()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                int a;
                public int Auto { get; set; }
                public int Total => a;
                private int Mixed { get { return a; } init { a = value; } }
                public int Half { get; private set { a = value; } }
                int this[int i] { get { return a + i; } set { a = value; } }
                event System.Action E { add { } remove { } }
            }
            """);

        var type = facts.Types.Single(type => type.Name == "C");
        var properties = type.Properties.Select(property => (property.Name, property.IsPublic, property.Written.Line, property.Written.EndLine, string.Join(' ', property.Accessors.Select(accessor => $"{accessor.Side}@{accessor.Written.Line}"))));
        Assert.Equal([("Total", true, 5, 5, "get@5"), ("Mixed", false, 6, 6, "get@6 init@6"), ("Half", true, 7, 7, "set@7"), ("this[]", false, 8, 8, "get@8 set@8")], properties);
        var methods = type.Methods.Select(method => method.Symbol).ToHashSet();
        Assert.All(type.Properties.SelectMany(property => property.Accessors), accessor => Assert.Contains(accessor.Method, methods));
    }

    [Fact]
    public async Task ATypeNamesItsNamespaceInFullAndItsAssembly()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            namespace Shop.Orders
            {
                class Till { }
            }

            class Loose { }
            """);

        var till = facts.Types.Single(type => type.Name == "Till");
        Assert.Equal("Shop.Orders", till.Namespace);
        Assert.Equal("Snippet", till.Assembly);
        Assert.Equal(string.Empty, facts.Types.Single(type => type.Name == "Loose").Namespace);
    }

    [Fact]
    public async Task AMethodsContractsAreTheTypesThatDeclareWhatItOverridesOrImplements()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            interface I { int Id { get; } void Run(); void Stop(); }
            abstract class B { public abstract void Go(); public virtual string Name() => ""; }
            class C : B, I
            {
                public int Id => 1;
                public void Run() { }
                void I.Stop() { }
                public override void Go() { }
                public override string ToString() => "";
                public void Own() { }
            }
            """);

        var contracts = facts.Types.Single(type => type.Symbol.Value == "Snippet::C").Methods
            .Where(method => method.Contracts.Count > 0)
            .Select(method => $"{method.Name}: {string.Join(", ", method.Contracts.Select(contract => contract.Value.Split("::")[1]))}");
        Assert.Equal(
            ["Go(): B", "I.Stop(): I", "Id.get: I", "Run(): I", "ToString(): object"],
            contracts.Order(StringComparer.Ordinal));
        Assert.Empty(facts.Method("B.Name()").Contracts);
    }

    [Fact]
    public async Task AMethodWithoutABodyIsAMethodUntilADeclarationWithABodyIsRead()
    {
        var facts = await CSharpSnippet.FactsAsync(
            ("A.cs", "partial class C { partial void Done(int a); partial int Both(int a); }"),
            ("B.cs", "partial class C { partial int Both(int a) { if (a > 0) { return 1; } return 0; } }"));

        Assert.Empty(facts.Method("C.Done(int)").ControlFlow);
        Assert.Single(facts.Method("C.Both(int)").ControlFlow);
    }

    [Fact]
    public async Task APrimaryConstructorSetsARecordsPropertiesOrAClasssParameters()
    {
        var facts = await CSharpSnippet.FactsAsync("record R(int A, string B); class C(int a) { int M() => a; }");

        var record = facts.Types.Single(type => type.Symbol.Value == "Snippet::R").Methods.Single(method => method.IsConstructor);
        var type = facts.Types.Single(type => type.Symbol.Value == "Snippet::C").Methods.Single(method => method.IsConstructor);
        Assert.Equal(["Snippet::R.A", "Snippet::R.B"], record.FieldUses.Select(field => field.Value));
        Assert.Equal(["Snippet::C.C(int)#a"], type.FieldUses.Select(field => field.Value));
    }

    [Fact]
    public async Task ANestedTypeNamesTheTypeItIsDeclaredIn()
    {
        var facts = await CSharpSnippet.FactsAsync("class C { void M() { } class Inner { void N() { } class Deeper { void O() { } } } }");

        Assert.Null(facts.Types.Single(type => type.Symbol.Value == "Snippet::C").ContainingType);
        Assert.Equal(new SymbolId("Snippet::C"), facts.Types.Single(type => type.Symbol.Value == "Snippet::C.Inner").ContainingType);
        Assert.Equal(new SymbolId("Snippet::C.Inner"), facts.Types.Single(type => type.Symbol.Value == "Snippet::C.Inner.Deeper").ContainingType);
    }

    [Fact]
    public async Task NamesEachMethodByAssemblyTypeAndParameterTypes()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            namespace N;
            class C
            {
                void M(int a) { }
                void M(string s, System.Collections.Generic.List<int> l) { }
                class Inner { void M() { } }
            }
            """);

        Assert.Equal(
            ["Snippet::N.C.Inner.M()", "Snippet::N.C.M(int)", "Snippet::N.C.M(string, List<int>)"],
            facts.Types.SelectMany(type => type.Methods).Select(method => method.Symbol.Value).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task LocatesAMethodAtItsName()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                [System.Obsolete]
                void M() { }
            }
            """);

        Assert.Equal(new SourceLocation("Snippet.cs", 4, 10), facts.Method("C.M()").Location);
    }

    [Fact]
    public async Task WrittenCodeIsTheWholeLinesFromTheFirstAttributeWithItsCommentsAndAnAccessorsFromItsProperty()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            // navarone: 0000001
            class C
            {
                // navarone: 0000002
                [System.Obsolete]
                // navarone: 0000003
                void M()
                {
                    if (P > 0) { }
                }

                int P
                {
                    get { return 1; }
                }
            }
            """);

        Assert.Equal(2, Assert.Single(facts.Types).Written.Line);
        var method = facts.Method("C.M()");
        Assert.Equal(new Written(5, "    [System.Obsolete]\n    // navarone: 0000003\n    void M()\n    {\n        if (P > 0) { }\n    }"), method.Written);
        Assert.Equal(10, method.Written.EndLine);
        Assert.Equal(9, Assert.Single(method.ControlFlow).Span.StartLine);
        Assert.Equal(new Written(12, "    int P\n    {\n        get { return 1; }"), facts.Method("C.P.get").Written);
    }

    [Fact]
    public async Task ReadsTheNavaroneIdWrittenAboveAMethodOrItsProperty()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                // navarone: 0001234
                // a note
                void M() { }
                void Other() { }
                // navarone: 0000007
                int P { get { return 1; } }
            }
            """);

        Assert.Equal(["0001234"], facts.Method("C.M()").NavaroneIds);
        Assert.Empty(facts.Method("C.Other()").NavaroneIds);
        Assert.Equal(["0000007"], facts.Method("C.P.get").NavaroneIds);
    }

    [Fact]
    public async Task GathersAPartialClassFromEveryFileIntoOneType()
    {
        var facts = await CSharpSnippet.FactsAsync(
            ("A.cs", "partial class C { void A() { } }"),
            ("B.cs", "partial class C { void B() { } }"));

        var type = Assert.Single(facts.Types);
        Assert.Equal(2, type.Methods.Count);
    }

    [Fact]
    public async Task SkipsOnlyWhatTheBuildGeneratesUnderObj()
    {
        var facts = await CSharpSnippet.FactsAsync(
            ("obj/Debug/A.cs", "class Built { void M() { } }"),
            ("C.g.cs", "class NamedGenerated { void M() { } }"),
            ("C.designer.cs", "class NamedDesigner { void M() { } }"),
            ("D.cs", "// <auto-generated/>\nclass MarkedGenerated { void M() { } }"));

        Assert.Equal(
            ["Snippet::MarkedGenerated", "Snippet::NamedDesigner", "Snippet::NamedGenerated"],
            facts.Types.Select(type => type.Symbol.Value).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AnInitializerThatHoldsLogicIsMeasuredUnderItsMember()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            using System;
            class C
            {
                // navarone: 0000003
                private static readonly Func<int, int> Pick = x => x > 0 ? Helper(x) : 0;
                public bool Flag { get; } = DateTime.Now.Second > 3 || Helper(1) > 0;
                private readonly int plain = Helper(2), other = 3;
                private event Action Changed = () => { };
                static int Helper(int x) => x;
            }
            """);

        var pick = facts.Method("C.Pick");
        Assert.Equal("Pick", pick.Name);
        Assert.Equal(new SourceLocation("Snippet.cs", 5, 44), pick.Location);
        Assert.True(pick.IsStatic);
        Assert.False(pick.IsAccessor);
        Assert.Equal([ControlFlowKind.Conditional], pick.ControlFlow.Select(fact => fact.Kind));
        Assert.Equal(["Snippet::C.Helper(int)"], pick.Calls.Select(call => call.Value));
        Assert.Empty(pick.FieldUses);
        Assert.Equal(["0000003"], pick.NavaroneIds);
        Assert.DoesNotContain("navarone", pick.Code, StringComparison.Ordinal);
        Assert.False(facts.Method("C.Flag").IsAccessor);
        Assert.Equal("Snippet::C.Changed", facts.Method("C.Changed").Symbol.Value);
        Assert.DoesNotContain(facts.Types.Single().Methods, method => method.Name is "plain" or "other");
    }

    [Fact]
    public async Task AReadOfAPrimaryConstructorParameterAMemberCapturesIsAFieldUse()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C(int seed, int start)
            {
                private readonly System.Func<int> next = () => start > 0 ? start : 1;
                int Next() => seed + 1;
            }
            record R(int Size) { int Twice() => Size * 2; }
            """);

        var type = facts.Types.Single(type => type.Symbol.Value == "Snippet::C");
        Assert.Contains(type.Fields, field => field.Name == "seed" && !field.IsStatic);
        Assert.Equal(type.Fields.Single(field => field.Name == "seed").Symbol, Assert.Single(facts.Method("C.Next()").FieldUses));
        Assert.Empty(facts.Method("C.next").FieldUses);
        Assert.DoesNotContain(facts.Types.Single(type => type.Symbol.Value == "Snippet::R").Fields, field => field.Symbol.Value.Contains('#', StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadsTheNavaroneIdWrittenBetweenTheAttributesAndTheSignature()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                [System.Obsolete]
                // navarone: 0001234
                void M() { }
                [System.Obsolete] // navarone: 0000005
                [System.CLSCompliant(false)]
                int P { get { return 1; } }
                [System.Obsolete]
                void Other() { // navarone: 0000006
                }
            }
            """);

        Assert.Equal(["0001234"], facts.Method("C.M()").NavaroneIds);
        Assert.Equal(["0000005"], facts.Method("C.P.get").NavaroneIds);
        Assert.Empty(facts.Method("C.Other()").NavaroneIds);
    }

    [Fact]
    public async Task EveryReadOfCallsReturnsTheSameList()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                void M() { Helper(); Helper(); }
                void Helper() { }
            }
            """);

        var method = facts.Method("C.M()");
        Assert.Same(method.Calls, method.Calls);
    }

    [Fact]
    public async Task ACopyWithOtherCallSitesCallsWhatThoseSitesCall()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                void M() { Helper(); }
                void Helper() { }
            }
            """);

        var other = new SymbolId("Other");
        var copy = facts.Method("C.M()") with { CallSites = [new CallSite(other, 1)] };

        Assert.Equal([other], copy.Calls);
    }

    [Fact]
    public async Task ResolvesCallsAndFieldUsesToTheirDeclarations()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                private int count;
                int Total { get; set; }
                void M() { count++; Total = Helper(count); }
                int Helper(int x) => x;
            }
            """);

        var method = facts.Method("C.M()");
        Assert.Equal(["Snippet::C.Helper(int)"], method.Calls.Select(call => call.Value));
        Assert.Equal(["Snippet::C.Total", "Snippet::C.count"], method.FieldUses.Select(use => use.Value).Order(StringComparer.Ordinal));
        Assert.Equal(["Snippet::C.Total", "Snippet::C.count"], facts.Types.Single().Fields.Select(field => field.Symbol.Value).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task EveryUseTheCompilerBindsIsACallWhateverSyntaxWritesIt()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            using System.Collections;
            class V
            {
                public static V operator +(V a, V b) => a;
                public static V operator -(V a) => a;
                public static implicit operator int(V v) => 0;
                public int this[int i] { get { return i; } set { } }
                public void Deconstruct(out int a, out int b) { a = 0; b = 0; }
                public Walk GetEnumerator() => new Walk();
            }
            class Walk
            {
                public bool MoveNext() => false;
                public int Current => 0;
            }
            class Bag : IEnumerable
            {
                public void Add(int item) { }
                IEnumerator IEnumerable.GetEnumerator() => null!;
            }
            class C
            {
                void M(V v)
                {
                    var sum = v + -v;
                    int n = v;
                    v[0] = v[1];
                    var (a, b) = v;
                    foreach (var item in v) { }
                    var bag = new Bag { 1 };
                    System.Console.WriteLine(n);
                }
            }
            """);

        Assert.Equal(
            [
                "Snippet::Bag.Add(int)",
                "Snippet::Bag.Bag()",
                "Snippet::V.Deconstruct(out int, out int)",
                "Snippet::V.GetEnumerator()",
                "Snippet::V.implicit operator int(V)",
                "Snippet::V.operator +(V, V)",
                "Snippet::V.operator -(V)",
                "Snippet::V.this[int].get",
                "Snippet::V.this[int].set",
                "Snippet::Walk.Current.get",
                "Snippet::Walk.MoveNext()",
                "System.Console::System.Console.WriteLine(int)",
                "System.Private.CoreLib::System.IDisposable.Dispose()",
            ],
            facts.Method("C.M(V)").Calls.Select(call => call.Value).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task APatternACustomEventAndEachDeconstructionCallWhatTheCompilerBinds()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class P
            {
                public void Deconstruct(out int a, out int b) { a = 0; b = 0; }
            }
            class Q
            {
                public void Deconstruct(out P p, out int t) { p = new P(); t = 0; }
            }
            class C
            {
                event System.Action E { add { } remove { } }
                void Events() { E += Events; E -= Events; }
                bool Pattern(P p) => p is (_, 1);
                void Loop(P[] ps) { foreach (var (x, y) in ps) { } }
                void Nested(Q q) { var ((a, b), t) = q; }
            }
            """);

        Assert.Equal(["Snippet::C.E.add", "Snippet::C.E.remove", "Snippet::C.Events()"], OwnCalls(facts, "C.Events()"));
        Assert.Equal(["Snippet::P.Deconstruct(out int, out int)"], OwnCalls(facts, "C.Pattern(P)"));
        Assert.Equal(["Snippet::P.Deconstruct(out int, out int)"], OwnCalls(facts, "C.Loop(P[])"));
        Assert.Equal(["Snippet::P.Deconstruct(out int, out int)", "Snippet::Q.Deconstruct(out P, out int)"], OwnCalls(facts, "C.Nested(Q)"));
    }

    [Fact]
    public async Task AForeachReadsAnAutoPropertyCurrentAsAField()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            struct Walk
            {
                public bool MoveNext() => false;
                public int Current { get; private set; }
            }
            class V
            {
                public Walk GetEnumerator() => default;
            }
            class C
            {
                void M(V v) { foreach (var item in v) { } }
            }
            """);

        var method = facts.Method("C.M(V)");
        Assert.Equal(["Snippet::V.GetEnumerator()", "Snippet::Walk.MoveNext()"], OwnCalls(facts, "C.M(V)"));
        Assert.Equal(["Snippet::Walk.Current"], method.FieldUses.Select(use => use.Value));
    }

    [Fact]
    public async Task ANameofUsesNothing()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                private int count;
                string M() => nameof(Helper) + nameof(count) + nameof(C.Total);
                int Total => count;
                void Helper() { }
            }
            """);

        var method = facts.Method("C.M()");
        Assert.Empty(method.Calls);
        Assert.Empty(method.FieldUses);
    }

    [Fact]
    public async Task TheFieldKeywordInAnAccessorUsesItsPropertyAsAField()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                int P { get => field; set => field = value; }
            }
            """);

        Assert.Equal(["Snippet::C.P"], facts.Types.Single().Fields.Select(field => field.Symbol.Value));
        Assert.Equal(["Snippet::C.P"], facts.Method("C.P.get").FieldUses.Select(use => use.Value));
        Assert.Equal(["Snippet::C.P"], facts.Method("C.P.set").FieldUses.Select(use => use.Value));
    }

    [Fact]
    public async Task EachCallSiteCarriesTheLineItIsWrittenOnInTheOrderWritten()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                void M()
                {
                    Helper(1);
                    Helper(2); Helper(3);
                    Other();
                    Helper(4);
                }
                int Helper(int x) => x;
                void Other() { }
            }
            """);

        var method = facts.Method("C.M()");
        Assert.Equal(
            [("Snippet::C.Helper(int)", 5), ("Snippet::C.Helper(int)", 6), ("Snippet::C.Other()", 7), ("Snippet::C.Helper(int)", 8)],
            method.CallSites.Select(site => (site.Callee.Value, site.Line)));
        Assert.Equal(["Snippet::C.Helper(int)", "Snippet::C.Other()"], method.Calls.Select(call => call.Value));
    }

    [Fact]
    public async Task EachTypeCarriesItsOwnNameAndEachMemberItsSignature()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class Outer
            {
                class C(int seed)
                {
                    private static readonly string[] names = [];
                    public int Count { get; private set; }
                    public int Twice => Count * 2;
                    public C() : this(0) { }
                    internal static string? Find(int at, params int[] rest) => names[at];
                    int Seeded() => seed;
                }
            }
            """);

        var type = facts.Types.Single(type => type.Symbol.Value == "Snippet::Outer.C");
        Assert.Equal("C", type.Name);
        Assert.Equal(
            ["private static readonly string[] names", "public int Count { get; private set; }", "int seed"],
            type.Fields.Select(field => field.Signature));
        Assert.Equal(
            ["public int Twice { get; }", "public C()", "internal static string? Find(int at, params int[] rest)", "private int Seeded()", "public C(int seed)"],
            type.Methods.Select(method => method.Signature));
    }

    [Fact]
    public async Task EachFieldCarriesItsDeclarationAsWritten()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            record R(int Id)
            {
                // the count
                private int count, other;
                public string Name { get; } =
                    "none";
            }
            """);

        var fields = facts.Types.Single().Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
        Assert.Equal(new Written(4, "    private int count, other;"), fields["count"].Written);
        Assert.Equal(new SourceLocation("Snippet.cs", 4, 17), fields["count"].Location);
        Assert.Equal(new Written(4, "    private int count, other;"), fields["other"].Written);
        Assert.Equal(new Written(5, "    public string Name { get; } =\n        \"none\";"), fields["Name"].Written);
        Assert.Equal(new Written(1, "record R(int Id)"), fields["Id"].Written);
    }

    [Theory]
    [InlineData("P += 1;", new[] { "P.get", "P.set" })]
    [InlineData("P ??= null;", new[] { "P.get", "P.set" })]
    [InlineData("P++;", new[] { "P.get", "P.set" })]
    [InlineData("--P;", new[] { "P.get", "P.set" })]
    [InlineData("this.P = null;", new[] { "P.set" })]
    [InlineData("P = null;", new[] { "P.set" })]
    [InlineData("(P, f) = (null, null);", new[] { "P.set" })]
    [InlineData("var x = P!;", new[] { "P.get" })]
    [InlineData("var x = P;", new[] { "P.get" })]
    public async Task AUseOfAPropertyWithABodyCallsTheAccessorsItUses(string statement, string[] accessors)
    {
        var facts = await CSharpSnippet.FactsAsync($$"""
            class C
            {
                string? f;
                string? P { get { return f; } set { f = value; } }
                void M() { {{statement}} }
            }
            """);

        Assert.Equal(accessors.Select(accessor => $"Snippet::C.{accessor}"), facts.Method("C.M()").Calls.Select(call => call.Value).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task TheFingerprintChangesWithTheCodeAndARenameButNotWithTheNavaroneComment()
    {
        var original = await FingerprintAsync("class C { int M(int a) { return a; } }", "C.M(int)");
        var changed = await FingerprintAsync("class C { int M(int a) { return a + 1; } }", "C.M(int)");
        var renamed = await FingerprintAsync("class C { int N(int a) { return a; } }", "C.N(int)");
        var classRenamed = await FingerprintAsync("class D { int M(int a) { return a; } }", "D.M(int)");
        var commented = await FingerprintAsync("class C {\n// navarone: 0000001\nint M(int a) { return a; } }", "C.M(int)");

        Assert.NotEqual(original, changed);
        Assert.NotEqual(original, renamed);
        Assert.NotEqual(original, classRenamed);
        Assert.Equal(original, commented);
    }

    [Fact]
    public async Task AnIdAfterTheAttributesLeavesTheFingerprintAlone()
    {
        var original = await FingerprintAsync("class C {\n[System.Obsolete]\nint M(int a) { return a; } }", "C.M(int)");
        var commented = await FingerprintAsync("class C {\n[System.Obsolete]\n// navarone: 0000001\nint M(int a) { return a; } }", "C.M(int)");
        var accessor = await FingerprintAsync("class C {\n[System.Obsolete]\nint P { get { return 1; } } }", "C.P.get");
        var accessorCommented = await FingerprintAsync("class C {\n[System.Obsolete]\n// navarone: 0000001\nint P { get { return 1; } } }", "C.P.get");

        Assert.Equal(original, commented);
        Assert.Equal(accessor, accessorCommented);
    }

    [Fact]
    public async Task TheFingerprintOfAnAccessorChangesWithItsPropertysType()
    {
        var original = await FingerprintAsync("class C { int P { get { return 1; } } }", "C.P.get");
        var retyped = await FingerprintAsync("class C { long P { get { return 1; } } }", "C.P.get");
        var otherAccessor = await FingerprintAsync("class C { int P { get { return 1; } set { } } }", "C.P.get");

        Assert.NotEqual(original, retyped);
        Assert.Equal(original, otherAccessor);
    }

    [Fact]
    public async Task AMethodsTokensAreItsBodysTokensWithEveryIdentifierAndLiteralNormalisedAndNoComments()
    {
        var facts = await CSharpSnippet.FactsAsync("""
            class C
            {
                string M(int count)
                {
                    // a comment is no token
                    var name = $"n{count}";
                    return count > 2 ? name : null ?? "x" + true;
                }
            }
            """);

        var tokens = facts.Method("C.M(int)").Tokens;

        Assert.Equal(
            "{ id id = $\" lit { id } \" ; return id > lit ? id : null ?? lit + lit ; }",
            string.Join(' ', tokens.Select(token => token.Shape)));
        Assert.Equal(["{", "var", "name"], tokens.Take(3).Select(token => token.Text));
        Assert.Equal([4, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 7], tokens.Take(12).Select(token => token.Line));
    }

    // The calls of the method into the snippet's own code, in order.
    private static IEnumerable<string> OwnCalls(CodebaseFacts facts, string symbolEnd) =>
        facts.Method(symbolEnd).Calls.Select(call => call.Value).Where(call => call.StartsWith("Snippet::", StringComparison.Ordinal)).Order(StringComparer.Ordinal);

    private static async Task<CodeFingerprint> FingerprintAsync(string code, string symbolEnd)
    {
        var method = (await CSharpSnippet.FactsAsync(code)).Method(symbolEnd);
        return CodeFingerprint.Of(method.Symbol, method.Code);
    }
}
