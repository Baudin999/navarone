# Navarone.Cli

The `navarone` command. Agents may run it themselves. It is one command, with or without an id.

## `navarone`

Checks the repo it runs in through the engine with the C# provider. It first collects the user's judgements of clashes on this worktree, as `navarone <id> keep` and `navarone <id> replace` below say, and prints each problem a clash holds red, whatever the change. The repo root is the nearest directory up from the current one that holds `.git`. Navarone analyses every `.sln` and `.slnx` anywhere under that root, skipping `bin`, `obj` and hidden directories, each solution on its own with all the projects in it, and reports a project two solutions share once, with one id per problem. A repo with no solution anywhere under it, or a `navarone.json` or `reasons.json` Navarone cannot read, such as a limit for a check that does not exist or an unknown key, is a usage error, which exits 2. What `navarone.json` ignores is gone from this command and every other: the provider reads no fact from it.

Each finding is matched against `reasons.json` at the repo root. A new problem is granted an id and recorded there; a problem whose code is gone is removed. All five checks of SPEC.md are wired in.

The deterministic classifier classifies every problem on every run, so a changed rule reaches the problems classified before. A `cyclomatic` or `cognitive` problem on a flat dispatch is a false positive, and so is a `control-coupling` problem on a mapping, a flat dispatch whose every branch only returns a value, and an `lcom4` problem on a value class. The C# provider decides what is a flat dispatch, a mapping and an accessor through Roslyn. A false positive is green without a reason, and the agent is never asked about it.

Prints each red problem in the code the current change touches as a question with its id, its check id, its resolved symbol and where it is:

    0000001 cyclomatic Compiler::Compiler.Parser.Parse(Token) at Compiler/Parser.cs:42: cyclomatic complexity 13 is above the limit of 10. Why does this method need 13 paths?

A `split-complexity` question ends with its helper ledger: each helper that adds a decision, with the decisions it adds. These are the helpers a reason must name. They come from the same computation that gives the check its value.

The change is the diff between the working tree and the merge-base of HEAD with the default branch: the local branch origin/HEAD names, else that remote branch, else `master`, else `main`. Staged, unstaged and untracked work is part of it. A changed line opens every signal of the method or class it sits in, and a changed helper the split-complexity signal of the method it helps. SPEC.md, "The change", holds the rule. With no change it prints nothing and exits 0. Outside git, or when no such branch shares a commit with HEAD, it is a usage error, which exits 2 and names `--full-coverage`. Every problem is still found, granted an id and recorded either way.

A problem is green while its resolution answers its code as it is now and the code carries its id. Exits 0 when every problem it prints is green, 1 while any is red.

Each run is logged to Navarone's event log with the problems it showed and its cost in tokens, the characters it printed divided by four, after the `raised` and `fixed` events it made happen. SPEC.md, "The event log", says when each is written.

## `navarone --full-coverage`

Works as `navarone` over every problem in the repo, whatever the change, and needs no git.

## `navarone init`

Writes `navarone.json` at the repo root, the nearest directory up that holds `.git`, with the test projects ignored, so a project such as `Navarone.Tests` is ignored from the start:

    {
      "ignore": {
        "projects": ["*.Test", "*.Tests"]
      }
    }

It adds `.navarone/`, the facts cache, to the repo's `.gitignore`, creating the file when there is none. It writes no other file and needs no solution. A `navarone.json` that is there already is left untouched, whatever it holds, and so is a `.gitignore` that ignores the folder already, and init says so of each on one line. It writes a `cost` event. Exits 0 either way.

## `navarone <id> "<reason>"`

Checks the repo as `navarone` does, then hands the reason for the problem with that id to the deterministic reason classifier. It rejects a reason when the problem has a finding and its code carries no `// navarone: <id>` comment, when it has fewer than 8 words, when it repeats a resolution in `reasons.json` word for word, or when it answers a `split-complexity` problem without naming every helper that adds a decision. A rejected reason is printed with why and stored nowhere, and the command exits 1:

    0000001 rejected: the reason has 3 words; give at least 8

Any other reason is stored as the problem's resolution, with the fingerprint and value of the code it answered, in place of the resolution before it, and the command exits 0:

    0000001 resolved: the problem stays silent while its code is unchanged

Navarone does not weigh the reason: the validator reviews it. A stored reason is logged as a `resolved` event with the code it defends. A fixed problem, which has no finding now, takes a reason only while the validator's refusal of its fix stands on this worktree: the reason argues for the fix, so it is logged and stored nowhere, and its code need not carry its id. Any other id that matches no problem in the code as it is now is a usage error, which exits 2.

## `navarone <id> keep` and `navarone <id> replace <rule> "<finding>"`

The validator's position on the builder's answer to a problem. `keep` accepts the fix or the reason and logs an `accepted`:

    0000001 keep: the validator accepts the fix

