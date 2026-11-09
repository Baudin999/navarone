using System.Text.Encodings.Web;
using System.Text.Json;
using Navarone.Engine.Reasons;

namespace Navarone.Engine.Findings;

/// <summary>
/// How a finding's value comes to life, built from the facts by the same code that computes the value, so the two never
/// drift. Code is the subject's code where it stands. The value is Base plus what every item of the Proof adds, its
/// parts included.
/// </summary>
public sealed record Evidence(CodePiece Code, int Base, IReadOnlyList<ProofItem> Proof)
{
    // Code is shown as written, so &&, < and > stay themselves instead of \u escapes.
    private static readonly JsonSerializerOptions Indented = new(ReasonsFile.Options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Base plus what every item of the proof adds.</summary>
    public int Value() => Base + Proof.Sum(item => item.Total());

    /// <summary>
    /// The evidence document of the given finding, as indented JSON: the code block, the finding with its check,
    /// symbol, value, limit and base, and the proof tree. `navarone evidence &lt;id&gt; --json` prints it.
    /// </summary>
    public string Document(Finding finding) => JsonSerializer.Serialize(
        new Shape(Code, new FindingShape(finding.Check, finding.Subject.Value, finding.Value, finding.Limit, Base), Proof),
        Indented);

    private sealed record Shape(CodePiece Code, FindingShape Finding, IReadOnlyList<ProofItem> Proof);

    private sealed record FindingShape(string Check, string Symbol, int Value, int Limit, int Base);
}
