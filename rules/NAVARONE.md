# NAVARONE.md

Kelk's taste in code, and how Navarone runs in the build. Every agent reads this. When the repo you work in has its
own `NAVARONE.md` at its root, read it too: where it contradicts this file, it wins in that repo.

## The rules

Code is for the next change, not for looks.

When two rules pull apart, the higher tier wins.

### Deadly: the glance lies

Never traded away, not for a metric and not for a lower rule.

2. I know what it does at a glance, and the glance is true: no side effect hidden in a filter, no
   alignment that fakes a pattern.
3. One line does one thing: no crammed condition, no step hidden in an argument, no maths on ternaries.
6. Check null at the boundary; a signature never lies about what it accepts.

### Sin: dense in the form

Allowed only when a measured performance problem buys it.

5. No nested ternaries. A ternary only picks between two values, and parallel cases share one form.
8. A public method is its class's API: a small, cohesive class, and callers know nothing of the inside.
9. Dense in the domain is fine; dense in the form is not. Only a measured performance problem buys density.

### OK: a matter of shape

It always loses to a higher tier.

7. Plain over clever. Don't scatter a method into tiny ones; one style, braces everywhere.

### Good: the target

What to reach for when a signal leaves you choosing between two bad options.

1. Imperative: statements that do one step after another and read top to bottom like a story. An
   expression only where it reads plainer than the statements.
4. A longer method goes guard, resolve, return. Guards first as plain ifs; prepare data in named steps.

## Bad examples

```csharp
public static IReadOnlyList<BlockDrift> Of(IReadOnlyList<QuestionBlock> blocks) =>
    [.. Walk(blocks, [], (block, at) => Of(block).Select(says => new BlockDrift(at, says)))];
```

```csharp
public static IReadOnlyList<BlockAgreement> Agreements(IReadOnlyList<QuestionBlock> blocks) =>
    [.. Walk(blocks, [], (block, at) => Agreement(at, block) is { } agreement ? [agreement] : Array.Empty<BlockAgreement>())];
```

```csharp
public static IReadOnlyList<DreamRecord> Since(BrainEnv env, string conversation, DateTimeOffset since) =>
    [.. All(env).Where(record => record.Conversation == conversation && record.At >= since && record.Deep is null).OrderBy(record => record.At)];
```

```csharp
public IEnumerable<int> Within(string? path, int first, int last) => path == Path ? Lines.Where(line => line >= first && line <= last) : [];
```

```csharp
public IReadOnlyList<float[]>? Embed(IReadOnlyList<string> texts) => null;
```

```csharp
.Where(neighbour => shown.Add((neighbour.Edge.From.Ref, neighbour.Edge.Edge)))
```

```csharp
_live += (_burns[place].Live ? 1 : 0) - (was ? 1 : 0);
```

```csharp
private static Interval Apart(Interval range, Interval other) =>
    other.Lo != other.Hi ? range
    : range.Lo == other.Lo ? range.Meet(Interval.Any with { Lo = other.Lo + 1 })
    : range.Hi == other.Lo ? range.Meet(Interval.Any with { Hi = other.Lo - 1 })
    : other.Lo == 0 ? range.WithoutZero
    : range;
```

```csharp
public string? Problem() =>
    string.IsNullOrWhiteSpace(Path) ? "a focus has no path"
    : Lines is not [var first, var last] || first < 1 || last < first ? $"focus \"{Path}\" wants its lines as [first, last] with 1 <= first <= last"
    : null;
```

```csharp
private static string? KindProblem(string? kind)
{
    if (string.IsNullOrEmpty(kind))
    {
        return $"{FieldNames.Kind} is missing";
    }

    if (Kinds.IsKnown(kind))
    {
        return null;
    }

    return Migration.Moves(kind)
        ? $"{FieldNames.Kind} {kind} is from the old model; brain migrate moves it"
        : $"{FieldNames.Kind} {kind} is not one of {string.Join(Separator, Kinds.All)}";
}
```

```csharp
var burn = burns.All[(unit * burns.AbilityCount) + ability];
```

```csharp
return new ContextView(context.Commit, [.. context.Sections.Select(section => new SectionView(section, Drift.Of(section.Blocks), Drift.Sums(section.Blocks), Drift.Agreements(section.Blocks)))]);
```

```csharp
return new MethodFacts(
    ...,
    declaration is AccessorDeclarationSyntax { Parent.Parent: { } property } ? [.. NavaroneComments.IdsAt(property), .. NavaroneComments.IdsAt(declaration)] : NavaroneComments.IdsAt(declaration),
    ...);
```

