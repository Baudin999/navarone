using System.Text.Json.Nodes;
using Navarone.Engine.Events;
using Navarone.Engine.Reasons;
using Navarone.Tests.Support;

namespace Navarone.Tests.Events;

public sealed class NavaroneEventHandlerTests : IDisposable
{
    private static readonly EventPlace Here = new("/repo", "/repo/.claude/worktrees/one", "one", "abc123");

    private static readonly DateTimeOffset October = new(2026, 10, 31, 23, 59, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset November = new(2026, 11, 1, 0, 1, 0, TimeSpan.Zero);

    private readonly TempEvents events = new();

    public void Dispose() => events.Dispose();

    [Fact]
    public void AnEventIsOneFlatLineWithItsTypeTimePlaceAndOwnFields()
    {
        events.At(November).Write(Here, [new RaisedEvent("0000001", "cyclomatic", "T.First")]);

        var text = File.ReadAllText(Path.Combine(events.Folder, "events", "2026-11.jsonl"));
        Assert.EndsWith("\n", text, StringComparison.Ordinal);
        var line = JsonNode.Parse(text)!.AsObject();
        Assert.Equal(
            ["type", "at", "repo", "worktree", "branch", "head", "id", "check", "symbol"],
            line.Select(field => field.Key));
        Assert.Equal("raised", (string?)line["type"]);
        Assert.Equal(November, line["at"]!.GetValue<DateTimeOffset>());
        Assert.Equal(("/repo", "/repo/.claude/worktrees/one", "one", "abc123"), ((string?)line["repo"], (string?)line["worktree"], (string?)line["branch"], (string?)line["head"]));
    }

    [Fact]
    public void EachUtcMonthHasItsOwnFileAndTheLogReadsBackMonthByMonthInTheOrderWritten()
    {
        events.At(November).Write(Here, [new RaisedEvent("0000002", "cyclomatic", "T.Second")]);
        events.At(October).Write(Here, [new RaisedEvent("0000001", "cyclomatic", "T.First"), new ResolvedEvent("0000001", "a reason", null)]);

        var read = events.Handler.Read();

        Assert.Equal(["2026-10.jsonl", "2026-11.jsonl"], Directory.GetFiles(Path.Combine(events.Folder, "events"), "*.jsonl").Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal(["0000001", "0000001", "0000002"], read.Select(logged => logged.Event.Id));
        Assert.Equal(new ResolvedEvent("0000001", "a reason", null), read[1].Event);
        Assert.Equal(Here, read[0].Place);
        Assert.Equal(October, read[0].At);
    }

    [Fact]
    public void EveryEventTypeReadsBackAsItWasWritten()
    {
        INavaroneEvent[] all =
        [
            new RunEvent([new ShownProblem("0000001", "cyclomatic", "T.First", FindingState.Red, "c0de"), new ShownProblem("0000002", "lcom4", "T", FindingState.Green, null)], 42),
            new RaisedEvent("0000001", "cyclomatic", "T.First"),
            new FixedEvent("0000001", "cyclomatic", "T.First", "before", null, true),
            new ResolvedEvent("0000002", "the reason, as the builder wrote it", new DefendedCode("c0de", "T.cs", 3, 5, "method T.First class:short")),
            new ResolvedEvent("0000002", "a reason logged before its code was", null),
            new RefusedFixEvent("0000001", "cyclomatic", "T.First", "the fix only fooled the metric", "before", "after", new TasteRule(3, "One line does one thing.")),
            new RefusedReasonEvent("0000002", "cognitive", "T.Second", "the reason does not hold", "before", null),
            new AcceptedEvent("0000003", Position.Replace),
            new LargeChangeEvent("0000004", "cyclomatic", "T.First", "One record holds two kinds of dream.", "c0de"),
            new SettledEvent("0000004", "cyclomatic", "T.First", Position.Keep, "the two kinds share every field", "c0de"),
            new SettledEvent("0000004", "cyclomatic", "T.First", Position.Replace, null, null),
        ];

        events.Handler.Write(Here, all);

        var read = events.Handler.Read().Select(logged => logged.Event).ToList();
        Assert.Equal(all.Length, read.Count);
        for (var at = 0; at < all.Length; at++)
        {
            Assert.Equal(new LoggedEvent(November, Here, all[at]).Line(), new LoggedEvent(November, Here, read[at]).Line());
        }
    }

    [Fact]
    public void PositionsAreWrittenAsKeepOrReplace()
    {
        events.Handler.Write(Here, [new RefusedReasonEvent("0000002", "cognitive", "T.Second", "the reason does not hold", "before", null), new AcceptedEvent("0000003", Position.Replace)]);

        var lines = File.ReadAllLines(Directory.GetFiles(Path.Combine(events.Folder, "events"))[0]).Select(line => JsonNode.Parse(line)!).ToList();
        Assert.Equal(("refused-reason", "keep", "replace"), ((string?)lines[0]["type"], (string?)lines[0]["builder"], (string?)lines[0]["validator"]));
        Assert.Equal(("accepted", "replace", "keep"), ((string?)lines[1]["type"], (string?)lines[1]["builder"], (string?)lines[1]["validator"]));
    }

    [Fact]
    public async Task AppendsAtOnceNeitherInterleaveNorLoseALine()
    {
        var reason = new string('x', 20_000);
        var writers = Enumerable.Range(0, 16).Select(writer => Task.Run(() =>
        {
            for (var line = 0; line < 20; line++)
            {
                events.Handler.Write(Here, [new ResolvedEvent($"{writer:D3}{line:D4}", reason, null)]);
            }
        }, TestContext.Current.CancellationToken));

        await Task.WhenAll(writers);

        var read = events.Handler.Read();
        Assert.Equal(320, read.Count);
        Assert.Equal(320, read.Select(logged => logged.Event.Id).Distinct().Count());
        Assert.All(read, logged => Assert.Equal(reason, ((ResolvedEvent)logged.Event).Reason));
    }

    [Fact]
    public async Task AWriteWaitsWhileAnotherProcessHoldsTheLock()
    {
        Directory.CreateDirectory(Path.Combine(events.Folder, "events"));
        var held = new FileStream(Path.Combine(events.Folder, "events", ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var write = Task.Run(() => events.Handler.Write(Here, [new RaisedEvent("0000001", "cyclomatic", "T.First")]), TestContext.Current.CancellationToken);

        await Task.Delay(200, TestContext.Current.CancellationToken);
        Assert.False(write.IsCompleted);
        Assert.Empty(events.Handler.Read());
        await held.DisposeAsync();
        await write;

        Assert.Single(events.Handler.Read());
    }

    [Fact]
    public void ALineStillBeingWrittenIsNotReadYet()
    {
        events.At(November).Write(Here, [new RaisedEvent("0000001", "cyclomatic", "T.First")]);
        File.AppendAllText(Path.Combine(events.Folder, "events", "2026-11.jsonl"), """{"type":"raised","at":""");

        Assert.Single(events.Handler.Read());
    }

    [Theory]
    [InlineData("", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData("abc", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    public void TheHashACodeIsStoredUnderIsItsSha256InLowerCaseHex(string code, string expected)
    {
        Assert.Equal(expected, NavaroneEventHandler.HashOf(code));
    }

    [Fact]
    public void ALineThatIsNoEventIsAnErrorThatNamesItsFileAndLine()
    {
        events.At(November).Write(Here, [new RaisedEvent("0000001", "cyclomatic", "T.First")]);
        var month = Path.Combine(events.Folder, "events", "2026-11.jsonl");
        File.AppendAllText(month, """{"type":"unknown","at":"2026-11-01T00:00:00+00:00","repo":"/r","worktree":"/r"}""" + "\n");

        var refused = Assert.Throws<InvalidDataException>(() => events.Handler.Read());

        Assert.StartsWith($"{month} line 2 is no Navarone event", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAppendWhoseLockStaysHeldPastThePatienceLosesItsEventsWithOneLineAndNoException()
    {
        var directory = Path.Combine(events.Folder, "events");
        Directory.CreateDirectory(directory);
        using var held = new FileStream(Path.Combine(directory, ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var error = new StringWriter();
        var handler = new NavaroneEventHandler(events.Folder, new SteppingClock(), error);

        handler.Write(Here, [new RaisedEvent("0000001", "cyclomatic", "T.First")]);

        var line = Assert.Single(error.ToString().TrimEnd().Split('\n'));
        Assert.StartsWith("navarone: the event log was not written, 1 event(s) lost: ", line, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(directory, "*.jsonl"));
    }

    [Fact]
    public void CodeThatCannotBeStoredIsLostWithOneLineAndItsHashIsStillReturned()
    {
        File.WriteAllText(Path.Combine(events.Folder, "code"), "a file where the code store's folder belongs");
        using var error = new StringWriter();

        var hash = events.Reporting(error).Store("void First() { }");

        Assert.Equal(NavaroneEventHandler.HashOf("void First() { }"), hash);
        var line = Assert.Single(error.ToString().TrimEnd().Split('\n'));
        Assert.StartsWith($"navarone: the code under {hash} was not stored: ", line, StringComparison.Ordinal);
        Assert.Null(events.Handler.Code(hash));
    }

    [Fact]
    public void NoLogReadsAsNoEvents() => Assert.Empty(events.Handler.Read());

    [Fact]
    public void CodeIsStoredOnceUnderTheHashOfItsText()
    {
        var hash = events.Handler.Store("void First() { }");

        Assert.Equal(hash, events.Handler.Store("void First() { }"));
        Assert.Equal(NavaroneEventHandler.HashOf("void First() { }"), hash);
        Assert.Equal([hash], Directory.GetFiles(Path.Combine(events.Folder, "code")).Select(Path.GetFileName));
        Assert.Equal("void First() { }", events.Handler.Code(hash));
    }

    [Theory]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("../events/2026-11.jsonl")]
    [InlineData("")]
    public void AHashNoCodeIsStoredUnderHasNoCode(string hash) => Assert.Null(events.Handler.Code(hash));

    // A clock that moves a minute on at every reading, so a wait for a lock reaches its patience at once.
    private sealed class SteppingClock : TimeProvider
    {
        private DateTimeOffset now = November;

        public override DateTimeOffset GetUtcNow()
        {
            now = now.AddMinutes(1);
            return now;
        }
    }
}
