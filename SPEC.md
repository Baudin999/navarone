# Navarone spec

Navarone is the static analyzer an AI agent runs so it writes less slop. It
gives signals only: every finding sends the agent to look. The agent answers
it by fixing the code or by giving a reason, which Navarone stores as the
problem's resolution. While that code is unchanged the signal stays silent;
touching the code opens it again.

## Parts
- Engine: language agnostic. Runs the checks, owns reasons.json, reports.
- Language providers: one per language, parse it and hand the engine resolved
  facts. C# reads through Roslyn only; no tree-sitter, no fallback, no flag.
- Checks: compute over facts, never see a language.
- Classifiers: deterministic rules over the facts and a reason's text. They
  call a problem a false positive, or reject a reason. They never see a
  language.
- The lens: the engine builds what a lens shows as one page of sections,
  blocks and marks, and writes it as markdown or as JSON.
- The event log: what Navarone made happen and what it cost, in Navarone's
  own data folder, so brain can show whether Navarone is worth its tokens.
Only C# is analysed for now.

## Checks
A finding is raised only above a limit. Every limit is configurable in
navarone.json at the repo root, as `{ "limits": { "cyclomatic": 8 } }`;
these are the defaults. A limit may be set
at or below its default, never above: the agent cannot loosen the gate. A
limit above its default is refused with one line on stderr and exit 2.

| Check            | Subject | Counts                                              | Default |
|------------------|---------|-----------------------------------------------------|---------|
| cyclomatic       | method  | McCabe: decisions in any syntax, plus one           | 10      |
| cognitive        | method  | Campbell: breaks in linear flow, weighted by nesting| 15      |
| lcom4            | class   | connected pieces of the class's methods and fields  | 1       |
| control-coupling | method  | parameters that steer which branch runs             | 0       |
| split-complexity | method  | the method's cyclomatic with its helpers inlined:   | 10      |
|                  |         | its decisions plus each helper's, plus one          |         |

split-complexity is the trick of splitting a method so each piece passes
while the whole does not. A helper is any method of the method's own type or
of a type nested in it, whatever its accessibility, whose callers are all
the method or other helpers, and which is at or under the cyclomatic limit.
A method that implements an interface member or overrides a base member
declared outside the method's own type and the types nested in it is no
helper: it is called through that contract, so its resolved callers are not
all its callers. A contract declared inside them is part of the split and
hides nothing. A call into any other type, the type the method's own type is
nested in included, is a hand-off to a different job. The value is the
method's decisions plus each helper's decisions (cyclomatic minus one), plus
one: the cyclomatic the method would have with its helpers inlined. So
inlining a split scores the same as the split, and a helper without
decisions adds nothing. Every helper adds its decisions, a flat dispatch or
a mapping included. It reads the cyclomatic results of the method and its
helpers, and is measured on the code as it is now, with no git history. Its
question lists every helper that adds a decision with the decisions it adds:
the helpers a reason for the split must name.

lcom4 is standard LCOM4. Static methods count as methods; a static field
joins nothing. A method that uses no instance field, calls no other method of its
class and is called by none is left out, and a test class (its methods carry [Fact],
[Theory], [Test] or [TestMethod]) is not measured. A primary constructor
parameter a method reads is a field. A static factory, a static method that
calls a constructor of its own class, uses the fields that constructor sets.
A method declared without a body, such as a [GeneratedRegex] partial method,
joins its callers like any other method.

A field or property initializer that holds logic, a lambda or any branch, is
measured like a method under its member, and its calls count as calls.
Cyclomatic counts `?.`, `??` and `??=` as decisions. Generated code is only what the
build generates: files under obj/ or from a source generator.

A decision costs the same in any syntax, as McCabe counts predicates.
Cyclomatic and cognitive count a when guard as one decision, on a case, an
arm or a catch: it can fail after its pattern matched. A subpattern that
tests a value is one decision: a property pattern such as
`{ IsConst: false }`, a nested type test such as
`{ ContainingSymbol: IMethodSymbol c }`, a constant or a range, in an `is`
pattern anywhere as in a switch. A bare binding, `{ Name: var n }` or `_`,
tests nothing and adds nothing. A case or arm whose pattern is a discard or
a var pattern, such as `_ when c`, `var x` or `case var x when c:`, tests
nothing and adds nothing of its own; its when counts. A list pattern tests
its length, a constant or a range, as one decision, unless a lone slice such
as `[..]` takes any length. Each element that tests a value is one decision
more, as a subpattern is, and what it holds counts on its own; a discard, a
var or a bare slice is free, so `s is ['a', _, ..]` adds 2 to what its if
costs and `v is [[1]]` adds 4. A `not` in a pattern adds nothing, like a
`!`: it flips a test that is already counted, and the pattern under it
counts as it would without it, so `is not null`, a `not null` arm,
`{ A: not null }` and `A and not B` cost what they cost without the `not`.
`and`, `or`, `&&` and `||` count as before. A call and a `!` add nothing of
their own: the decision is the test of their result. So the arm
`IParameterSymbol { ContainingSymbol: IMethodSymbol c } p when IsPrimary(c) && !InInitializer(node)`
is 4 decisions: its type test, the nested type test, the when and the &&,
what the same tests cost written as an if. In cognitive each costs one flat.

A cyclomatic finding carries its evidence: how the value comes to life. Navarone
builds it from Roslyn alone, in the same code that computes the value, so the
two never drift. It holds the code block, with its
path, first and last line and text; the finding, with its check, symbol, value,
limit and base; and a proof tree. Each proof item has its span in the file,
start and end line and column and the span's text, its kind as C# writes it,
such as if, else, case, arm, when, subpattern, default, &&, ||, ?:, ?. or a
loop, what it adds, why, and its parts. An if is one item, and the && and ||
in its condition are its parts, each with its own span and weight. An arm is
one item, and its subpatterns and its when are its parts, the && in the when
a part of the when. A construct the rule does not count, a plain else, a
switch as a whole, a default case or a goto, shows with adds 0 and why. A
statement inside another one, such as an if in a loop's body, is an item of
its own. The value is the base, 1, plus every adds in the tree. The other
checks build no evidence yet.

