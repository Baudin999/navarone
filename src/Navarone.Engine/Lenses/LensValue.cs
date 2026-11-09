namespace Navarone.Engine.Lenses;

/// <summary>
/// One value of a view, after its colon: a value alone, such as the 3 of `callers:3`, which is the view's parameter at
/// its place, or a value with the name of its parameter, such as the lines:5 of `callers:3,lines:5`.
/// </summary>
public sealed record LensValue(LensWord? Name, LensWord Value)
{
    /// <summary>The column of the lens the value starts at, its name's when it has one.</summary>
    public int Column => Name?.Column ?? Value.Column;

    /// <summary>The value as written, such as `3` or `lines:5`.</summary>
    public string Written => Name is { } name ? $"{name.Text}:{Value.Text}" : Value.Text;
}