## Good examples

```csharp
var light = new List<DreamRecord>();
foreach (var record in All(env))
{
    if (record.Conversation != conversation) { continue; }
    if (record.At < since) { continue; }
    if (record.Deep is not null) { continue; }
    light.Add(record);
}
```

```csharp
var live = _burns[place].Live;
if (live && !was)
{
    _live++;
}

if (was && !live)
{
    _live--;
}
```

```csharp
public Report Summarise(IReadOnlyList<Sale> sales, DateOnly day)
{
    if (sales.Count == 0)
    {
        return Report.Empty(day);
    }

    var today = sales.Where(sale => sale.Day == day).ToList();
    var total = today.Sum(sale => sale.Amount);
    var largest = today.MaxBy(sale => sale.Amount);

    return new Report(day, total, largest);
}
```

```csharp
var kind = node.Field("kind");
if (string.IsNullOrEmpty(kind))
{
    return "kind is missing";
}

return KindProblem(kind);

private static string? KindProblem(string kind)
{
    if (Kinds.IsKnown(kind))
    {
        return null;
    }

    if (Migration.Moves(kind))
    {
        return $"{FieldNames.Kind} {kind} is from the old model; brain migrate moves it";
    }

    return $"{FieldNames.Kind} {kind} is not one of {string.Join(Separator, Kinds.All)}";
}
```

```csharp
var burn = burns.Of(unit, ability);
```

```csharp
var server = new ReviewServer(env, Port(), url => Open(env, browser, url), TimeProvider.System, theme, () => ServeProcess.Start(env), models);
using var stop = new CancellationTokenSource();
using var hangup = PosixSignalRegistration.Create(PosixSignal.SIGHUP, context => context.Cancel = true);
using var interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, context => Stop(context, stop));
```

```csharp
var ids = NavaroneComments.IdsAt(declaration);
if (declaration is AccessorDeclarationSyntax { Parent.Parent: { } property })
{
    ids = [.. NavaroneComments.IdsAt(property), .. ids];
}
```

## The process

A repo opts in with a `navarone.json` at its root. In such a repo:

**The builder** runs `navarone` after its change. It shows the signals of the code the change touches. A signal is a
reason to look, never a verdict and never the solution. Answer every signal of each method, class or module the change
touches, the old ones included: leave the code better than you found it. Answer a signal by fixing the code and running
`navarone` again, or, when the code is right, by recording why: `navarone <id> <reason>`. When a pattern is wrong, fix
the pattern, not only the line where it shows. When that fix is a large change, don't make it and don't skip it: log it,
`navarone <id> large "<what is wrong, where it shows, what the fix is>"`, and name it in your report to the open agent,
who brings it to the user. The large change is the problem's answer, so your task lands. When the open agent sends you
for a large change the user decided, log his decision first: `navarone <id> settled replace "<his words>"`. When he says
no, the agent still working in that worktree logs `navarone <id> settled keep "<his words>"`. When the validator refuses
your answer and you still hold the code is right, answer with a new reason and leave the code as it is: that is a clash,
and the user judges it.

**The validator** runs `navarone` too. There is a reason it is a validator: it checks the code, the taste and the
architecture. An unanswered signal is a finding. It reads every resolution with the rules above and sends back each one
it does not accept. When it reads a signal and the builder's answer, it asks what design the leaf is patching over: it
notices that the method is cramped and thinks about the architecture. It records its position on every signal it
reviews, the fixed ones and the resolved ones: `navarone <id> keep` when it accepts the fix or the reason, and
`navarone <id> replace <rule> "<finding>"` when the code must still change. The rule is the number of the rule above
that the code breaks, such as `3`, and Navarone refuses a replace that names none. The finding opens with one sentence
on what is wrong in this code, with no metric in it, because that sentence is what the user reads first; the why
follows. When the real fix is a large change, bigger than the task, it logs it,
`navarone <id> large "<what is wrong, where it shows, what the fix is>"`, and names it in its report to the open agent.

**A clash** is the builder and the validator not agreeing on how to answer a signal: the validator refused the builder's
answer with `replace`, and the builder answered with a new reason while the code is unchanged. Navarone sees it in its
events. A clash goes to brain's window, not to the open agent in prose: the problem is red and `navarone` fails until
the user judges it there. He reads the rule, what is wrong, the code, the builder's reason and the validator's finding.
His keep makes the builder's reason the resolution; his replace keeps the problem red with the validator's finding and
his words until the builder changes the code. The next `navarone` run collects his answer.