## Ignores
A repo opts into Navarone by holding a navarone.json at its root;
`navarone init` writes it. Navarone still runs in a repo without one, with
the default limits and nothing ignored. Beside the limits, navarone.json
holds globs for files and for projects to ignore:

    { "ignore": { "files": ["src/Generated/**"], "projects": ["*.Tests"] } }

A file glob matches a file's whole path relative to the repo root, with /
between directories. A project glob matches a project's name, its project
file's name without the extension. In a glob, `**` matches any depth, so
`**/` also matches no directory at all, `*` matches any characters but /,
`?` one character but /, and every other character matches itself.

Ignored code is gone from every part of Navarone: the provider reads no
fact from it. No check raises a signal on it, the lens does not show it,
not as a subject, a caller or an expanded call, duplicates does not pair
it, and reasons.json drops its problems as it drops a problem whose code
is gone. A field declared in an ignored file is no field of its type.

Agents may change navarone.json, ignores included. Unlike a limit, which
may never be set above its default, an ignore is trusted to the agent: the
user reverts a wrong one by hand. A missing `ignore` ignores nothing. A
top-level key other than `limits` and `ignore`, such as a misspelled
`ignores`, an `ignore` with a key other than `files` and `projects`, or a
glob that is null or empty, is refused with one line on stderr and exit 2.

## From finding to verdict
1. Find: a check raises a finding on a method, class or module. A new
   problem gets an id from Navarone.
2. Classify the problem: the deterministic classifier calls it a false
   positive, or says nothing. A false positive is green without a reason.
3. Reason: the agent fixes the code and runs Navarone again, or puts the id
   in a comment at the code and gives a reason with `navarone <id> "<reason>"`.
4. Classify the reason: the deterministic classifier rejects it, or Navarone
   stores it as the problem's resolution with the code it answered.

Navarone does not weigh a reason. The validator reviews the agent's work and
its reasons with the user's taste, and a clash between them goes to the
user's bench in brain's window, as "Clashes" says. When the real fix is a
large change, bigger than the task, it goes to the open agent, as "Large
changes" says.

## Ids in the code
The code holds only a reference, never the prose:

    // navarone: 0001234
    public decimal Calculate(Order order) { ... }

An id is seven ASCII digits. An id belongs to one problem: its check and its
resolved symbol. An id copied onto other code does not match and does not
count. A change to the code opens the problem again: it keeps its id and is
red until a new reason is stored.

## reasons.json

- Lives at the root of the analysed repo. Written only by Navarone; an agent
  never edits it. It and navarone.json are written to a file of their own
  and moved over the target, so a crash mid-write leaves no truncated file. A
  write that fails says so on one line and exits 2.
- Holds `granted`, the last id Navarone granted, so an id is never granted
  twice, and every problem by id with its check, symbol, fingerprint, value
  and resolution. When it grows too big it moves to sqlite.
- A resolution is the agent's last reason for the problem that no rule
  rejected, with the fingerprint and value of the code it answered. A new
  reason takes the place of the one before it.
- A problem is green while its resolution answers its code as it is now and
  the code carries its id. One reason answers one problem.
- A reason that repeats a resolution in reasons.json word for word is
  rejected. A rejected reason is not stored, so a reason refused because the
  id was not at the code yet can be given again once it is.
- A fixed problem stays in reasons.json while its code exists. It is no
  longer printed.
- Changing the code, a rename included, opens the problem again: it is red
  until a reason for the new code is stored.
- A resolution keeps the value of the finding it answered, and the problem
  the value Navarone last saw. A changed value opens the problem as changed
  code does, so a change to how a check counts sends its findings back to
  the agent.
- A problem whose code is gone is removed by Navarone.

## Classifying a problem
The deterministic classifier classifies every problem on every run, so a
changed rule reaches the problems classified before.
- A cyclomatic or cognitive problem on a flat dispatch is a false positive:
  a switch, an else-if chain or a chain of conditional expressions where
  every branch is a single return or a single expression, and every
  condition is one test. A condition that combines tests with && or ||, a
  property or positional pattern, or a case or arm with a when is not flat;
  a pure label list, a pattern that joins constants or types with or and
  and such as `1 or 2 or 3`, only names the cases of one branch and stays
  flat.
- A control-coupling problem on a mapping is a false positive: a flat
  dispatch whose every branch only returns a value is a function of its
  input, not a caller choosing behaviour. A flat dispatch whose branches act
  stays a problem.
- An lcom4 problem on a value class is a false positive: a class whose every
  method other than a constructor is a property accessor without control
  flow and without a call, that reads or writes at most one of the class's
  own fields. Each piece is then one field with its accessors, which is
  data, not a second job.
- The language provider decides what is a flat dispatch, a mapping and an
  accessor.
- No rule calls a split-complexity problem a false positive: a split is
  often ordinary decomposition, such as a parser with one reader per state,
  so the agent answers it.
- A false positive is green without a reason. It stays in reasons.json, and
  the agent is never asked about it. Every other problem is red until the
  agent fixes the code or a resolution answers it.

## Classifying a reason
The deterministic classifier rejects a reason when the problem has a finding
and its code does not carry its id, when it has fewer than 8 words, when it
repeats a resolution in reasons.json word for word, and when it answers a
split-complexity problem without naming every helper that adds a decision.
A helper is named by its method name as a whole word in the reason, in its
own case; backticks or a trailing "()" around it are fine. Overloads share
a name, so one mention covers them. A helper that adds no decision need not
be named. The rejection names the helpers left out. Navarone stores every
other reason as the problem's resolution.

