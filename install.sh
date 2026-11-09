#!/usr/bin/env bash
# Installs navarone from this checkout, links the central NAVARONE.md where every agent loads it and, when brain is
# installed, adds Navarone's questions to brain's window. The user runs it: it writes brain's sources.tsv, which brain
# runs commands from as the user, so no agent may run it.
#
#   ./install.sh
#
# Before it changes anything, it prints every path it will write and asks `Continue? [y/N]`. Only y or yes goes on;
# anything else, an empty answer or no terminal included, changes nothing and exits 1. A script answers on stdin:
# `echo y | ./install.sh`.
#
#   publish   publishes the CLI into ~/.local/lib/navarone, outside the checkout, so `git clean` leaves it working. When
#             ${XDG_CONFIG_HOME:-~/.config}/brain/guard is executable, the publish runs through it, as brain's
#             scripts/dotnet-guard.sh runs dotnet, so this machine's limits hold.
#   bin       links LOCAL_BIN/navarone to ~/.local/lib/navarone/navarone. A file there that is not a link is left
#             alone and the install stops.
#   rules     links CLAUDE_CONFIG_DIR/rules/NAVARONE.md to rules/NAVARONE.md in this checkout, where Claude Code
#             loads it into every agent, so an edit here reaches them without installing again. A file there that is
#             not a link is left alone and the install says so. It removes CLAUDE_CONFIG_DIR/rules/taste.md when it is
#             a link whose target no longer exists: NAVARONE.md replaced it.
#   sources   when brain is installed, keeps the navarone line of BRAIN_HOME/sources.tsv at six columns:
#             `navarone<TAB>list<TAB>context<TAB>events<TAB>code<TAB>classes`, the commands being
#             `'<LOCAL_BIN>/navarone' disputes --json`, `'<LOCAL_BIN>/navarone' lens --json` (brain's window runs it
#             with a lens's words as arguments), `'<LOCAL_BIN>/navarone' events --json`, `'<LOCAL_BIN>/navarone' code`
#             (brain appends the hash as one argument) and `'<LOCAL_BIN>/navarone' classes` (brain appends a file's path
#             and `--json`). No navarone line yet: the full line is added. A line with fewer columns gets the missing
#             ones from where it stops and keeps every column it has; a blank column at the end counts as missing. A
#             full line is kept as it is. Each case prints one line saying which. The commands name navarone by its
#             absolute path, quoted for sh, because brain's server may run without LOCAL_BIN on PATH.
#
# Brain is installed when its home exists. Running it again publishes again and changes nothing else.
#
#   LOCAL_BIN          a directory on PATH, ~/.local/bin by default
#   BRAIN_HOME         brain's home, ${XDG_DATA_HOME:-~/.local/share}/brain by default, as brain reads it
#   CLAUDE_CONFIG_DIR  Claude Code's config directory, ~/.claude by default, as Claude Code reads it
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
local_bin="${LOCAL_BIN:-$HOME/.local/bin}"
brain_home="${BRAIN_HOME:-${XDG_DATA_HOME:-$HOME/.local/share}/brain}"
guard="${XDG_CONFIG_HOME:-$HOME/.config}/brain/guard"
publish="$HOME/.local/lib/navarone"
claude_rules="${CLAUDE_CONFIG_DIR:-$HOME/.claude}/rules"

if ! command -v dotnet >/dev/null; then
    echo "install: navarone needs the .NET SDK to build, and dotnet is not on PATH. Nothing was changed." >&2
    exit 1
fi

if [[ -e "$local_bin/navarone" && ! -L "$local_bin/navarone" ]]; then
    echo "install: $local_bin/navarone is a file, not a link. Move it away and run ./install.sh again. Nothing was changed." >&2
    exit 1
fi

echo "This installs navarone from $root. It changes these paths:"
echo
echo "  $publish"
echo "      the navarone CLI, built from this checkout with dotnet publish"
echo "  $local_bin/navarone"
echo "      a link to that CLI, so navarone runs by name"
if [[ -e "$claude_rules/NAVARONE.md" && ! -L "$claude_rules/NAVARONE.md" ]]; then
    echo "  $claude_rules/NAVARONE.md is a file, not a link: it is left alone"
else
    echo "  $claude_rules/NAVARONE.md"
    echo "      a link to rules/NAVARONE.md in this checkout. Claude Code loads every file in that folder into every"
    echo "      session in every project, so from now on every Claude Code agent on this machine works by Navarone's"
    echo "      rules and its author's taste in code. Delete the link to undo it."
fi
if [[ -L "$claude_rules/taste.md" && ! -e "$claude_rules/taste.md" ]]; then
    echo "  $claude_rules/taste.md"
    echo "      removed: a link to a file that no longer exists, which NAVARONE.md replaced"
