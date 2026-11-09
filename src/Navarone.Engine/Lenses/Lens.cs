namespace Navarone.Engine.Lenses;

/// <summary>
/// A lens into the code: a subject, named by its kind and its name, and the views that show its context. The name may
/// take the number of one overload after a colon. Every view is an operation on the subject, whatever views come
/// before it. Text is the lens as written, which an error points into.
/// </summary>
// navarone: 0000021
public sealed record Lens(string Text, LensWord Kind, LensWord Name, LensWord? Overload, IReadOnlyList<LensView> Views)
{
    /// <summary>
    /// The lens the given text writes. A text that does not follow the lens grammar, a kind Navarone does not know or
    /// has not built yet, or a view the kind does not take, is an error that says what is wrong and where.
    /// </summary>
    public static Lens Parse(string text)
    {
        var lens = LensParser.Parse(text);
        if (lens.Kind.Text is "class" or "module")
        {
            throw LensError.At(text, lens.Kind.Column, $"the kind {lens.Kind.Text} is not built yet; only method is");
        }

        if (lens.Kind.Text != "method")
        {
            throw LensError.At(text, lens.Kind.Column, $"unknown kind {lens.Kind.Text}; the kinds are method, class and module, and only method is built");
        }

        MethodLensRules.Check(lens);
        return lens;
    }

    /// <summary>
    /// What the lens shows in the given code, as markdown. It reads facts only: it writes nothing but the facts cache.
    /// A name that matches no method is an error.
    /// </summary>
    public async Task<string> ShowAsync(Code code, CancellationToken cancellationToken)
    {
        var page = await PageAsync(code, cancellationToken).ConfigureAwait(false);
        return page.Markdown();
    }

    /// <summary>
    /// What the lens shows in the given code, as one page that writes itself as markdown or as JSON. It reads facts
    /// only: it writes nothing but the facts cache. A name that matches no method is an error.
    /// </summary>
    public async Task<LensPage> PageAsync(Code code, CancellationToken cancellationToken)
    {
        var facts = await code.FactsAsync(cancellationToken).ConfigureAwait(false);
        return new MethodLens(facts, code.Provider.Language, this).Show();
    }
}