## Install
`install.sh` at the repo root installs Navarone. The user runs it; it can
be run again safely. Before it changes anything, it prints every path it
will write, says that Claude Code loads the rules link into every session
in every project, and asks `Continue? [y/N]`. Only y or yes goes on; any
other answer, an empty one or a closed stdin included, changes nothing and
exits 1. It publishes `navarone` into `~/.local/lib/navarone`,
outside the checkout, so cleaning the checkout leaves it working, and links
it into `~/.local/bin`, on the user's PATH. Whether or not brain is
installed, it links `<CLAUDE_CONFIG_DIR>/rules/NAVARONE.md`, `~/.claude` by
default, to the checkout's `rules/NAVARONE.md`, so Claude Code loads it into
every agent and an edit in the checkout reaches them without installing
again. A file there that is not a link is left alone, and the install says
so. It removes `<CLAUDE_CONFIG_DIR>/rules/taste.md` only when it is a link
whose target is gone, and says so. When brain is installed, it keeps the
navarone line of `<BRAIN_HOME>/sources.tsv` at five columns:
`navarone<TAB>list<TAB>context<TAB>events<TAB>code`. The list command is
`'<path>/navarone' disputes --json`. The context command is
`'<path>/navarone' lens --json`; brain's window runs it with a lens's words
after it, each word one argument, never through a shell. The events command
is `'<path>/navarone' events --json`. The code command is
`'<path>/navarone' code`; brain appends the hash as one argument. No navarone
line yet: the full line is added. A line with fewer columns gets the missing
ones from where it stops and keeps every column it has, a blank column at
the end counting as missing; a full line is kept as it is. Each case prints
one line saying which. The commands name the linked `navarone` by its
absolute path, quoted for sh, because brain's server may run without
`~/.local/bin` on PATH. No agent writes that file: brain runs its commands
as the user. The publish goes through the machine's guard when there is one.

## CLI
One command. Agents may run it themselves.
- `navarone`: checks the repo it runs in, grants ids to new problems and
  prints each red problem in the code the current change touches, as
  "The change" says, as a question with its id, check, symbol and place,
  and each problem a clash holds red, whatever the change, as "Clashes"
  says. It collects the user's judgements of clashes first. Exit 0 when
  every problem it prints is green or it prints none, 1 while any is red.
  With no change and no clash it prints nothing and exits 0. A repo where
  it finds no commit to measure the change from is a usage error: exit 2.
- `navarone --full-coverage`: the same, over every problem in the repo.
- `navarone init`: writes navarone.json at the repo root with the test
  projects ignored, `{ "ignore": { "projects": ["*.Test", "*.Tests"] } }`,
  so a project such as `Navarone.Tests` is ignored from the start. It
  adds the line `.navarone/` to the repo's `.gitignore`, so git never
  tracks the facts cache, creating the file when there is none. It writes
  no other file. A navarone.json that is there already is left untouched,
  and so is a `.gitignore` with a line that ignores the folder already,
  `.navarone/`, `.navarone`, `/.navarone/` or `/.navarone`, and init says
  so of each. It needs no solution. It writes a `cost`. Exit 0 either way.
- `navarone <id> "<reason>"`: stores the reason as that problem's
  resolution, unless a rule rejects it, and says which on one line, and
  logs a `resolved` with the code it defends. Exit 0 once stored, 1 when
  rejected. A fixed problem, which has no finding now, takes a reason only
  while the validator's refusal of its fix stands on the worktree: the
  reason argues for the fix, so it is logged and stored nowhere, its code
  need not carry its id, and every other rule of "Classifying a reason"
  holds. Any other id that matches no problem in the code as it is now is a
  usage error: exit 2.
- `navarone <id> keep`: the validator accepts the builder's answer to the
  problem, its fix or its reason, and Navarone logs an `accepted`, as "The
  event log" says. Exit 0 once logged.
- `navarone <id> replace <rule> "<finding>"`: the validator holds that the
  code must still change. The rule is the number of the rule of NAVARONE.md
  the code breaks, under its heading "The rules", and the finding says why,
  opening with one sentence on what is wrong. Navarone logs a `refused-fix`
  or a `refused-reason` with the rule's number and text. Exit 0 once
  logged. A blank finding is a usage error, and so is a replace that names
  no rule: a word that is no number of a rule, a replace with only a
  finding, and a NAVARONE.md that cannot be read or numbers no rule. Each
  exits 2. Navarone reads the `NAVARONE.md` at the root of the repo
  when it numbers a rule under "The rules", which wins in that repo, and
  otherwise `<CLAUDE_CONFIG_DIR>/rules/NAVARONE.md`, `~/.claude` by default,
  the link `install.sh` makes. A repo file with no numbered rule there
  contradicts none, so the global rules apply. A file with CRLF line ends
  reads as one with LF.
- `navarone <id> large "<text>"`: the real fix of the problem is a large
  change, bigger than the task. The text says what is wrong, where it shows
  and what the fix is. Navarone stores it as the problem's resolution and
  logs a `large`, as "Large changes" says. Exit 0 once logged. A blank
  text, an id Navarone never granted, a problem whose subject is gone from
  the code, and a problem with a finding now whose code does not carry its
  id, are usage errors: exit 2. A fixed problem whose finding is gone needs
  no id at its code.
- `navarone <id> settled keep|replace ["<words>"]`: an agent logs the
  user's decision on a large change, with his words, as "Large changes"
  says. Exit 0 once logged. A choice other than keep and replace, a problem
  no `large` of the repo names, and a problem in a clash on the worktree,
  are usage errors: exit 2. The user judges a clash in brain's window.
- An id is seven ASCII digits. A first word that is neither a command nor of
  an id's shape, and the id in `evidence <id> --json` when it is not of that
  shape, is a usage error: the usage text, exit 2, and nothing is logged. A
  mistyped command is never taken for a reason.
- `keep`, `replace`, `large` and `settled` are reserved: a reason that is
  exactly one of them is no reason, and each without its text, or `settled`
  without its choice, is a usage error.
