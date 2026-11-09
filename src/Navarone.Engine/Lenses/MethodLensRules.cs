namespace Navarone.Engine.Lenses;

/// <summary>
/// The views a method lens takes and the parameters each takes. A view without a value takes its defaults. A method is
/// named Type.Method, with the number of one overload after a colon when it has several.
/// </summary>
internal static class MethodLensRules
{
    private static readonly Dictionary<string, LensViewRule> Rules = new(StringComparer.Ordinal)
    {
        ["callers"] = new([LensParameter.Count("count", 1, 1), LensParameter.Count("lines", 0, 3)], TakesViews: true),
        ["expand"] = new([LensParameter.Count("depth", 1, 1)], TakesViews: true),
        ["class"] = new([new LensParameter("form", "short", form => form == "short", "short, the one form built")], TakesViews: false),
    };

    /// <summary>Checks the lens's name and every view in it, to any depth.</summary>
    public static void Check(Lens lens)
    {
        var name = lens.Name.Text;
        if (name.Split('.') is not [{ Length: > 0 }, { Length: > 0 }])
        {
            throw LensError.At(lens.Text, lens.Name.Column, $"a method is named Type.Method, such as Parser.Parse, not {name}");
        }

        if (lens.Overload is { } overload && !LensParameter.IsCount(overload.Text, 1))
        {
            throw LensError.At(lens.Text, overload.Column, "an overload is a whole number from 1, such as Show:1");
        }

        foreach (var view in lens.Views)
        {
            Check(lens.Text, view);
        }
    }

    /// <summary>The rule of a view the lens's rules checked.</summary>
    public static LensViewRule RuleOf(LensView view) => Rules[view.Word.Text];

    private static void Check(string text, LensView view)
    {
        var word = view.Word.Text;
        if (!Rules.TryGetValue(word, out var rule))
        {
            throw LensError.At(text, view.Word.Column, $"unknown view {word}; a method takes callers, expand and class");
        }

        // Values throws on a value the view does not take.
        rule.Values(text, view);
        if (!rule.TakesViews && view.Views.Count > 0)
        {
            throw LensError.At(text, view.Views[0].Word.Column, $"{word} takes no views in brackets");
        }

        foreach (var inner in view.Views)
        {
            Check(text, inner);
        }
    }
}
