namespace Navarone.Engine.Lenses;

/// <summary>
/// One view of a lens: an operation on the subject. Word names it, Values are the data after its colon, and Views are
/// its own views, written in brackets after it, which apply to each of its results.
/// </summary>
public sealed record LensView(LensWord Word, IReadOnlyList<LensValue> Values, IReadOnlyList<LensView> Views)
{
    /// <summary>The view and its values as written, such as `callers:2,lines:5`, without its own views.</summary>
    public string Written
    {
        get
        {
            if (Values.Count == 0)
            {
                return Word.Text;
            }

            return $"{Word.Text}:{string.Join(',', Values.Select(value => value.Written))}";
        }
    }
}