`replace` holds that the code must still change. The rule is the number of the rule of NAVARONE.md the code breaks, under "The rules" in the NAVARONE.md Claude Code loads, `rules/NAVARONE.md` under `CLAUDE_CONFIG_DIR` or `~/.claude`, and the finding says why, opening with one sentence on what is wrong. It logs a `refused-fix` with the code before and after the fix, or a `refused-reason` with the code the reason defends, each with the rule's number and text:

    navarone 0000001 replace 3 "The condition crams three tests into one line. Name each test, then branch on the names."
    0000001 replace: the validator refuses the reason; the code must still change

Both exit 0 once logged and change neither `reasons.json` nor a resolution. `keep` and `replace` are reserved: a reason that is exactly one of them is no reason. A `replace` without a finding, with a blank one, or without its rule, a rule that is no number of a rule there, and a NAVARONE.md that cannot be read, are usage errors, which exit 2. An id Navarone never granted, a problem still red, a false positive, or a problem with no finding now and no fix logged on this worktree, is a usage error too: there is nothing to accept or refuse. A fixed problem whose finding is gone from the code is found through its `fixed` event.

When the builder answers a `replace` with a new reason while the subject's code is unchanged, the two clash. From then on `navarone` prints the problem red, whatever the change, and exits 1:

    0000001 cyclomatic <symbol> at src/A.cs:3: the builder and the validator clash, red until the user judges it in brain's window: <the finding's first sentence>

The next `navarone` run collects the user's judgement from brain's `cache/answers/navarone`, under the question id for the subject's code as it is now, and logs a `settled`. Keep stores the builder's reason as the problem's resolution, so it turns green. Replace keeps it red with the validator's finding and his words while its code is the code he answered about, until the builder's fix is logged. An answer about code that changed since is never collected, and the question is listed again as stale. The validator's `keep` ends a clash. A builder that changes the code instead of giving a new reason does not clash. SPEC.md, "Clashes", holds the rules.

## `navarone <id> large "<text>"`

The real fix of the problem is a large change, bigger than the task. The text says what is wrong, where it shows and what the fix is. Navarone checks the repo as `navarone` does, stores the text as the problem's resolution, so the problem is green and the task lands, and logs a `large` with the text and the subject's code:

    0000001 large: the large change is the problem's answer; name it in your report to the open agent, who brings it to the user

Exits 0 once logged. A blank text, an id Navarone never granted, a problem whose subject is gone, or a problem with a finding now whose code carries no `// navarone: <id>` comment, is a usage error, which exits 2. `large` is reserved: it is no reason.

## `navarone <id> settled keep|replace ["<words>"]`

Logs the user's decision on a large change, which the open agent brought him, with his words, as a `settled` with the subject's code as it is now. The second builder the open agent sends for the large change logs his replace in its own worktree, and an agent still working in the large change's worktree logs his keep:

    0000001 settled: the user's replace is logged

A replace opens the problem on that code, so the large change's fix is logged as `fixed` once the finding is gone and the code changed. A keep with words makes the problem's resolution the agent's text and then "The user adds: <words>"; without words the agent's text stays. Exits 0 once logged. A choice other than `keep` and `replace`, a problem no `large` of this repo names, and a problem in a clash on this worktree, are usage errors, which exit 2: the user judges a clash in brain's window. `settled` is reserved: it is no reason. SPEC.md, "Large changes", holds the rules.

## `navarone events --json`

Every event in Navarone's log as one JSON line, `{"source":"navarone","events":[...]}`, each event as its line of the log holds it, for brain to read. It writes nothing and works from any directory. A line of the log that is no event Navarone knows is a usage error that names its file and line. SPEC.md, "The event log", holds the events.

## `navarone code <hash>`

Prints the code stored under the hash an event names, as it was written. It writes nothing and works from any directory. A hash no code is stored under is a usage error, which exits 2.

## `navarone disputes --json`

The questions brain's window lists, as one JSON line, `{"source":"navarone","questions":[...]}`: one bench per clash not yet judged, on every worktree the log names that still exists. Each carries the rule the validator named as its title; then as its blocks what is wrong, the finding's first sentence, the code before and the fix as a diff or the code a refused reason defends, the builder's reason and the validator's finding in full, and the details last; the choices `keep` and `replace`; the check as its tag, the worktree as its repo, its branch as its group, the subject's lens as its context with the finding's line in focus, and `stale` once the code changed. It reads the event log and the code store, writes nothing and works from any directory. brain runs it from `sources.tsv`, which `install.sh` writes.

## `navarone evidence <id> --json`

Prints the evidence document of the problem with that id, from the code as it is now, so it can be checked by hand. Navarone analyses the repo as `navarone` does and builds the document from Roslyn alone. It grants no id and writes only its `cost` event, as SPEC.md "The event log" says:

    {"code":{"path":"src/A.cs","startLine":3,"endLine":7,"code":"…"},
     "finding":{"check":"cyclomatic","symbol":"<symbol>","value":4,"limit":10,"base":1},
     "proof":[{"span":{"startLine":5,"startCol":9,"endLine":5,"endCol":32,"text":"if ((a || b) && c != 12)"},
               "kind":"if","adds":1,"why":"a branch adds one path",
               "parts":[{"span":{…,"text":"(a || b) && c != 12"},"kind":"&&","adds":1,"why":"…","parts":[{…,"kind":"||",…}]}]}]}