fi
if [[ -d "$brain_home" ]]; then
    echo "  $brain_home/sources.tsv"
    echo "      the navarone line, so brain shows Navarone's questions, events and code"
fi
echo
printf 'Continue? [y/N] '
answer=""
read -r answer || true
case "$answer" in
y | Y | yes | Yes | YES) ;;
*)
    echo "Nothing was changed."
    exit 1
    ;;
esac

# Runs the command through this machine's guard when there is one.
guarded() {
    if [[ -x "$guard" ]]; then
        "$guard" "$@"
    else
        "$@"
    fi
}

guarded dotnet publish "$root/src/Navarone.Cli" -c Release -o "$publish" -m:2 -nodeReuse:false -p:UseSharedCompilation=false
echo "published navarone into $publish"

mkdir -p "$local_bin"
local_bin="$(cd "$local_bin" && pwd)"
ln -sfn "$publish/navarone" "$local_bin/navarone"
echo "linked $local_bin/navarone"
case ":$PATH:" in
*":$local_bin:"*) ;;
*) echo "install: $local_bin is not on PATH. Add it to PATH so navarone runs by name." >&2 ;;
esac

if [[ -e "$claude_rules/NAVARONE.md" && ! -L "$claude_rules/NAVARONE.md" ]]; then
    echo "install: $claude_rules/NAVARONE.md is a file, not a link, so it was left alone and agents do not load $root/rules/NAVARONE.md." >&2
else
    mkdir -p "$claude_rules"
    ln -sfn "$root/rules/NAVARONE.md" "$claude_rules/NAVARONE.md"
    echo "linked $claude_rules/NAVARONE.md"
fi

if [[ -L "$claude_rules/taste.md" && ! -e "$claude_rules/taste.md" ]]; then
    rm "$claude_rules/taste.md"
    echo "removed $claude_rules/taste.md, a link to a file that no longer exists; NAVARONE.md replaced it"
fi

if [[ ! -d "$brain_home" ]]; then
    echo "brain is not installed at $brain_home; navarone works without it"
    exit 0
fi

brain_home="$(cd "$brain_home" && pwd)"
sources="$brain_home/sources.tsv"
# brain runs the list command through /bin/sh -c, and the context command with the lens's words after it as
# arguments: the path goes in single quotes, each ' in it as '\''.
quoted="'$(sed "s/'/'\\\\''/g" <<<"$local_bin/navarone")'"

# The six columns of the navarone line: name, list, context, events, code and classes commands. brain appends the lens's
# words to the context command, the hash to the code command, and a file's path and --json to the classes command.
list="$quoted disputes --json"
lens="$quoted lens --json"
events="$quoted events --json"
code="$quoted code"
classes="$quoted classes"

# One pass over sources.tsv decides the outcome. A navarone line with fewer columns gets the missing ones from where it
# stops and keeps the columns it has; a column that is blank at the end counts as missing. No navarone line: the full
# line goes at the end. The pass writes the new file and prints added, completed or kept. ENVIRON passes the commands
# to awk as they are, where -v would read their backslashes as escapes.
completed="$(mktemp)"
trap 'rm -f "$completed"' EXIT
[[ -e "$sources" ]] || : >"$sources"
outcome="$(OUT="$completed" LIST="$list" LENS="$lens" EVENTS="$events" CODE="$code" CLASSES="$classes" awk -F '\t' '
    BEGIN {
        defaults[2] = ENVIRON["LIST"]
        defaults[3] = ENVIRON["LENS"]
        defaults[4] = ENVIRON["EVENTS"]
        defaults[5] = ENVIRON["CODE"]
        defaults[6] = ENVIRON["CLASSES"]
        out = ENVIRON["OUT"]
        outcome = "added"
    }
    {
        name = $1
        gsub(/^[ \t]+|[ \t]+$/, "", name)
        if (name != "navarone") { print > out; next }
        last = NF
        while (last > 1 && $last ~ /^[ ]*$/) { last-- }
        line = $1
        for (column = 2; column <= 6; column++) {
            line = line "\t" (column <= last ? $column : defaults[column])
        }
        for (column = 7; column <= NF; column++) {
            line = line "\t" $column
        }
        if (last < 6) { changed = 1 }
        if (outcome == "added") { outcome = "kept" }
        print line > out
    }
    END {
        if (outcome == "added") {
            print "navarone\t" defaults[2] "\t" defaults[3] "\t" defaults[4] "\t" defaults[5] "\t" defaults[6] > out
        }
        if (changed) { outcome = "completed" }
        print outcome
    }' "$sources")"

case "$outcome" in
added)
    cat "$completed" >"$sources"
    echo "added navarone to $sources"
    ;;
completed)
    cat "$completed" >"$sources"
    echo "completed the navarone line in $sources"
    ;;
*)
    echo "kept the navarone line in $sources"
    ;;
esac
