using System.Globalization;

namespace Navarone.Engine.Lenses;

/// <summary>
/// What one view takes: its parameters, in their places, and whether brackets with views of its own may follow it.
/// </summary>
internal sealed record LensViewRule(IReadOnlyList<LensParameter> Parameters, bool TakesViews)
{
    /// <summary>
    /// The value of each of the view's parameters: the value written for it, by its name or at its place, or its
    /// default. A value for no parameter, a value the parameter does not take, or a parameter given twice, is an error
    /// in the given lens.
    /// </summary>
    public Dictionary<string, string> Values(string lens, LensView view)
    {
        var word = view.Word.Text;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var place = 0; place < view.Values.Count; place++)
        {
            var value = view.Values[place];
            if (ParameterOf(value, place) is not { } parameter)
            {
                throw LensError.At(lens, value.Column, $"{word} takes {Described()}");
            }

            if (!parameter.Accepts(value.Value.Text))
            {
                throw LensError.At(lens, value.Value.Column, $"{parameter.Name} of {word} takes {parameter.Takes}");
            }

            if (!values.TryAdd(parameter.Name, value.Value.Text))
            {
                throw LensError.At(lens, value.Column, $"{word} gives {parameter.Name} twice");
            }
        }

        foreach (var parameter in Parameters)
        {
            values.TryAdd(parameter.Name, parameter.Default);
        }

        return values;
    }

    /// <summary>The value of the given parameter as a whole number, for a view the lens's rules checked.</summary>
    public int Number(string lens, LensView view, string parameter) =>
        int.Parse(Values(lens, view)[parameter], NumberStyles.None, CultureInfo.InvariantCulture);

    // A named value goes to the parameter of its name, a value alone to the parameter at its place.
    private LensParameter? ParameterOf(LensValue value, int place)
    {
        if (value.Name is not { } name)
        {
            return Parameters.ElementAtOrDefault(place);
        }

        return Parameters.FirstOrDefault(parameter => parameter.Name == name.Text);
    }

    // The parameters in their places, such as `count, then lines`.
    private string Described() => string.Join(", then ", Parameters.Select(parameter => parameter.Name));
}

/// <summary>
/// One parameter of a view: its name, its default, whether it accepts a value written for it, and what it takes in
/// words.
/// </summary>
internal sealed record LensParameter(string Name, string Default, Func<string, bool> Accepts, string Takes)
{
    /// <summary>A parameter that takes one whole number at or above the given least.</summary>
    public static LensParameter Count(string name, int least, int fallback) => new(
        name,
        fallback.ToString(CultureInfo.InvariantCulture),
        value => IsCount(value, least),
        string.Create(CultureInfo.InvariantCulture, $"a whole number from {least}"));

    /// <summary>Whether the value is a whole number at or above the given least.</summary>
    public static bool IsCount(string value, int least) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count >= least;
}