- `keep` and `replace` change neither reasons.json nor a resolution: they
  record the validator's position. Each is a usage error, exit 2, on an id
  Navarone never granted, on a problem that is still red, on a false
  positive, and on a problem with no finding now and no `fixed` logged on
  this worktree: there is nothing to accept or refuse. They work on a fixed
  problem whose finding is gone from the code.
- Every command above that an agent reads, and that is no `navarone` or
  `navarone --full-coverage`, writes one `cost` event when it ends: `init`,
  `lens`, `evidence`, `duplicates`, a reason, `keep`, `replace`,
  `large` and `settled`. A command that ends in a usage error writes
  none. The commands for brain write no `cost`: `events --json`,
  `code <hash>`, `disputes --json`, `lens --json`, `map --json` and
  `classes <path> --json`. They write
  nothing.
- `navarone events --json`: every event in the log as one JSON document,
  `{"source":"navarone","events":[...]}`, each event as its line holds it,
  for brain to read. It writes nothing and works from any directory. A torn
  line is skipped, as in every other command, and named by file and line
  number in one line on stderr, `navarone: skipped torn line: <file> line
  <number>`, or `lines:` with the names joined by commas. It prints every
  event it can read and exits 0. A line of the log that is JSON and no event
  Navarone knows is a usage error that names its file and line: exit 2.
- `navarone code <hash>`: prints the code stored under the hash an event
  names, as it was written. It writes nothing and works from any
  directory. A hash no code is stored under is a usage error: exit 2.
- `navarone disputes --json`: the questions brain's window lists, as one
  JSON document, `{"source":"navarone","questions":[...]}`: one per
  clash the user has not judged yet, as "Clashes" says. It reads
  the event log and the code store, writes nothing and works from any
  directory.
- `navarone evidence <id> --json`: prints the evidence document of the
  problem with that id, from the code as it is now, so it can be checked by
  hand. It grants no id and writes only its `cost`. Exit 0 once printed.
  An id that matches no problem in the code as it is now, or a problem
  whose check builds no evidence, is a usage error: exit 2.
- `navarone lens method <Type.Method> <view>...`: prints what the lens
  shows of the method, as markdown, as "The lens" says. It grants no id and
  writes only its `cost`, so an agent may run it. Exit 0 once printed. A
  lens that does not parse, a kind or view not built, a value a view does
  not take, a name that matches no method, or an overload number no
  overload has, is a usage error: exit 2, with what is wrong and the lens
  with a caret under where it is.
- `navarone lens --json method <Type.Method> <view>...`: prints the same
  lens as one JSON document for brain's window, as "One page, two outputs"
  under "The lens" says, and otherwise works as `navarone lens`, but it
  writes no `cost`.
- `navarone map --json`: prints the map of the code as one JSON document for
  brain's window, in the repo it runs in. It writes nothing: it grants no id,
  stores no code and logs no event, not even a `cost`, so brain may run it as
  often as it needs. It reads through the facts cache, as the lens does, and a
  repo with no navarone.json or outside git behaves as the lens does there. The
  document is `{"commit", "changed", "files", "problems"}`:
  - `commit` and `changed` name the version of the code, as "The lens" does:
    the full hash of HEAD, or null outside git or with no commit yet, and the
    paths of the files that differ from it, none when the working tree is the
    commit. `changed` is empty outside git.
  - `files` holds one `{"path", "methods"}` per source file with a method,
    ordered by path. `path` is relative to the repo. `methods` holds
    `{"name", "firstLine", "lastLine", "type"}` in line order: `name` is the
    method's name in a lens, `Type.Method` with `:N` where the name matches
    several overloads, as `navarone lens method` takes it. The lines are those
    of the method's code as written, comments included, and `type` is its
    declaring type. A method no lens can name, such as an accessor, is not in
    the map.
  - `problems` holds every red problem `navarone --full-coverage` prints, in id
    order, as `{"id", "path", "firstLine", "lastLine", "method", "check",
    "state", "question"}`. The lines are those of the subject's code. `method`
    is the subject's name in a lens, or null for a class or a module. `state`
    is `red`, or `clash` when a clash holds the problem red, as "Clashes" says,
    and then `question` is what `navarone` prints for it. A problem no run has
    shown yet has a null `id`: Navarone grants ids only to a `navarone` run.
- `navarone classes <path> --json`: prints the class view of one source file
  as one JSON document for brain's window: every type declared in it, each
  member and the piece of its class the member belongs to, so the window shows
  the class's API and how a method fits in it. `path` is relative to the repo.
  It writes nothing, as `map --json` does, and reads through the facts cache. A
  path to no file, or to a file outside the repo, is a usage error: exit 2. A
  file that declares no type has none. The document is `{"commit", "path",
  "types"}`:
  - `commit` is the full hash of HEAD, or null outside git or with no commit
    yet, as in `map --json`, and `path` the file's path as the repo names it.
  - `types` holds one `{"assembly", "namespace", "name", "containingType",
    "firstLine", "lastLine", "lcom4", "members"}` per type whose first
    declaration is in the file, a nested type included, in line order.
    `assembly` is the name of the assembly that compiles it, `namespace` its
    namespace in full, empty for the global namespace, and `name` its own short
    name. `containingType` is the short name of the type it is nested in, or
    null for a top-level type. The lines are those of its declaration as
    written. `lcom4` is `{"value", "limit", "above"}`: the number of pieces the
    lcom4 check counts, its limit, and whether the value is above the limit,
    whatever answers the problem. It is null for a type lcom4 does not measure:
    an interface and a test class.
  - `members` holds one `{"name", "kind", "public", "firstLine", "lastLine",
    "lens", "piece"}` per member declared in the file, in line order. `name`
    is the name a question shows: `Parse(string)` for a method, `.ctor(int)`
    for a constructor, `Total.get` for a property's accessor, and a field's or
    auto-property's own name. `kind` is `method`, `constructor`, `property` or
    `field`: an auto-property is one `property`, a property with a body is one
    `property` per accessor, and the lines of an accessor start at its
    property's signature. A primary constructor's captured parameter is a
    `field`. An initializer is its field's. `public` is true for a member any
    code may use. The lines are those of the member's declaration as written.
    `lens` is the member's name in a lens, `Type.Method` with `:N` where the
    name matches several overloads, as `navarone lens method` takes it, or
    null for a member no lens can name, such as a constructor or an accessor.
    `piece` is the number, from 1, of the lcom4 piece that holds the member,
    in the order of the check's question, or null for a member in no piece:
    a constructor, a field no method of a piece uses, and a method that uses
    no instance field, calls no other method of the class and is called by
    none.
  A partial type shows only in the file of its first declaration, with the
  members declared there.
