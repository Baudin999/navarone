using Navarone.Engine.Facts;
using Navarone.Tests.Support;

namespace Navarone.Tests.Languages;

public sealed class FlatDispatchTests
{
    [Theory]
    [InlineData("int M(char c) { switch (c) { case 'a': return 1; case 'b': case 'c': return 2; default: throw new System.ArgumentException(); } }")]
    [InlineData("int M(char c) { switch (c) { case 'a': { return 1; } case 'b': return 2; } return 0; }")]
    [InlineData("void M(int k) { switch (k) { case 1: Log(1); break; case 2: Log(2); break; default: break; } }")]
    [InlineData("int M(char c) => c switch { 'a' => 1, 'b' or 'c' => 2, _ => 0 };")]
    [InlineData("int M(object o) => o switch { string => 1, int or long => 2, _ => 0 };")]
    [InlineData("int M(char c) { return c switch { 'a' => 1, _ => throw new System.ArgumentException() }; }")]
    [InlineData("void M(int k) { if (k == 1) Log(1); else if (k == 2) Log(2); else Log(0); }")]
    [InlineData("int? M(string? s) => s?.Length switch { 1 => s?.Length ?? 0, _ => null };")]
    [InlineData("int M(object o) { switch (o) { case string or char: return 2; case 1 or (2 or 3): return 1; default: return 0; } }")]
    [InlineData("int M(object? o) => o switch { null => 0, not null => 1 };")]
    [InlineData("int M(int[] v) => v switch { [..] => 1 };")]
    public async Task ASwitchOrElseIfChainOfSingleReturnsOrExpressionsIsAFlatDispatch(string method)
    {
        Assert.NotEqual(Dispatch.None, await DispatchAsync(method));
    }

    [Theory]
    [InlineData("int M(char c) { switch (c) { case 'a': Log(1); return 1; default: return 0; } }")]
    [InlineData("int M(char c) { switch (c) { case 'a': return c > 'b' ? 1 : 2; default: return 0; } }")]
    [InlineData("int M(char c) => c switch { 'a' => c > 'b' ? 1 : 2, _ => 0 };")]
    [InlineData("int M(int k) { if (k == 1) { if (k > 0) return 1; } else if (k == 2) return 2; return 0; }")]
    [InlineData("int M(int k) { Log(k); if (k == 1) return 1; else if (k == 2) return 2; return 0; }")]
    [InlineData("int M(int k) { if (k == 1) Log(1); if (k == 2) Log(2); return 0; }")]
    [InlineData("int M(int k) { foreach (var i in new[] { k }) { Log(i); } return 0; }")]
    [InlineData("int M(int k) { if (new[] { k }.Any(x => { foreach (var y in new[] { x }) { if (y > 0) { while (y > 9) { } return true; } } return false; })) return 1; return 0; }")]
    [InlineData("int M(int k) { if (new[] { k }.Any(x => x > 0)) return 1; return 0; }")]
    [InlineData("int M(int k) { if (k > 0 ? k > 1 : k < -1) return 1; return 0; }")]
    [InlineData("int M(int k) { switch (Compute(k, v => { for (var i = 0; i < v; i++) { if (i > 2) { return i; } } return 0; })) { case 1: return 1; default: return 0; } }")]
    [InlineData("int M(object o) { switch (o) { case int n when new[] { n }.Any(i => { if (i > 0) { return true; } return false; }): return 2; default: return 0; } }")]
    [InlineData("int M(int k) => Compute(k, v => v > 0 ? 1 : 0) switch { 1 => 1, _ => 0 };")]
    [InlineData("int M(object o) => o switch { int n when n > 0 ? true : false => 1, _ => 0 };")]
    [InlineData("int M(int k) => k > 0 ? (k > 9 ? 2 : 1) : 0;")]
    [InlineData("int M(int k) => (k > 0 ? k > 9 : k < -9) ? 1 : 0;")]
    [InlineData("int M(int k) => k > 0 ? 1 : k < 0 ? -1 : new[] { k }.Count(x => { if (x > 0) { return true; } return false; });")]
    [InlineData("int M(int k) { if (k == 1) return 1; else if (k == 2 || k == 3) { return 2; } else return 0; }")]
    [InlineData("int M(int k) { if (k == 1) return 1; if (k == 2) return 2; if (k > 9 && k < 20) return 3; return 0; }")]
    [InlineData("int M(object o) { switch (o) { case int n when n > 0 && n < 9: return 1; default: return 0; } }")]
    [InlineData("int M(int k) => k switch { > 0 and < 9 => 1, _ => 0 };")]
    [InlineData("int M(char c) { switch (c) { case 'a': return 1; case 'b' when c != 'x': return 2; } return 0; }")]
    [InlineData("int M(char c) => c switch { 'a' => 1, _ when c > 'x' => 3, _ => 0 };")]
    [InlineData("int M(object o) => o switch { string { Length: 0 } => 1, _ => 0 };")]
    [InlineData("int M(object o) => o switch { string { } s => 1, _ => 0 };")]
    [InlineData("int M(object o) { switch (o) { case string { Length: > 3 }: return 1; default: return 0; } }")]
    [InlineData("int M(object o) { if (o is string { Length: 0 }) return 1; return 0; }")]
    [InlineData("int M(object o) => o is (1, 2) ? 1 : 0;")]
    [InlineData("int M(int[] v) => v switch { [1, ..] => 1, _ => 0 };")]
    [InlineData("bool M(int a, int b, int c) => a > 0 && b > 0 && c > 0 && a < 10 && b < 10 && c < 10 && a != b && b != c ? true : false;")]
    public async Task ABranchThatDoesMoreOrWorkAroundTheDispatchIsNotFlat(string method)
    {
        Assert.Equal(Dispatch.None, await DispatchAsync(method));
    }

