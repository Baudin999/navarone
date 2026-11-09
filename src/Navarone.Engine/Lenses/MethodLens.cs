using System.Globalization;
using Navarone.Engine.Facts;

namespace Navarone.Engine.Lenses;

/// <summary>
/// Shows a method lens over a codebase's facts. The subject is the overload the lens's name numbers, or else every
/// method the name matches: each is shown in turn, in file and line order, with every view. A method's full code and a
/// type's outline are printed once; later they point back to the section that shows them. Each view is a section under
/// the subject, in the order written, each of its results a section under it, and a view's own views are sections under
/// each of its results. Every line of a method's code that calls methods in the codebase is a mark with a lens per
/// method, which makes it the subject, and the calling line of a caller's excerpt offers the caller first. The
/// subject's section is open.
/// </summary>
internal sealed class MethodLens
{
    private readonly Lens lens;
    private readonly string language;
    private readonly Dictionary<SymbolId, MethodFacts> methods = [];
    private readonly Dictionary<SymbolId, List<(MethodFacts Caller, int Line)>> callers;
    private readonly LensQueries queries;
    private readonly List<LensSection> subjects = [];

    // How many sections the page has, so each section's id is unique.
    private int count;

    // The section that shows each method's full code or each type's outline.
    private readonly Dictionary<SymbolId, string> shown = [];

    public MethodLens(CodebaseFacts facts, string language, Lens lens)
    {
        this.lens = lens;
        this.language = language;
        foreach (var type in facts.Types)
        {
            foreach (var method in type.Methods)
            {
                methods.TryAdd(method.Symbol, method);
            }
        }

        callers = CallersOf(methods.Values);
        queries = new LensQueries(facts);
    }

    /// <summary>The lens's page: each subject with its code, then a section per view.</summary>
    // navarone: 0000026
    public LensPage Show()
    {
        var overloads = queries.Overloads(lens);
        foreach (var subject in LensQueries.Chosen(lens, overloads))
        {
            var name = NameOf(subject);
            var overload = overloads.IndexOf(subject) + 1;
            var section = NewSection($"method {name}, overload {overload} of {overloads.Count}", open: true);
            subjects.Add(section);
            Full(section, subject);
            foreach (var view in lens.Views)
            {
                Show(view, subject, section);
            }
        }

        return new LensPage(language, subjects);
    }

    // Every call site in the codebase, by the symbol it calls, in file and line order.
    private static Dictionary<SymbolId, List<(MethodFacts Caller, int Line)>> CallersOf(IEnumerable<MethodFacts> methods) =>
        methods.SelectMany(caller => caller.CallSites.Select(site => (site.Callee, Caller: caller, site.Line)))
            .OrderBy(call => call.Caller.Location.Path, StringComparer.Ordinal)
            .ThenBy(call => call.Line)
            .GroupBy(call => call.Callee)
            .ToDictionary(group => group.Key, group => group.Select(call => (call.Caller, call.Line)).ToList());

    // A new section with the next id on the page, in no section yet.
    private LensSection NewSection(string title, bool open)
    {
        count++;
        var id = string.Create(CultureInfo.InvariantCulture, $"s{count}");
        return new LensSection(id, title, open);
    }

    // A new section under the given one.
    private LensSection Section(LensSection parent, string title)
    {
        var section = NewSection(title, open: false);
        parent.Sections.Add(section);
        return section;
    }

    // The code of the symbol, a method or a type, with its marks, in the given section, or where it was shown above. A
    // symbol names one method or type.
    private void Once(LensSection section, SymbolId symbol, string path, int line, string code, IReadOnlyList<LensMark> marks)
    {
        if (shown.TryGetValue(symbol, out var above))
        {
            var first = code.Split('\n')[0];
            section.Blocks.Add(new LensShownAbove(path, line, first, above));
            return;
        }

        shown.Add(symbol, section.Id);
        section.Blocks.Add(new LensCode(path, line, code, marks));
    }

    // The method's full code in the given section, with a mark on each line that calls a method a lens can name.
    private void Full(LensSection section, MethodFacts method)
    {
        var written = method.Written;
        var last = written.Line + written.Text.TrimEnd().Split('\n').Length - 1;
        var marks = Calls(method, written.Line, last);
        Once(section, method.Symbol, method.Location.Path, written.Line, written.Text, marks);
    }

    // A mark on each of the given lines of the method's code, from first to last, that calls methods in the codebase a
    // lens can name: a lens per method, in the order they are called, that makes it the subject.
    private List<LensMark> Calls(MethodFacts method, int first, int last)
    {
        var marks = new List<LensMark>();
        var sites = method.CallSites.Where(site => site.Line >= first && site.Line <= last);
        foreach (var line in sites.GroupBy(site => site.Line).OrderBy(line => line.Key))
        {
            var callees = line.Select(site => methods.GetValueOrDefault(site.Callee)).OfType<MethodFacts>();
            List<LensQuery> opened = [.. callees.Select(queries.Of).OfType<LensQuery>().Distinct()];
            if (opened.Count > 0)
            {
                marks.Add(new LensMark(line.Key, "call", "opens a method it calls as the subject", opened));
            }
        }

        return marks;
    }

    // The marks of a caller's excerpt, with the lens that makes the caller the subject first on its calling line, before
    // the methods that line calls.
    private List<LensMark> Calling(MethodFacts caller, int line, int first, int last)
    {
        var marks = Calls(caller, first, last);
        if (queries.Of(caller) is not { } opened)
        {
            return marks;
        }

        IReadOnlyList<LensQuery> called = marks.Find(mark => mark.Line == line)?.Queries ?? [];
        var calling = new LensMark(line, "caller", "opens the calling method, or a method this line calls, as the subject", [opened, .. called]);
        var others = marks.Where(mark => mark.Line != line);
        return [.. others.Append(calling).OrderBy(mark => mark.Line)];
    }