- `navarone duplicates [--similarity <share>] [--tokens <count>]`: prints
  every clone pair at or above the floor, similarity 0.7 and 30 tokens by
  default, with both pieces of code. It is research toward a duplication
  check: it raises no finding and writes only its `cost`. An option it
  cannot read is a usage error: exit 2.

`navarone` analyses every .sln and .slnx anywhere under the repo root,
skipping bin, obj, .git and other hidden directories. Each solution is
analysed on its own with all the projects in it. A project two solutions
share is reported once, with one id per problem. A repo with no solution
anywhere is a usage error: exit 2.

## The facts cache
Every command that reads the code, `navarone`, `--full-coverage`, a
reason, `keep`, `replace`, `large`, `settled`, `evidence`, `lens`,
`lens --json`, `map --json`, `classes --json` and `duplicates`, loads every solution and builds its facts.
Building them is the slow part, so Navarone keeps the merged facts of a
version of the code in `.navarone/` at the root of the repo it runs in, in
a git worktree the worktree's own root, and the next command at the same
version reads them from there. The facts it reads are exactly the facts a
fresh load builds, so every output is the same with the cache as without
it. That holds only while the facts depend on the code alone, so Navarone
keeps only facts the provider loaded completely. The C# provider calls a
load incomplete when MSBuild reports a failure, or when a project it reads
misses a type, a namespace or an assembly it references, as a project
whose packages are not restored does: its symbols do not bind, and a
restore, which changes no file git sees, would change its facts. Such
facts are used for the command and never written, so the next command
loads them again. The cache is no output: a command that writes nothing,
or only its `cost`, still reads and writes it.

The version is the key of an entry, one SHA-256 hash of:
- the full hash of HEAD;
- the path and contents of every file that differs from HEAD, tracked
  changes and untracked files, the same files the lens lists as changed;
- the contents of navarone.json;
- the solutions loaded, by path;
- the build of Navarone: the module version id of the engine's assembly
  and of the provider's, so no build reads facts another build wrote.

A clean tree and a dirty tree therefore get separate entries, and a
builder's dirty worktree still gets hits while its code does not change.
Outside a git repository, or in one with no commit yet, there is no cache:
the facts are built every time. A git status that fails means no cache
either.

`.navarone/` holds at most 10 entries. Before a new entry is written, the
oldest entries, by the time each was written, are deleted so that at most
10 remain after the write. An entry is written whole or not at all,
through a temporary file moved over it. An entry that cannot be read,
corrupt, partial or of another shape, is a miss: the facts are built again
and the entry is written again, never an error. A folder that cannot be
written leaves the entry out. The folder holds a `.gitignore` of its own
that ignores everything in it, so an entry never shows as a change, never
changes the key and never reaches the lens's list of changed files, even
in a repo whose `.gitignore` does not name the folder; `navarone init`
adds `.navarone/` to the repo's `.gitignore` as well.

## The change
`navarone` answers what a builder must answer: the signals in the code its
own change touches. The change is the diff between the working tree and
the commit where HEAD left the default branch, its merge-base with that
branch. The default branch is the one origin/HEAD names, the local branch
before the remote one, else master, else main. Staged, unstaged and
untracked work is part of the change, and every line of an untracked file
is changed. Lines removed from between two lines of a subject change it.

A signal belongs to its subject, not to a line, because we leave the code
better than we found it. A changed line opens every signal of the method,
class or module it sits in, old ones included: a method signal when a
line of that method changes, a class signal when a line of that class
changes, in its first declaration or in any of its fields and methods,
in whichever partial declaration they are written, and a
module signal when a file of that module changes; no check measures a
module yet. A split-complexity
signal is also opened by a change to one of its helpers, whose code its
fingerprint covers.

The coverage decides only what is printed and what the exit code counts.
Every problem is still found, granted an id and recorded in reasons.json.
`navarone --full-coverage` prints every red problem in the repo. Outside a
git repository, or when none of those branches shares a commit with HEAD,
`navarone` is a usage error that names `--full-coverage`: exit 2.
A git command that fails while it reads the change, or the status a lens
names, is an error and never an empty change: exit 2, with one line on
stderr that names the command and carries git's stderr. Every git command
gets 60 seconds; one that runs longer is killed, and that is the same
error.

## The event log
Navarone logs what it made happen, so brain can show what Navarone cost and
what it changed: the signals it raised, the code the builder changed
because of it, the reasons it defended code with, the validator's
position on each, each large change, and the user's decision on each
large change and clash. Navarone works without brain installed.

The log lives in Navarone's own data folder, `~/.local/share/navarone`, or
`$XDG_DATA_HOME/navarone` when that is set, never in brain's folder or in
the repo. It is `events/YYYY-MM.jsonl`, one file per UTC month, one JSON
object per line, appended and never rewritten. Several worktrees run
`navarone` at once, so every append, from any thread or process, holds an
exclusive lock on `events/.lock` and writes its lines in one write: no two
appends interleave and none is lost.
A log that cannot be written, such as a lock held for more than 10 seconds,
never fails `navarone`: it says so on one line on stderr, loses the events
of that write and exits with the code it would have had. The same holds for
the code store below. A reader skips a last line that has no line break
yet, since it is still being written, and a torn line, one that is no JSON
because a writer died mid-line, so no command breaks on it. An append after
a torn line starts a line of its own. `navarone events --json` also names
each torn line it skipped, as its entry says.
`NavaroneEventHandler` is the only code that writes the log and reads it
back, and every event type implements
`INavaroneEvent`.

