using System.Globalization;
using System.Text.Json.Nodes;
using Navarone.Engine.Events;

namespace Navarone.Engine.Clashes;

/// <summary>
/// The questions brain's window lists for Navarone, `navarone disputes --json`: one per clash the user has not judged
/// yet, read from the event log alone, so it works from any directory. Each is a bench read top to bottom in brain's
/// generic blocks; brain renders them.
/// </summary>
public static class Questions
{
    /// <summary>
    /// Every clash not yet judged, as one JSON document, `{"source":"navarone","questions":[...]}`, worktree by worktree
    /// and oldest first. A clash in a worktree that is gone is left out: no run there can collect its answer.
    /// </summary>
    public static string Document(NavaroneEventHandler events)
    {
        var questions = new JsonArray();
        var histories = WorktreeHistory.OfEach(events.Read()).OrderBy(each => each.Key, StringComparer.Ordinal);
        foreach (var (worktree, history) in histories)
        {
            if (!Directory.Exists(worktree))
            {
                continue;
            }

            var clashes = history.Clashes().OrderBy(each => each.At);
            foreach (var clash in clashes)
            {
                questions.Add(Question(clash, events));
            }
        }

        return new JsonObject { ["source"] = "navarone", ["questions"] = questions }.ToJsonString(LoggedEvent.Options);
    }

    // The bench of one clash: the rule the validator named as the title, then what is wrong, the code, the builder's
    // argument and the validator's finding, and the details last. The subject's lens is the context with the finding's
    // line in focus, the landing's branch its group, and the question is stale once the subject's code changed. A
    // subject that is gone has no lens to show.
    private static JsonObject Question(Clash clash, NavaroneEventHandler events)
    {
        var refusal = clash.Refusal;
        var defended = clash.Defended;
        var blocks = new JsonArray
        {
            Text("What is wrong", refusal.WhatIsWrong),
            Code(clash, events),
            Text("The builder", clash.Reason),
            Text("The validator", refusal.Finding),
            Details(clash),
        };

        var question = new JsonObject
        {
            ["id"] = clash.QuestionId(),
            ["repo"] = clash.Place.Worktree,
            ["title"] = refusal.Rule.Text,
            ["group"] = clash.Place.Branch ?? clash.Place.Worktree,
            ["tags"] = new JsonArray(refusal.Check),
            ["created"] = clash.At,
            ["stale"] = clash.Stale,
            ["focus"] = new JsonArray(new JsonObject { ["path"] = defended.Path, ["lines"] = new JsonArray(defended.Line) }),
            ["blocks"] = blocks,
            ["choices"] = new JsonArray(Choice("keep", "Keep: the builder is right"), Choice("replace", "Replace: the validator is right")),
        };
        if (clash.CodeNow is null)
        {
            return question;
        }

        if (defended.Context is { } context)
        {
            question["context"] = context;
        }

        return question;
    }

    // The code the clash is about: the code before the refused fix and the fix as a diff, or the code a refused reason
    // defends.
    private static JsonObject Code(Clash clash, NavaroneEventHandler events)
    {
        var defended = clash.Defended;
        var code = Stored(events, defended.Code);
        if (clash.Refusal.Before is not { } before)
        {
            return new JsonObject
            {
                ["type"] = "code",
                ["caption"] = "The code the builder's reason defends",
                ["path"] = defended.Path,
                ["startLine"] = defended.StartLine,
                ["code"] = code,
            };
        }

        var file = new JsonObject
        {
            ["path"] = defended.Path,
            ["before"] = Stored(events, before),
            ["after"] = code,
        };
        return new JsonObject
        {
            ["type"] = "diff",
            ["caption"] = "The code before and the builder's fix",
            ["files"] = new JsonArray(file),
        };
    }

    // What the user judges by last: the check, the problem and where it was, the rule's number and the hashes.
    private static JsonObject Details(Clash clash)
    {
        var refusal = clash.Refusal;
        var facts = new JsonArray
        {
            Fact("Check", refusal.Check),
            Fact("Symbol", refusal.Symbol),
            Fact("Problem", refusal.Id),
            Fact("Rule", refusal.Rule.Number.ToString(CultureInfo.InvariantCulture)),
            Fact("Worktree", clash.Place.Worktree),
            Fact("Code", clash.Defended.Code),
        };
        if (refusal.Before is { } before)
        {
            facts.Add(Fact("Code before the fix", before));
        }

        return new JsonObject { ["type"] = "facts", ["caption"] = "Details", ["facts"] = facts };
    }

    // The code stored under the given hash, or empty when the store could not keep it.
    private static string Stored(NavaroneEventHandler events, string hash) => events.Code(hash) ?? "";

    private static JsonObject Text(string caption, string text) => new() { ["type"] = "text", ["caption"] = caption, ["text"] = text };

    private static JsonObject Fact(string label, string value) => new() { ["label"] = label, ["value"] = value };

    private static JsonObject Choice(string id, string label) => new() { ["id"] = id, ["label"] = label };
}