Lines and columns are positions in the file, from 1, and `endCol` is the column of the span's last character. The value is the base plus every `adds` in the tree. A construct the rule does not count, such as an `else`, a `when` or a `default`, shows with `adds` 0 and why. Only cyclomatic builds evidence for now.

Exits 0 once printed. An id that matches no problem in the code as it is now, or a problem whose check builds no evidence, is a usage error, which exits 2.

## `navarone lens method <Type.Method> <view>...`

Prints what the lens shows of the method in the repo it runs in, as markdown. The words after `lens` are joined with spaces and read as one lens, so nothing is quoted. SPEC.md, "The lens", holds the language and what each view shows. It analyses no check, grants no id and writes only its `cost` event, so an agent may run it:

    navarone lens method DeterministicProblemClassifier.FalsePositive callers:2 [expand:1] expand:1 class:short

Exits 0 once printed. A lens that does not parse, a kind or view not built, a value a view does not take, or a name that matches no method, is a usage error, which exits 2, with what is wrong and the lens with a caret under where it is:

    navarone: expected a view, or ] to close the [ at column 26, not the end of the lens, at column 35 of the lens
      method T.First callers:2 [expand:1
                                        ^

## `navarone lens --json method <Type.Method> <view>...`

The same lens as one JSON line for brain's window, which runs it with a lens's words as separate arguments, never through a shell. The engine builds one page, a tree of sections with their blocks and marks, and writes both outputs from it, so the two never drift. A section holds the sections under it in `sections`, left out where there are none, to any depth: a view's results sit under the view, and a result's own views under the result. SPEC.md, "One page, two outputs", holds the shape:

    {"commit":"<full hash of HEAD>","sections":[{"id":"s1","title":"method Shop.Price(int), overload 1 of 2","open":true,"blocks":[{"type":"code","path":"Shop.cs","startLine":8,"code":"…","marks":[{"start":{"line":10,"col":9},"end":{"line":10,"col":32},"label":"call","weight":0,"note":"…","queries":[{"label":"Shop.Base","query":"method Shop.Base class:short"}]}]}],"sections":[{"id":"s2","title":"class:short","open":false,"blocks":[],"sections":[…]}]}]}

`commit` is null outside a git repository or in one with no commit yet. It exits as `navarone lens` does.

## `navarone duplicates [--similarity <share>] [--tokens <count>]`

Prints every clone pair in the repo it runs in at or above the floor, as markdown: research toward a duplication check, not a check yet. It raises no finding, grants no id and writes only its `cost` event. The floor is similarity 0.7 and 30 tokens unless the options set it. Each pair shows its similarity, its size in tokens, its share of same names, and both pieces as file and line range with the method each lies in and its code, a line that holds a token the other piece does not match marked `~`. `src/Navarone.Engine/Duplication/README.md` says how pairs are found:

    navarone duplicates --similarity 0.8 --tokens 50

Exits 0 once printed. An unknown option, an option given twice or without a value, a similarity outside 0 to 1 or a token count under 1 is a usage error, which exits 2.

## What lives here

- `Program.cs`: hands the arguments, the current directory, the event log in Navarone's data folder, the wiring and the console to `Commands`.
- `Commands.cs`: runs `navarone`. It reads the arguments as a command through `Grammar`, runs it, and logs its cost once, for a form that logs one and did not end in a usage error. A `navarone.json` or `reasons.json` Navarone cannot read prints one line on stderr and exits 2.
- `Grammar.cs`: the grammar. It reads the arguments as one form and returns its command; arguments of no form, and a `replace` without its rule, are usage errors.
- `Command.cs`: the command value: the form's name, which is the name its cost is logged under, whether it logs a cost, and whether it needs the repo. An `InRepo` wires the engine by the function it is given and finds the codebases once; a repo with no codebase under its root prints one line on stderr and exits 2. An `Anywhere` runs from any directory.
- `CheckCommands.cs`: `navarone` and `navarone --full-coverage`.
- `InitCommand.cs`: `navarone init`.
- `IdAnswers.cs`: the answers to a problem by its id: a reason, `keep`, `replace`, `large` and `settled`.
- `ResearchCommands.cs`: `lens`, `evidence` and `duplicates`.
- `BrainCommands.cs`: `disputes`, `events` and `code`, which brain runs.
- `Wiring.cs`: wires the provider with what `navarone.json` ignores, the checks, their limits, the deterministic classifiers, the event log, brain's answers and the path of the NAVARONE.md Claude Code loads into the engine.
- `ExitCodes.cs`: what the command exits with.
- `CountingWriter.cs`: a writer that passes everything to another writer and counts the characters it passed, which is what a form's cost is measured from.
- `Tokens.cs`: the cost in tokens of the characters a command printed, one token for every four characters, rounded down.
- `FileLocations.cs`: where the CLI finds the repo root and the codebases under it, through the provider's `CanLoad`.

Analysis does not live here. The CLI only wires the parts into the engine and prints what it returns.
