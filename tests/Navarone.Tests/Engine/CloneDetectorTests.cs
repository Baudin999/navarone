using Navarone.Engine.Duplication;
using Navarone.Tests.Support;

namespace Navarone.Tests.Engine;

public sealed class CloneDetectorTests
{
    private const string Original = """
        class A
        {
            int Total(int[] prices, int discount)
            {
                var sum = 0;
                foreach (var price in prices)
                {
                    if (price > 100)
                    {
                        sum += price - discount;
                    }
                    else
                    {
                        sum += price;
                    }
                }

                return sum * 2;
            }
        }
        """;

    [Fact]
    public async Task AnExactCopyIsAPairWhateverItsWhitespaceAndComments()
    {
        var copy = """
            class B
            {
                int Total(int[] prices, int discount)
                {
                    // the same sum, laid out differently
                    var sum = 0;
                    foreach (var price in prices) {
                        if (price > 100) { sum += price - discount; }
                        else { sum += price; }
                    }
                    return sum * 2;
                }
            }
            """;

        var pair = Assert.Single(await PairsAsync(Original, copy));

        Assert.Equal(1.0, pair.Similarity);
        Assert.Equal(1.0, pair.SameNames);
        Assert.Equal("Snippet::A.Total(int[], int)", pair.First.Symbol.Value);
        Assert.Equal("Snippet::B.Total(int[], int)", pair.Second.Symbol.Value);
        Assert.Equal((4, 19), (pair.First.FirstLine, pair.First.LastLine));
        Assert.DoesNotContain(pair.First.Lines.Concat(pair.Second.Lines), line => line.Differs);
    }

    [Fact]
    public async Task ARenamedCopyWithOtherLiteralsIsAnExactPair()
    {
        var copy = """
            class B
            {
                long Cost(long[] items, long off)
                {
                    var acc = 0;
                    foreach (var item in items)
                    {
                        if (item > 250)
                        {
                            acc += item - off;
                        }
                        else
                        {
                            acc += item;
                        }
                    }

                    return acc * 3;
                }
            }
            """;

        var pair = Assert.Single(await PairsAsync(Original, copy));

        Assert.Equal(1.0, pair.Similarity);
        Assert.InRange(pair.SameNames, 0.0, 0.2);
    }

    [Fact]
    public async Task ACopyWithOneConditionTweakedAndALineAddedIsANearMissPairThatMarksTheLinesThatDiffer()
    {
        var copy = """
            class B
            {
                int Total(int[] prices, int discount)
                {
                    var sum = 0;
                    foreach (var price in prices)
                    {
                        if (price >= 100 && discount > 0)
                        {
                            sum += price - discount;
                        }
                        else
                        {
                            sum += price;
                        }
                    }

                    Log(sum);
                    return sum * 2;
                }

                void Log(int value) { }
            }
            """;

        var pair = Assert.Single(await PairsAsync(Original, copy));

        Assert.InRange(pair.Similarity, 0.7, 0.99);
        Assert.Equal([8, 18], pair.Second.Lines.Where(line => line.Differs).Select(line => line.Number));
        Assert.DoesNotContain(pair.First.Lines, line => line.Differs);
    }

    [Fact]
    public async Task ABlockCopiedIntoTwoLargerMethodsIsAPairOfTheBlockAlone()
    {
        var code = """
            class C
            {
                string Report(int[] prices, int discount, string title)
                {
                    var header = title.Trim().ToUpperInvariant();
                    var sum = 0;
                    foreach (var price in prices)
                    {
                        if (price > 100)
                        {
                            sum += price - discount;
                        }
                    }

                    return header + ": " + sum;
                }

                bool Exceeds(int[] prices, int discount, int limit)
                {
                    if (limit < 0) { throw new System.ArgumentOutOfRangeException(nameof(limit)); }
                    var sum = 0;
                    foreach (var price in prices)
                    {
                        if (price > 100)
                        {
                            sum += price - discount;
                        }
                    }

                    return sum > limit;
                }
            }
            """;

        var pair = Assert.Single(await PairsAsync(code));

        Assert.Equal((5, 15, 20, 30), (pair.First.FirstLine, pair.First.LastLine, pair.Second.FirstLine, pair.Second.LastLine));
    }

