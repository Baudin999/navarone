namespace Navarone.Engine.Duplication;

/// <summary>
/// The longest common subsequence of every two prefixes of two stretches of token shapes. Each stretch is read from
/// its start one step at a time, forwards with step 1 or backwards with step -1, so a match can grow at either end.
/// </summary>
internal sealed class TokenAlignment
{
    private readonly Stretch first;
    private readonly Stretch second;
    private readonly int width;
    private readonly int[] lengths;

    /// <summary>Aligns every prefix of the two stretches.</summary>
    public TokenAlignment(Stretch first, Stretch second)
    {
        this.first = first;
        this.second = second;
        width = second.Length + 1;
        lengths = new int[(first.Length + 1) * width];
        for (var down = 1; down <= first.Length; down++)
        {
            for (var across = 1; across <= second.Length; across++)
            {
                var here = (down * width) + across;
                if (Ends(down, across))
                {
                    lengths[here] = lengths[here - width - 1] + 1;
                }
                else
                {
                    lengths[here] = Math.Max(lengths[here - width], lengths[here - 1]);
                }
            }
        }
    }

    /// <summary>How many tokens the first stretch holds.</summary>
    public int FirstLength => first.Length;

    /// <summary>How many tokens the second stretch holds.</summary>
    public int SecondLength => second.Length;

    /// <summary>How many tokens the first stretch's first count and the second stretch's first count share, in order.</summary>
    public int Shared(int firstCount, int secondCount) => lengths[(firstCount * width) + secondCount];

    /// <summary>Whether the last tokens of the two prefixes are equal, so a match can end there.</summary>
    public bool Ends(int firstCount, int secondCount) =>
        first.Shape[first.At(firstCount - 1)] == second.Shape[second.At(secondCount - 1)];

    /// <summary>The positions in the two shapes of the tokens the two prefixes share, in order.</summary>
    public List<(int First, int Second)> Pairs(int firstCount, int secondCount)
    {
        var pairs = new List<(int First, int Second)>();
        var down = firstCount;
        var across = secondCount;
        while (down > 0 && across > 0)
        {
            if (Ends(down, across))
            {
                pairs.Add((first.At(down - 1), second.At(across - 1)));
                down--;
                across--;
            }
            else if (Shared(down - 1, across) >= Shared(down, across - 1))
            {
                down--;
            }
            else
            {
                across--;
            }
        }

        pairs.Reverse();
        return pairs;
    }
}

/// <summary>Length tokens of a shape read from Start, one Step at a time.</summary>
internal readonly record struct Stretch(int[] Shape, int Start, int Step, int Length)
{
    /// <summary>The position in the shape of the stretch's token at the given offset.</summary>
    public int At(int offset) => Start + (offset * Step);
}