Each line holds the event's `type`, its UTC time `at`, the `repo`, the
main checkout that every worktree of the repo shares, the `worktree`
`navarone` ran in, the `branch` checked out there and the full hash of
`head` at the time, then the event's own fields, among them the `id` when
the event is about one problem. Outside git, `repo` is the worktree, and
`branch` and `head` are null. Ids are granted per reasons.json, so an id
names one problem within one worktree: everything the log says of a
problem's history is read from the events of the same worktree path.

| type             | when                                                                 | fields                                  |
|------------------|----------------------------------------------------------------------|-----------------------------------------|
| `run`            | every `navarone` and `navarone --full-coverage`                      | `shown`, `tokens`                       |
| `raised`         | an id is granted, or a shown red problem is not open on the worktree | `id`, `check`, `symbol`                 |
| `fixed`          | an open problem has no finding in the code now and its code changed  | `id`, `check`, `symbol`, `before`, `after`, `secondRound`, `builder` |
| `resolved`       | `navarone <id> "<reason>"` takes the reason                          | `id`, `reason`, `defended`, `builder`   |
| `cost`           | `init`, `lens`, `evidence`, `duplicates`, `navarone <id> "<reason>"`, `keep`, `replace`, `large` and `settled` | `command`, `tokens` |
| `refused-fix`    | `navarone <id> replace <rule> "<finding>"` on a fixed problem        | `id`, `check`, `symbol`, `finding`, `before`, `after`, `rule`, `builder`, `validator` |
| `refused-reason` | `navarone <id> replace <rule> "<finding>"` on a problem resolved with a reason | `id`, `check`, `symbol`, `finding`, `before`, `rule`, `builder`, `validator` |
| `accepted`       | `navarone <id> keep` on a fixed or resolved problem                  | `id`, `builder`, `validator`            |
| `large`          | `navarone <id> large "<text>"`                                       | `id`, `check`, `symbol`, `text`, `code` |
| `settled`        | a run collects the user's judgement of a clash, or `navarone <id> settled` logs his decision on a large change | `id`, `check`, `symbol`, `user`, `words`, `code` |

- `shown` holds each problem the run showed, red or green, as `{"id",
  "check", "symbol", "state", "code"}`: `state` is `red` or `green`, and
  `code` is the hash of a red problem's subject code, left out for a green
  one. `tokens` is the run's cost: the characters it printed divided by
  four, rounded down.
- `cost` is the cost of a command that checks nothing, in the same tokens
  as a run: the characters it printed to stdout divided by four, rounded
  down. It is no `run`: "previous run" and `fixed` are read from runs only,
  and a `cost` takes no part in them.
- A problem is open on a worktree from a run that shows it red, with the
  code of that run, from a `refused-fix` or `refused-reason` of it, with the
  code refused: the fix's code after, or the code the reason defends, and
  from a `settled` with `user` replace, with the code he decided about. A
  `fixed` closes it. A run that shows it green closes it too, unless a
  refusal or a replace opened it, since a refused reason keeps the problem
  green until the builder changes its code.
- An open problem that has no finding now, and whose subject's code differs
  from the code it is open on, is `fixed`: the builder changed the code
  because of Navarone. A refused fix that left no subject stays open: there
  is no code before to show a later fix against.
- A shown red problem that is not open is `raised`, unless the run granted
  its id: it was green, or gone, or the worktree's runs never showed it,
  such as a problem resolved on the default branch whose code the change
  touches.
- `before` in a `fixed` is the code the problem is open on. `after` is the
  subject's code now, left out when the subject is gone. `secondRound` is
  true when a `refused-fix` or a `refused-reason` of the problem is logged
  on the worktree before it.
- `before` and `after` in a `refused-fix` are those of the last `fixed` of
  the problem. `before` in a `refused-reason` is the subject's code now:
  the code the reason defends.
- `builder`, `validator` and `user` are each side's position, `keep` or
  `replace`, the terms a clash is classed in: the builder replaces when
  it fixes and keeps when it gives a reason, and the validator keeps when
  it accepts and replaces when it refuses.
- `defended` in a `resolved` is the code the reason defends, as `{"code",
  "path", "startLine", "line", "context"}`: `code` is the hash of the
  subject's code, `path`, `startLine` and `line` are the file the subject
  is in, relative to its codebase's directory as a lens names it, the line
  its code starts on and the line of the finding, or the subject's first
  line when the problem has no finding now, and `context` is the lens that
  makes the subject a method's subject, left out for a subject a lens
  cannot show, such as a class. A line logged before Navarone logged it has
  none.
- `rule` in a refusal is the rule of NAVARONE.md the validator named, as
  `{"number", "text"}`: its number and its text without the number, the
  numbered line and the indented lines under it joined with single spaces.
  A line logged before a refusal named its rule has none.
- `code` in a `large` is the hash of the subject's code.
- `words` in a `settled` is what the user said with his decision, left out
  when he said nothing. `code` is the hash of the subject's code he decided
  about, the code a run found his answer under or the code as it was when
  `settled` logged it, left out when the subject was gone.

### The code store
An event never holds code: it names it by a hash. The code is the text of
the problem's subject, the method or the class as it stands in its file,
never the whole file. It is stored once per hash as `code/<hash>` in the
data folder, so identical code is stored once and the log stays small and
fast to read. The hash is the SHA-256 of the code's UTF-8 text, in lower
case hex. Git cannot hold this code: events are written in worktrees whose
unmerged code git later prunes. A piece is written whole under a name of
its own and then moved into place, so a reader never sees half of it.
`navarone code <hash>` prints it.

