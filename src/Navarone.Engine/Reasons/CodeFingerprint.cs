using System.Security.Cryptography;
using System.Text;
using Navarone.Engine.Facts;

namespace Navarone.Engine.Reasons;

/// <summary>
/// A fingerprint of a subject's code, computed from the Code its facts carry.
/// A reason keeps the fingerprint of the code it answered. When the code's fingerprint changes, the reason is void
/// and the problem is red again under the same id.
/// </summary>
public readonly record struct CodeFingerprint(string Value)
{
    /// <summary>
    /// Computes the fingerprint of a subject's code: the SHA-256 of its symbol and its code, in lower case hex. The
    /// symbol is part of it, so a rename voids a reason even where the code's own text names nothing renamed, such as
    /// a method whose class was renamed.
    /// </summary>
    public static CodeFingerprint Of(SymbolId subject, string code) =>
        new(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{subject.Value}\n{code}"))));
}