    [Fact]
    public async Task TwoPlacesInOneMethodArePairToo()
    {
        var code = """
            class C
            {
                int Twice(int[] prices, int[] fees, int discount)
                {
                    var sum = 0;
                    foreach (var price in prices)
                    {
                        if (price > 100)
                        {
                            sum += price - discount;
                        }
                        else
                        {
                            sum += price;
                        }
                    }

                    foreach (var fee in fees)
                    {
                        if (fee > 100)
                        {
                            sum += fee - discount;
                        }
                        else
                        {
                            sum += fee;
                        }
                    }

                    return sum;
                }
            }
            """;

        var pair = Assert.Single(await PairsAsync(code));

        Assert.Equal(pair.First.Symbol, pair.Second.Symbol);
        Assert.True(pair.First.LastLine < pair.Second.FirstLine);
    }

    [Fact]
    public async Task AShapeRepeatedInTwoMethodsIsOnePairNotOnePerShift()
    {
        var code = """
            class C
            {
                object First(R r) => new R(r.A, r.B, r.C, r.D, r.E, r.F, r.G, r.H, r.I, r.J, r.K, r.L);
                object Second(R q) => new R(q.L, q.K, q.J, q.I, q.H, q.G, q.F, q.E, q.D, q.C, q.B, q.A);
            }

            record R(int A, int B, int C, int D, int E, int F, int G, int H, int I, int J, int K, int L);
            """;

        Assert.Single(await PairsAsync(code));
    }

    [Fact]
    public async Task ALineThatHoldsTokensOutsideTheMatchIsMarked()
    {
        var code = """
            class C
            {
                int First(int[] prices, int discount)
                {
                    var sum = 0;
                    foreach (var price in prices)
                    {
                        if (price > 100)
                        {
                            sum += price - discount;
                        }
                        else
                        {
                            sum += price;
                        }
                    }

                    return Report(sum, prices.Length, discount, "first", 1, 2, 3, 4);
                }

                int Second(int[] fees, int discount)
                {
                    var sum = 0;
                    foreach (var fee in fees)
                    {
                        if (fee > 100)
                        {
                            sum += fee - discount;
                        }
                        else
                        {
                            sum += fee;
                        }
                    }

                    return sum;
                }

                int Report(int a, int b, int c, string d, int e, int f, int g, int h) => a;
            }
            """;

        var pair = Assert.Single(await PairsAsync(code));

        Assert.Equal(18, pair.First.LastLine);
        Assert.True(pair.First.Lines[^1].Differs);
    }

    [Fact]
    public async Task ABlockCopiedIntoMoreMethodsThanASeedIsPairedForEveryCopy()
    {
        var methods = Enumerable.Range(0, 130).Select(index => $$"""
                int M{{index}}(int[] prices, int discount)
                {
                    var sum = 0;
                    foreach (var price in prices)
                    {
                        if (price > 100)
                        {
                            sum += price - discount;
                        }
                        else
                        {
                            sum += price;
                        }
                    }

                    return sum * {{index}};
                }
            """);
        var code = $"class C\n{{\n{string.Join('\n', methods)}\n}}\n";

        var pairs = await PairsAsync(code);

        var paired = pairs.SelectMany(pair => new[] { pair.First.Symbol.Value, pair.Second.Symbol.Value }).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(130, paired.Count);
        Assert.InRange(pairs.Count, 129, 1000);
    }

    [Fact]
    public async Task MethodsThatShareOnlyBoilerplateAreNoPair()
    {
        var other = """
            class B
            {
                string Name(string first, string last)
                {
                    if (string.IsNullOrEmpty(first))
                    {
                        throw new System.ArgumentException("first", nameof(first));
                    }

                    var parts = new System.Collections.Generic.List<string> { first, last };
                    return string.Join(" ", parts).Trim();
                }
            }
            """;

        Assert.Empty(await PairsAsync(Original, other));
    }

    [Fact]
    public async Task APairSmallerThanTheTokenFloorIsNotReportedAndTheFloorIsSettable()
    {
        var small = """
            class S
            {
                int First(int a, int b) { if (a > b) { return a - b; } return b - a + 1; }
                int Second(int x, int y) { if (x > y) { return x - y; } return y - x + 1; }
            }
            """;

        Assert.Empty(await PairsAsync(CloneFloor.Research, small));
        Assert.Single(await PairsAsync(CloneFloor.Research with { Tokens = 20 }, small));
    }

    private static Task<IReadOnlyList<ClonePair>> PairsAsync(params string[] files) => PairsAsync(CloneFloor.Research, files);

    private static async Task<IReadOnlyList<ClonePair>> PairsAsync(CloneFloor floor, params string[] files)
    {
        var facts = await CSharpSnippet.FactsAsync([.. files.Select((code, index) => ($"F{index}.cs", code))]);
        return CloneDetector.Find(facts, floor);
    }
}
