using Navarone.Engine;
using Navarone.Engine.Configuration;
using Navarone.Engine.Facts;
using Navarone.Tests.Support;

namespace Navarone.Tests.Engine;

/// <summary>
/// The facts cache in .navarone at the repo root. Each test reads the facts twice and changes what the provider hands
/// over in between, so facts that did not change came from the cache and facts that changed were loaded again.
/// </summary>
public sealed class FactsCacheTests : IDisposable
{
    // Every kind of control flow the provider reads, so the round trip carries each, with the parts of a pattern and of
    // a condition, which a metric tells apart by reference.
    private const string Source = """
        using System;
        using System.Linq;

        class Shop
        {
            private int tax;
            public int Rate { get; set; }
            public int Total => tax + Rate;

            public int Price(int amount, bool round)
            {
                if (amount > 0 && round || amount < -10)
                {
                    tax++;
                }
                else if (amount == 0)
                {
                    return 0;
                }
                else
                {
                    tax--;
                }

                for (var at = 0; at < amount; at++)
                {
                    try { tax += Base(at) ?? 0; }
                    catch (InvalidOperationException) when (round) { }
                }

                var kind = amount switch
                {
                    > 100 and < 200 => 1,
                    < 0 => 2,
                    _ => 3,
                };

                return round ? kind : Enumerable.Range(0, amount).Count(item => item > tax);
            }

            private int? Base(int amount) => amount is { } value and not 0 ? value : null;
        }
        """;

    private readonly TempRepo repo = new();
    private readonly FakeProvider provider = new();

    public void Dispose() => repo.Dispose();

    [Fact]
    public async Task FactsReadFromTheCacheAreTheFactsAFreshLoadBuilds()
    {
        await CommittedAsync();
        var fresh = await CSharpSnippet.FactsAsync(Source);
        provider.Codebases["a.sln"] = fresh;
        await FactsAsync();
        provider.Codebases["a.sln"] = new CodebaseFacts([], Complete: true);

        var cached = await FactsAsync();

        Assert.True(fresh.Complete);
        Assert.NotSame(fresh, cached);
        Assert.Equivalent(fresh, cached, strict: true);
        foreach (var method in cached.Types.SelectMany(type => type.Methods))
        {
            AssertEachWholeComesBefore(method.ControlFlow);
            Assert.Equal(method.CallSites.Select(site => site.Callee).Distinct(), method.Calls);
        }
    }

    [Fact]
    public async Task FactsTheProviderCouldNotLoadCompletelyAreNeverKeptSoTheNextCommandLoadsAgain()
    {
        await CommittedAsync();
        var whole = Path.Combine(repo.Root, "b.sln");
        provider.Codebases[whole] = FakeFacts.With("B", ("Whole", 1, "whole"));
        provider.Codebases["a.sln"] = FakeFacts.With(("Degraded", 1, "degraded")) with { Complete = false };
        var code = new Code(provider, repo.Root, ["a.sln", whole]);

        var degraded = await code.FactsAsync(TestContext.Current.CancellationToken);
        provider.Codebases["a.sln"] = FakeFacts.With(("Restored", 1, "restored"));
        var restored = await code.FactsAsync(TestContext.Current.CancellationToken);
        provider.Codebases["a.sln"] = FakeFacts.With(("Other", 1, "other"));

        Assert.False(degraded.Complete);
        Assert.Equal(["Degraded", "Whole"], degraded.Types.SelectMany(type => type.Methods).Select(method => method.Name));
        Assert.Equal(["Restored", "Whole"], restored.Types.SelectMany(type => type.Methods).Select(method => method.Name));
        Assert.Equal(["Restored", "Whole"], (await code.FactsAsync(TestContext.Current.CancellationToken)).Types.SelectMany(type => type.Methods).Select(method => method.Name));
        Assert.Single(Entries());
    }

    [Fact]
    public async Task OutsideAGitRepositoryNothingIsCached()
    {
        provider.Codebases["a.sln"] = FakeFacts.With(("First", 1, "first"));
        await FactsAsync();
        var second = FakeFacts.With(("Second", 1, "second"));
        provider.Codebases["a.sln"] = second;

        Assert.Same(second.Types[0], (await FactsAsync()).Types[0]);
        Assert.False(Directory.Exists(Folder));
    }

    [Fact]
    public async Task ARepositoryWithNoCommitYetCachesNothing()
    {
        repo.Git("init", "-q", "-b", "master");
        provider.Codebases["a.sln"] = FakeFacts.With(("First", 1, "first"));
        await FactsAsync();
        var second = FakeFacts.With(("Second", 1, "second"));
        provider.Codebases["a.sln"] = second;

        Assert.Same(second.Types[0], (await FactsAsync()).Types[0]);
        Assert.False(Directory.Exists(Folder));
    }

    [Fact]
    public async Task ADirtyTreeHitsWhileItsChangesStayAndMissesWhenTheyChange()
    {
        await CommittedAsync();
        await WriteAsync("T.cs", "dirty");
        provider.Codebases["a.sln"] = FakeFacts.With(("Dirty", 1, "dirty"));
        await FactsAsync();
        provider.Codebases["a.sln"] = FakeFacts.With(("Other", 1, "other"));

        Assert.Equal("Dirty", Single(await FactsAsync()));

        await WriteAsync("T.cs", "dirtier");
        Assert.Equal("Other", Single(await FactsAsync()));
    }