## Large changes
A signal often shows where a missing design is patched over in a leaf.
When the real fix is a large change, bigger than the task, the builder or
the validator logs it: `navarone <id> large "<text>"`, where the text says
what is wrong, where it shows and what the fix is. A large change flows
through the open agent, the one the user talks with, never through brain's
window.

- The large change is the problem's answer. Navarone stores the text as
  the problem's resolution, with the code as it is now, so the problem is
  green and the task it came from lands. A fixed problem, which has no
  finding now, needs no resolution. A large change holds nothing red and is
  no question in `navarone disputes --json`.
- The agent that logs it names it in its report to the open agent, who
  brings it to the user. When the user decides for the large change, the
  open agent may send a second builder for it, and that builder logs his
  decision: `navarone <id> settled replace "<his words>"`. A no is logged
  by an agent still working in the worktree of the large change:
  `navarone <id> settled keep "<his words>"`. The words may be left out.
- `settled` takes a problem that a `large` of the same repo names, in any
  of its worktrees, so the second builder logs it in its own. It logs a
  `settled` with the check and symbol of the latest large change of the
  problem and the hash of the subject's code as it is now. A replace opens
  the problem on that code on the worktree, as a refusal does, so the large
  change's fix is logged as the usual `fixed` once its finding is gone and
  its code changed. A keep with words makes the problem's resolution the
  agent's text and then "The user adds: <words>", for the code as it is
  now; a keep without words leaves the agent's text as the resolution.

## Clashes
A clash is the builder and the validator not agreeing on how to answer a
signal. The validator refused the builder's answer with
`navarone <id> replace <rule> "<finding>"`, and the builder holds its
position: it answers with a new reason, `navarone <id> "<reason>"`, while
the subject's code is unchanged since the refusal. Navarone reads clashes
from the event log of the worktree; there is no command for one. The user
is the judge, in brain's window, never through the open agent. A clash is
an escalation, the only one: it reaches the user's bench.

- A refusal stands from its `refused-fix` or `refused-reason` until a
  `fixed` of the problem, a `resolved` whose code differs from the code
  refused, an `accepted`, or a `settled`. A `resolved` whose defended code
  is the code refused, the fix's code after or the code the refused reason
  defends, is a clash. A refusal the builder answers by changing the code is
  no clash, and the validator's keep ends a clash: the two agree. A refusal
  logged without its rule, or one of a fix that left no subject, starts no
  clash. A new clash of the same problem takes the place of the one before.
- A clash holds its problem red, whatever its finding says and whatever
  the coverage, until the user's judgement is collected: `navarone` prints
  it with the finding's first sentence and exits 1. A run shows it red,
  with the hash of its subject's code now, or none when the subject is
  gone. While a clash waits, a run that shows its problem neither opens nor
  closes it on the worktree. There is no toggle in navarone.json yet.
- `navarone disputes --json` lists each clash not yet judged, on every
  worktree the log names that still exists, read from the event log alone.
  Each is a bench of brain's generic blocks, read top to bottom: the
  `title` is the text of the rule the validator named; the blocks are what
  is wrong, the finding's first sentence; the code, as a diff of the code
  before the refused fix and the fix, or for a refused reason the code the
  reason defends with its path and start line; the builder's reason and
  the validator's finding, each in full; and last a facts block of details:
  the check, the symbol, the problem's id, the rule's number, the worktree
  and the hashes. The choices are `keep`, the builder is right, and
  `replace`, the validator is right. `context` is the lens of the defended
  code, left out when the subject is gone; `focus` holds the finding's
  line; `repo` is the worktree, where brain runs the context; `group` is
  the worktree's branch, or the worktree outside a branch; `tags` hold the
  check; `created` is when the builder's reason was logged; and `stale` is
  true once the last run showed the subject's code changed since the
  builder defended it.
- A question's id is the problem's id, a dash and the first 16 hex
  characters of the SHA-256 of the worktree, the problem's id, the time of
  the clash and the hash of the subject's code the question shows. brain
  stores an answer per question id and never deletes one, and Navarone
  grants ids per worktree, so the id is unique across repos and worktrees,
  new for each clash and new once the code changes.
- brain's window stores the answer in
  `cache/answers/navarone/<question id>.json` under brain's home,
  `BRAIN_HOME` or brain under the user's data home, as
  `{"id", "choice", "words", "answeredAt"}`. Navarone reads it there and
  never writes in brain's folder. Without brain no question is shown, and
  a clash stays red.
- The next `navarone` run in the worktree collects the answer: it looks up
  the question id for the subject's code as it is now, and logs a `settled`
  with `user` keep or replace and his words. An answer whose subject's code
  changed after it was asked is under another question id, so it is never
  collected: the question is listed again, stale. A file that is no answer
  Navarone can read, or a choice other than keep and replace, is no answer.
- `keep` stores the builder's reason as the problem's resolution, with the
  code as it is now, in the same check that collects it, so the problem
  turns green. A problem with no finding now needs no resolution.
- `replace` keeps the problem red, with the validator's finding and the
  user's words after it, while its subject's code is the code he answered
  about, and opens the problem on that code. A replace about a subject that
  was gone holds nothing. The builder changes the code, and when the
  finding is gone and the code changed, the usual `fixed` is logged.

## The lens
A lens shows a method with the context an agent, or the user, needs to
read it. The engine builds it from the facts the provider hands it and
never reads the language itself.

    navarone lens method DeterministicProblemClassifier.FalsePositive callers:2 [expand:1] expand:1 class:short

The lens is a small language with its own lexer, parser and execution:

    lens  = kind name (":" word)? view*
    view  = step ("[" view* "]")?
    step  = word (":" arg ("," arg)*)?
    arg   = word (":" word)?