    [Theory]
    [InlineData("string M(K k) => k switch { K.A => \"a\", K.B => \"b\", _ => \"other\" };")]
    [InlineData("double M(K k) => k switch { K.A => 4, K.B => 10, _ => 7 };")]
    [InlineData("string M(K k) => k switch { K.A => nameof(K.A), _ => nameof(K.B) };")]
    [InlineData("int M(char c) { switch (c) { case 'a': return 1; case 'b': break; default: throw new System.ArgumentException(nameof(c)); } return 0; }")]
    [InlineData("int M(K k) { if (k == K.A) return 1; else if (k == K.B) return 2; return 0; }")]
    [InlineData("object M(K k) => k switch { K.A => new System.Text.StringBuilder(), _ => throw new System.ArgumentException(nameof(k)) };")]
    [InlineData("string M(K k) => k == K.A ? \"a\" : k == K.B ? \"b\" : \"c\";")]
    [InlineData("int M(int k) { return k > 0 ? 1 : 0; }")]
    public async Task AFlatDispatchWhoseEveryBranchOnlyReturnsAValueIsAMapping(string method)
    {
        Assert.Equal(Dispatch.Mapping, await DispatchAsync(method));
    }

    [Theory]
    [InlineData("void M(int k) { switch (k) { case 1: Log(1); break; case 2: Log(2); break; default: break; } }")]
    [InlineData("void M(int k) { if (k == 1) Log(1); else if (k == 2) Log(2); else Log(0); }")]
    [InlineData("int M(int k) => k switch { 1 => Compute(k, v => v), _ => 0 };")]
    [InlineData("int M(int k) { switch (k) { case 1: return Count = 1; default: return 0; } }")]
    [InlineData("int M(int k) { switch (k) { case 1: return Count++; default: return 0; } }")]
    [InlineData("void M(int k) { switch (k) { case 1: return; default: throw new System.ArgumentException(); } }")]
    [InlineData("int M(int k) { if (Compute(k, null) == 1) return 1; return 0; }")]
    [InlineData("int M(K k) => k switch { K.A => 1, _ => throw Abort(k) };")]
    [InlineData("int M(K k) { switch (k) { case K.A: return 1; default: throw new System.ArgumentException(Save()); } }")]
    [InlineData("int M(K k) => k switch { K.A => 1, _ => throw new System.ArgumentException(k.ToString()) };")]
    [InlineData("int M(K k) => k == K.A ? Compute(1, null) : 0;")]
    public async Task AFlatDispatchWhoseBranchesActIsNoMapping(string method)
    {
        Assert.Equal(Dispatch.Flat, await DispatchAsync(method));
    }

    private static async Task<Dispatch> DispatchAsync(string method)
    {
        var facts = await CSharpSnippet.FactsAsync($"using System.Linq; enum K {{ A, B, C }} class C {{ int Count; {method} void Log(int k) {{ }} int Compute(int k, System.Func<int, int> f) => f(k); System.Exception Abort(K k) => new(); string Save() => \"\"; }}");
        return facts.Types.SelectMany(type => type.Methods).Single(candidate => candidate.Name.StartsWith("M(", StringComparison.Ordinal)).Dispatch;
    }
}
