# Navarone

Navarone is a static analyzer for code that AI agents write. The agent runs it after every change, and Navarone points at the methods and classes that have grown hard to read: too many paths, too deep, too loosely held together, steered by a flag, or split into helpers to hide how complex they are. It reports signals, never verdicts: for each one the agent fixes the code or says why the code is right, and Navarone remembers the answer.

Only C# is analysed for now, through Roslyn. Navarone is under the Apache License 2.0; see [LICENSE](LICENSE) and [NOTICE](NOTICE).

The README has two parts: [For humans](#for-humans), on what Navarone is, how to install it and how to work on it, and [For LLMs](#for-llms), on how an agent works with it.

# For humans

## Why Navarone

An agent writes code fast, and it writes the same slop each time: the long method with a dozen branches, the class that is three classes, the boolean parameter that picks which half of a method runs. A linter flags these and the agent silences it. A metric gets gamed: split the method into five private helpers and every piece passes while the whole is as tangled as before.

Navarone is built against both. A signal sends the agent to look, and the agent has to answer it: fix the code, or put a reason on record that a reviewer, human or agent, can read and refuse. A reason holds only while the code it answered is unchanged, so the next change to that code asks the question again. And split-complexity counts a method with its helpers inlined, so splitting a method scores the same as not splitting it.

## The checks

Each check raises a finding only above its limit. `navarone.json` at the repo root may lower a limit, never raise it, so an agent cannot loosen the gate.

| Check            | On      | Counts                                                  | Limit |
|------------------|---------|---------------------------------------------------------|-------|
| cyclomatic       | method  | decisions, plus one (McCabe)                            | 10    |
| cognitive        | method  | breaks in linear flow, weighted by nesting (Campbell)   | 15    |
| lcom4            | class   | connected groups of the class's methods and fields      | 1     |
| control-coupling | method  | parameters that steer which branch runs                 | 0     |
| split-complexity | method  | cyclomatic with the method's helpers inlined            | 10    |

Some findings are certain false positives, and Navarone keeps them quiet without asking: a flat dispatch under cyclomatic or cognitive, a mapping under control coupling, and a value class under LCOM4. [SPEC.md](SPEC.md) defines every check exactly.

## How a signal is answered

1. Find: a check raises a finding above its limit, and Navarone gives the new problem a seven-digit id.
2. Show: `navarone` prints each red problem in the code the current change touches, as a question with its id. A changed line opens every signal of the method, class or module it sits in, the old ones included, so the code is left better than it was found.
3. Answer: the agent fixes the code and runs `navarone` again, or it puts the id in a comment at the code, `// navarone: <id>`, and gives a reason with `navarone <id> "<reason>"`.
4. Check the reason: Navarone refuses a reason when the code does not carry its id, when it has fewer than 8 words, when it repeats an earlier reason word for word, or when it answers a split-complexity problem without naming every helper that adds a decision. Navarone stores every other reason, with the code it answered, and the problem is silent while that code is unchanged.

Navarone does not judge whether a reason is good. That is a reviewer's job. In the workflow it was built for, one agent builds and a second agent, the validator, reviews the work against a written taste, `rules/NAVARONE.md`: it accepts each fix or reason with `navarone <id> keep`, or refuses it with `navarone <id> replace <rule> "<finding>"`. When the builder holds its position against a refusal, the two clash, and the human decides. When the real fix is bigger than the task, an agent logs it as a large change, so the task can land and the human decides on the large change later.

Navarone writes `reasons.json` at the repo root and an agent never edits it: it holds every problem by id with its check, symbol, fingerprint, value and resolution. Commit it with the code.

## Navarone and brain

Navarone was built alongside brain, its author's system for working with agents. brain gives the human a window where clashes and large changes wait for a decision, and shows what Navarone cost and what it changed, from Navarone's event log.

Navarone runs without brain: every check, id, reason, `keep`, `replace` and `large` works the same. The one thing that needs brain is judging a clash: without it, a clash stays red. If you work with a single agent and no validator, no clash ever happens.

## Install

You need the .NET SDK (see `global.json` for the version) and bash. Then run, yourself:

    ./install.sh

Before it changes anything, it prints every path it will change and asks `Continue? [y/N]`. No is the default: anything but y or yes changes nothing. It changes:

- `~/.local/lib/navarone`: the CLI, built from the checkout with `dotnet publish`, outside the checkout so cleaning it leaves Navarone working.
- `~/.local/bin/navarone`: a link to that CLI. Put `~/.local/bin` on your PATH, or set `LOCAL_BIN` to a directory that is.
- `~/.claude/rules/NAVARONE.md` (under `CLAUDE_CONFIG_DIR` when that is set): a link to `rules/NAVARONE.md` in the checkout. Claude Code loads every file in that folder into every session in every project, so every Claude Code agent on the machine then works by Navarone's process and its author's taste in code. Read [rules/NAVARONE.md](rules/NAVARONE.md) before you say yes. Delete the link to undo it, or put your own file there: a file that is not a link is left alone.
- brain's `sources.tsv`, only when brain is installed: the line that lets brain show Navarone's questions.

Running it again publishes again and changes nothing else. To use Navarone in a repo, run `navarone init` at its root, then `navarone` after each change.

## Working on Navarone

[SPEC.md](SPEC.md) is the spec, and it wins where the code or a README disagrees. Each project under `src/` has a README on what it does and what is still open.

- `SPEC.md`: the spec.
- `install.sh`: installs `navarone` and links `rules/NAVARONE.md`.
- `rules/NAVARONE.md`: the taste in code and the process for builder and validator agents.
- `navarone.json`: what Navarone ignores when it checks this repo: the tests.
- `reasons.json`: Navarone's record of its own problems and their answers, since Navarone checks itself.
- `src/`: the parts of Navarone, one project each.
- `tests/`: the one test project. `dotnet test` at the repo root runs it.
- `docs/brain/`: the project's design notes: what we want and why.
- `Navarone.slnx`: the solution.
- `Directory.Build.props`: the gate. Every project builds with zero warnings, and a warning is an error.
- `Directory.Packages.props`: every package version, in one place.

The engine knows no language. A language provider knows one language and hands the engine facts. A check computes over those facts and never sees a language. A classifier rules on problems and reasons and never sees a language either. References point inward: every project references the engine, and the engine references none of them. C# is read through Roslyn only: there is no tree-sitter anywhere, no package, no fallback, no flag.

# For LLMs

You are an agent changing code in a repo that holds a `navarone.json` at its root. Navarone is how you show that the code you touched is readable. Read this part before your first change.

## The loop

1. Make your change.
2. Run `navarone` at the repo root. It prints each red problem in the code your change touches, as one line: its id, check, symbol, place and a question. It exits 0 when nothing it prints is red, 1 while anything is, 2 on a usage error.
3. Answer every printed problem, the ones that were there before your change included. You touched that method, class or module, so its signals are yours.
   - Fix the code, then run `navarone` again. Prefer this.
   - Or, when the code is right as it is, put `// navarone: <id>` in a comment at the code and run `navarone <id> "<reason>"`. A reason says why this code is right, in at least 8 words, and is never a copy of another reason. For a split-complexity problem, the reason names every helper the question lists.
   - Or, when the real fix is a change bigger than your task, run `navarone <id> large "<what is wrong, where it shows, what the fix is>"` and name it in your report.
4. Repeat until `navarone` exits 0.

## Rules

- Never edit `reasons.json`. Navarone writes it. Commit it with your change.
- Never raise a limit in `navarone.json`; Navarone refuses one above its default. You may add an ignore for generated code or test projects.
- A signal is a reason to look, not the answer. Lowering a number is not the goal: readable code is. Splitting a method into helpers does not lower split-complexity, because it counts the helpers inlined.
- An id belongs to one problem. Copying an id onto other code answers nothing.
- When `rules/NAVARONE.md` is loaded, its rules are the taste a reviewer holds your code to. A reviewer refuses an answer by rule number.
- If you are the validator, record your position on every signal you review: `navarone <id> keep`, or `navarone <id> replace <rule> "<finding>"`, the finding opening with one sentence on what is wrong.
- A problem in a clash stays red until the human judges it. Do not try to answer it again with the same code.

## The CLI

One command. Agents may run it themselves.

- `navarone`: checks the repo it runs in, grants ids to new problems and prints each red problem in the code the current change touches as a question with its id. The change is the diff between the working tree and the commit where the branch left the default branch, uncommitted and untracked work included. A changed line opens every signal of the method, class or module it sits in, old ones included, because we leave the code better than we found it. Exits 0 when every problem it prints is green or it prints none, 1 while any is red. With no change it prints nothing.
- `navarone --full-coverage`: the same over every problem in the repo. Both modes find, grant and record every problem; they differ only in what they print and what the exit code counts.
- `navarone init`: writes `navarone.json` at the repo root with the test projects ignored, `*.Test` and `*.Tests`, adds `.navarone/` to the repo's `.gitignore`, and writes no other file. A `navarone.json` that is there already is left untouched, and so is a `.gitignore` that ignores the folder already, and init says so. Exits 0.
- Every command that reads the code keeps its facts in `.navarone/` at the repo root, keyed by HEAD, the files that differ from it, navarone.json and the build of Navarone, so the next command at the same version of the code skips loading the solutions. SPEC.md, "The facts cache", says how.
- `navarone <id> "<reason>"`: an id is seven digits, and a first word that is no command and no id is a usage error, exit 2, never a reason. Stores the reason as that problem's resolution, unless a rule rejects it. Exits 0 once stored, 1 when rejected.
- `navarone <id> keep` and `navarone <id> replace <rule> "<finding>"`: the validator's position on the builder's fix or reason, keep when it accepts, replace when the code must still change. A replace names the rule of NAVARONE.md the code breaks by its number there, and its finding opens with one sentence on what is wrong; a replace that names no rule is a usage error. Navarone logs it and changes neither `reasons.json` nor a resolution. A builder that answers a replace with a new reason while the code is unchanged clashes with the validator: the problem is red, whatever the change, until the user judges it in brain's window, and the next `navarone` run collects his answer. Keep makes the builder's reason the resolution; replace keeps the problem red with the validator's finding and his words until the code changes. An answer about code that changed since it was asked is dropped, and the question comes back as stale.
- `navarone <id> large "<what is wrong, where it shows, what the fix is>"`: the real fix is a large change, bigger than the task. Navarone stores the text as the problem's resolution and logs it, so the problem is green and the task lands. The agent names it in its report to the open agent. `navarone <id> settled keep|replace ["<words>"]` logs the user's decision on it: the second builder the open agent sends for the large change logs his replace, and an agent still in the worktree logs his keep, whose words are added to the resolution.
- `navarone events --json`: what Navarone made happen and what it cost, from its event log, as one JSON document for brain. `navarone code <hash>` prints the code an event names by its hash. Both write nothing and work from any directory. Every other command an agent reads, `init`, `lens`, `evidence`, `duplicates`, a reason, `keep`, `replace`, `large` and `settled`, also writes a `cost` event: its name and its tokens.
- `navarone disputes --json`: the questions brain's window lists, `{"source":"navarone","questions":[...]}`, one bench per clash the user has not judged yet, read from the event log. It writes nothing and works from any directory.
- `navarone evidence <id> --json`: prints the problem's evidence document from the code as it is now, so it can be checked by hand: the code block, the finding with its value, limit and base, and the proof tree that adds up to the value. Navarone builds it from Roslyn alone. Only cyclomatic builds evidence for now. It writes only its `cost` event.
- `navarone map --json`: the code as a map for brain's window, as one JSON document: the version of the code, every file with the methods a lens can name and their lines, and every red problem with the method it sits in. It writes nothing, not even a `cost` event or an id.
- `navarone classes <path> --json`: the class view of one source file for brain's window, as one JSON document: every type in it with its assembly, namespace and lines, and each member with its kind, whether it is public, its lines, its name in a lens and the lcom4 piece it belongs to, with whether the class's lcom4 is above its limit. SPEC.md, under "CLI", gives the shape. It writes nothing, not even a `cost` event or an id.
- `navarone lens method <Type.Method> <view>...`: shows a method with the context an agent, or the user, needs to read it, as markdown: its callers with the lines around each call, the code of what it calls to a depth, and its class in short. The lens is a small language, `navarone lens method Parser.Parse callers:2 [expand:1] class:short`, built from the provider's facts. `--json` prints the same lens for brain's window. It writes only its `cost` event, `--json` none.
- `navarone duplicates [--similarity <share>] [--tokens <count>]`: prints every clone pair at or above the floor, similarity 0.7 and 30 tokens by default, with both pieces of code. It is research toward a duplication check: it raises no finding and writes only its `cost` event.

See `src/Navarone.Cli/README.md` for what is still open.