    private string NameOf(MethodFacts method) => $"{queries.OwnerOf(method).Name}.{method.Name}";

    // The lens's rules checked every view, so a view here is callers, expand or class.
    // The view's section goes under the given one.
    private void Show(LensView view, MethodFacts method, LensSection parent)
    {
        var section = Section(parent, view.Written);
        switch (view.Word.Text)
        {
            case "callers":
                Callers(view, method, section);
                break;
            case "expand":
                Expand(view, method, section);
                break;
            default:
                Class(method, section);
                break;
        }
    }

    // At most count call sites, each with the calling method's lines around the call, lines above and below it. The
    // views in the brackets apply to the calling method. Each call site is a section under the view's.
    private void Callers(LensView view, MethodFacts method, LensSection parent)
    {
        var rule = MethodLensRules.RuleOf(view);
        var around = rule.Number(lens.Text, view, "lines");
        var count = rule.Number(lens.Text, view, "count");
        var sites = callers.GetValueOrDefault(method.Symbol, []).Take(count).ToList();
        if (sites.Count == 0)
        {
            parent.Blocks.Add(new LensText("No method in the codebase calls it."));
            return;
        }

        foreach (var (caller, line) in sites)
        {
            var (start, code) = Around(caller.Written, line, around);
            var last = start + code.Split('\n').Length - 1;
            var marks = Calling(caller, line, start, last);
            var section = Section(parent, NameOf(caller));
            section.Blocks.Add(new LensText($"Calls it at `{caller.Location.Path}:{line}`."));
            section.Blocks.Add(new LensCode(caller.Location.Path, start, code, marks));
            foreach (var inner in view.Views)
            {
                Show(inner, caller, section);
            }
        }
    }

    // The given line of the code with the given number of lines above and below it, inside the code, and the line of
    // the file the excerpt starts on.
    private static (int Line, string Code) Around(Written written, int line, int around)
    {
        var lines = written.Text.Split('\n');
        var at = line - written.Line;
        var from = Math.Max(0, at - around);
        var to = Math.Min(lines.Length - 1, at + around);
        return (written.Line + from, string.Join('\n', lines[from..(to + 1)]));
    }

    // The full code of every method in the codebase the method calls, to the given depth; the views in the brackets
    // apply to each of them. Each method is a section under the view's.
    private void Expand(LensView view, MethodFacts method, LensSection parent)
    {
        var depth = MethodLensRules.RuleOf(view).Number(lens.Text, view, "depth");
        var expanded = Expanded(method, depth);
        if (expanded.Count == 0)
        {
            parent.Blocks.Add(new LensText("It calls no method in the codebase."));
            return;
        }

        foreach (var (callee, caller) in expanded)
        {
            var section = Section(parent, NameOf(callee));
            section.Blocks.Add(new LensText($"Called by {NameOf(caller)}."));
            Full(section, callee);
            foreach (var inner in view.Views)
            {
                Show(inner, callee, section);
            }
        }
    }

    // Breadth first: the methods it calls in the order it calls them, then the methods those call, each once and never
    // the method itself. A call outside the codebase, such as into the framework, is not expanded.
    private List<(MethodFacts Callee, MethodFacts Caller)> Expanded(MethodFacts method, int depth)
    {
        var seen = new HashSet<SymbolId> { method.Symbol };
        var expanded = new List<(MethodFacts Callee, MethodFacts Caller)>();
        List<MethodFacts> reached = [method];
        for (var step = 0; step < depth; step++)
        {
            var next = new List<MethodFacts>();
            foreach (var caller in reached)
            {
                foreach (var callee in caller.Calls.Where(methods.ContainsKey))
                {
                    if (seen.Contains(callee))
                    {
                        continue;
                    }

                    seen.Add(callee);
                    var found = methods[callee];
                    expanded.Add((found, caller));
                    next.Add(found);
                }
            }

            reached = next;
        }

        return expanded;
    }

    // class:short: the declaring type's name, the signatures of its members in file and line order, then the
    // declaration of each of the type's fields and properties the method uses, in a section under the type's.
    private void Class(MethodFacts method, LensSection parent)
    {
        var type = queries.OwnerOf(method);
        var members = type.Fields.Select(field => (field.Location, field.Signature))
            .Concat(type.Methods.Select(member => (member.Location, member.Signature)))
            .OrderBy(member => member.Location.Path, StringComparer.Ordinal)
            .ThenBy(member => member.Location.Line)
            .Select(member => member.Signature)
            .Distinct();
        var outline = Section(parent, type.Name);
        var signatures = string.Join('\n', members);
        Once(outline, type.Symbol, type.Location.Path, type.Written.Line, signatures, []);

        var fields = type.Fields
            .Where(field => method.FieldUses.Contains(field.Symbol))
            .Select(field => (field.Signature, field.Location, field.Written));
        var properties = type.Methods
            .Where(member => member.IsAccessor && method.Calls.Contains(member.Symbol))
            .Select(member => (member.Signature, member.Location, member.Written));
        var used = fields.Concat(properties).DistinctBy(use => use.Signature).ToList();
        var uses = Section(outline, "Fields and properties it uses");
        if (used.Count == 0)
        {
            uses.Blocks.Add(new LensText($"It uses none of {type.Name}'s fields or properties."));
        }

        foreach (var (_, location, written) in used)
        {
            uses.Blocks.Add(new LensCode(location.Path, written.Line, written.Text, []));
        }
    }
}