`:`, `,`, `[` and `]` are a token each, white space separates, and every
other run of characters is a word. Nothing is quoted: Navarone joins its
arguments after `lens` with spaces and lexes them as one text, so spaces in
and around brackets do not matter. A bracket pair with no space inside,
such as `[expand:1]`, is a bash glob pattern; bash passes it on as written
unless a file in the current directory matches it, and the parser accepts
it.

The subject is a kind and a name. Only the kind `method` is built: `class`
and `module`, a csproj, are known words and a usage error that says they
are not built yet. A method is named `Type.Method`: the type's own name,
without its namespace or the types it is nested in, and the method's name
without its parameters. A name that matches several methods, such as
overloads, shows each in turn, in file and line order, each with every
view. The name takes data after a colon, as a view does: the number of one
overload, from 1, in file and line order, so `Shop.Price:2` is the second
`Price`. Nothing is quoted, so the number is how a user picks one overload
without writing parameters bash would take. A single method's `:1` is
valid. A number that is not a whole number from 1, or that no overload has,
is a usage error with the caret under the number.

Every view is an operation on the subject, not a chain: `expand:1`
expands the subject's calls whatever came before it. A view's data follows
a colon, several values separated by commas. A value alone is the view's
parameter at its place; a value written `name:value` names its parameter.
A parameter not given takes its default. A view's own views follow it in
brackets and apply to each of its results, to any depth.

| View      | Parameters                     | Shows                                                  |
|-----------|--------------------------------|--------------------------------------------------------|
| `callers` | count, default 1; lines, default 3 | at most count call sites of the method, each under its calling method's name, with the calling line and lines lines above and below it, inside the calling method. Its views apply to the calling method. |
| `expand`  | depth, default 1               | the full code of every method in the codebase the method calls, then the methods those call, to depth, each once and never the method itself, each with the method that calls it. A call outside the codebase, such as into the framework or LINQ, is not expanded. Its views apply to each expanded method. |
| `class`   | form, `short` the one built    | the declaring type's name, the signatures of its fields, properties, constructors and methods in file and line order, and the declaration of each of the type's fields and properties the method uses. It takes no views. |

Call sites are the calls that resolve to the method itself, in file and
line order; a call through an interface or base member it implements is
not among them.

The output is markdown, or JSON with `--json`, as "One page, two outputs"
says. The markdown's first line, before any subject heading, names
the version of the code it shows: `At commit <full hash of HEAD>.` in the
repo the lens runs in. What anyone decides about code holds only for the
version it was decided on, so a lens names its version. When
the working tree differs from HEAD, in a tracked or an untracked file, the
line adds `with uncommitted changes, so the code shown is the working
tree, not the commit`, and a bullet list of the changed files' paths,
relative to the repo, follows it. Outside a git repository, or in one with
no commit yet, the line says so instead and the lens still works. Reading
HEAD and the status is all the lens asks of git.

Then the subject, as `# method Type.Method(params), overload N of M`, where
M counts every overload the name matches and N is the subject's number, with
its code, then one section per view under it, headed by the view as written,
in the order written. A view's results are sections under the view's, and
its own views are sections under each result. Sections nest without limit;
the markdown heads each one level deeper than the section it sits under,
from `#` for the subject, and markdown has six levels, so a deeper one stays
at six. Every piece of code is a code block in the provider's language,
under a line with its path and start line. The members of `class:short` are
one block under the type's path and start line.

No code is printed twice in one output. A method's full code, as the
subject or under `expand`, and a type's `class:short` outline, are printed
the first time. Wherever they would come again the heading stays and one
line under it says `Shown above at `path:line`.`, with the path and start
line of the earlier block. This holds at any depth of nesting. A caller's
excerpt around its calling line is not a full method and is always
printed, and a `class:short` still lists the fields and properties its
own method uses.

### One page, two outputs
The engine first builds what a lens shows as one page: a tree of sections,
each with its blocks and the sections under it, and marks on the code. The
markdown and the JSON are both written from that page, so the two never
drift. A section is a heading: the subject, a view, or one of a view's
results. Its blocks are a line of text, a piece of code with its path and
start line, or code shown above. The markdown writes a section as its
heading and its blocks as above, and writes no mark.

`navarone lens --json` prints `{"commit", "sections"}`. `commit` is the full
hash of HEAD, or null outside a git repository or in one with no commit yet.
Each section is `{"id", "title", "open", "blocks", "sections"}`: `id` is
unique in the output, `title` is the heading's text without its `#`s, `open`
is true for the subject, and `sections` holds the sections under it, left
out where there are none. `sections` at the top holds the subjects. The tree
is the nesting; no section carries a depth. So `expand:1 [class:short]` is a
section whose sections are the expanded methods, each with its `class:short`
section under it. A line is a text block, `{"type": "text", "text"}`, and
code a code block, `{"type": "code", "path", "startLine", "code", "marks"}`,
as brain's window takes them.

A mark covers one line of code, from its first character to its last that
is not white space, with brain's columns: UTF-16 code units from 1, the end
one past the span. It holds `start`, `end`, a `label`, a `weight` of 0,
since a lens counts nothing, and a `note`. A line of a method's code that
calls methods in the codebase a lens can name is a mark labelled `call`
whose `queries` hold one `{"label", "query"}` per method, in the order
called: the label is the method's name in a lens, `Type.Method` with `:N`
where the name matches several overloads, and the query is the lens that
makes it the subject: `method <Type.Method[:N]> class:short` for a private
method, with ` callers:3` after it for any other, because a method its type
alone may call is read with its class, and any other is part of its class's
API. Calls are known
by line only, so a line with several calls carries several queries, and
brain shows a menu. The calling line of a caller's excerpt is labelled
`caller` and offers the calling method first, then what that line calls.
Code shown above is a code block of its first line, at its path and start
line, with one mark labelled `shown above` whose `section` is the id of the
section that shows it, and no query.