    [Fact]
    public async Task ACleanTreeAndADirtyTreeHaveEntriesOfTheirOwn()
    {
        await CommittedAsync();
        provider.Codebases["a.sln"] = FakeFacts.With(("Clean", 1, "clean"));
        await FactsAsync();
        await WriteAsync("New.cs", "untracked");
        provider.Codebases["a.sln"] = FakeFacts.With(("Dirty", 1, "dirty"));
        await FactsAsync();
        provider.Codebases["a.sln"] = FakeFacts.With(("Other", 1, "other"));

        Assert.Equal("Dirty", Single(await FactsAsync()));
        File.Delete(Path.Combine(repo.Root, "New.cs"));
        Assert.Equal("Clean", Single(await FactsAsync()));
        Assert.Equal(2, Entries().Length);
    }

    [Fact]
    public async Task AChangedNavaroneJsonMissesEvenWhenGitIgnoresIt()
    {
        await CommittedAsync();
        await WriteAsync(".gitignore", $"{NavaroneConfig.FileName}\n");
        repo.Git("add", ".gitignore");
        repo.Commit();
        await WriteAsync(NavaroneConfig.FileName, """{ "limits": { "cyclomatic": 5 } }""");
        provider.Codebases["a.sln"] = FakeFacts.With(("First", 1, "first"));
        await FactsAsync();
        provider.Codebases["a.sln"] = FakeFacts.With(("Second", 1, "second"));

        Assert.Equal("First", Single(await FactsAsync()));
        await WriteAsync(NavaroneConfig.FileName, """{ "limits": { "cyclomatic": 4 } }""");
        Assert.Equal("Second", Single(await FactsAsync()));
    }

    [Fact]
    public async Task ANewCommitMisses()
    {
        await CommittedAsync();
        provider.Codebases["a.sln"] = FakeFacts.With(("First", 1, "first"));
        await FactsAsync();
        provider.Codebases["a.sln"] = FakeFacts.With(("Second", 1, "second"));
        await WriteAsync("T.cs", "next");
        repo.Git("add", "T.cs");
        repo.Commit();

        Assert.Equal("Second", Single(await FactsAsync()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ \"Types\": [ { \"Symbol\": ")]
    [InlineData("{ \"Types\": null }")]
    [InlineData("{ \"Types\": [], \"Extra\": 1 }")]
    [InlineData("{ \"Kinds\": [] }")]
    [InlineData("null")]
    public async Task AnEntryThatCannotBeReadIsAMissAndIsWrittenAgain(string corrupt)
    {
        await CommittedAsync();
        provider.Codebases["a.sln"] = FakeFacts.With(("First", 1, "first"));
        await FactsAsync();
        var entry = Assert.Single(Entries());
        await File.WriteAllTextAsync(entry, corrupt, TestContext.Current.CancellationToken);
        provider.Codebases["a.sln"] = FakeFacts.With(("Second", 1, "second"));

        Assert.Equal("Second", Single(await FactsAsync()));
        provider.Codebases["a.sln"] = FakeFacts.With(("Third", 1, "third"));
        Assert.Equal("Second", Single(await FactsAsync()));
    }

    [Fact]
    public async Task TheFolderKeepsTheTenNewestEntries()
    {
        await CommittedAsync();
        provider.Codebases["a.sln"] = FakeFacts.With(("First", 1, "first"));
        var written = new List<string>();
        for (var version = 0; version < 12; version++)
        {
            await WriteAsync("T.cs", $"version {version}");
            var before = Entries();
            await FactsAsync();
            var entry = Assert.Single(Entries().Except(before));
            File.SetLastWriteTimeUtc(entry, new DateTime(2026, 1, 1, 0, version, 0, DateTimeKind.Utc));
            written.Add(entry);
        }

        Assert.Equal(written.Skip(2).Order(StringComparer.Ordinal), Entries().Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AnEntryNeverShowsAsAChangeToTheTree()
    {
        await CommittedAsync();
        var before = repo.Git("status", "--porcelain", "--untracked-files=all");
        provider.Codebases["a.sln"] = FakeFacts.With(("First", 1, "first"));

        await FactsAsync();

        Assert.Single(Entries());
        Assert.Equal(before, repo.Git("status", "--porcelain", "--untracked-files=all"));
    }

    // Each control flow fact is part of nothing or of the very fact before it in its method that it was part of.
    private static void AssertEachWholeComesBefore(IReadOnlyList<ControlFlowFact> facts)
    {
        for (var at = 0; at < facts.Count; at++)
        {
            if (facts[at].PartOf is { } whole)
            {
                Assert.Contains(facts.Take(at), fact => ReferenceEquals(fact, whole));
            }
        }
    }

    private static string Single(CodebaseFacts facts) => Assert.Single(Assert.Single(facts.Types).Methods).Name;

    private string Folder => Path.Combine(repo.Root, ".navarone");

    private string[] Entries() => Directory.Exists(Folder) ? Directory.GetFiles(Folder, "*.json") : [];

    private Task<CodebaseFacts> FactsAsync() => new Code(provider, repo.Root, ["a.sln"]).FactsAsync(TestContext.Current.CancellationToken);

    private Task WriteAsync(string name, string text) => File.WriteAllTextAsync(Path.Combine(repo.Root, name), text, TestContext.Current.CancellationToken);

    // A repo with T.cs committed on master.
    private async Task CommittedAsync()
    {
        repo.Git("init", "-q", "-b", "master");
        await WriteAsync("T.cs", "committed");
        repo.Git("add", "T.cs");
        repo.Commit();
    }
}
