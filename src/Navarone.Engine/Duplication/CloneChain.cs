namespace Navarone.Engine.Duplication;

/// <summary>
/// Exact runs between two token shapes joined in order into one near-miss match. Between two runs lie the tokens one
/// copy added, removed or changed; the tokens the gap's two sides still share, in order, count as matched too. A run
/// joins only while the match keeps at least the floor's similarity, so a gap the copy did not make ends it. Once no
/// run joins, the match grows past its ends by the tokens the two sides still share there, too few in a row to seed a
/// run, such as a line added before the last statement.
/// </summary>
internal sealed class CloneChain
{
    // Two gaps whose sizes multiply above this are not aligned token by token: the match ends there.
    private const long LargestGap = 1_000_000;

    // How far past each end the match looks for tokens the two sides still share.
    private const int EndReach = 100;

    private readonly int[] first;
    private readonly int[] second;
    private readonly List<(int First, int Second, int Length)> matches = [];

    /// <summary>A chain of one run between the given shapes.</summary>
    public CloneChain(int[] first, int[] second, ExactRun run)
    {
        this.first = first;
        this.second = second;
        FirstStart = run.FirstStart;
        SecondStart = run.SecondStart;
        FirstEnd = run.FirstEnd;
        SecondEnd = run.SecondEnd;
        Matched = run.Length;
        matches.Add((run.FirstStart, run.SecondStart, run.Length));
    }

    /// <summary>The first token of the match in the first shape.</summary>
    public int FirstStart { get; private set; }

    /// <summary>The first token of the match in the second shape.</summary>
    public int SecondStart { get; private set; }

    /// <summary>The position just past the match in the first shape.</summary>
    public int FirstEnd { get; private set; }

    /// <summary>The position just past the match in the second shape.</summary>
    public int SecondEnd { get; private set; }

    /// <summary>How many tokens the two sides match, in order.</summary>
    public int Matched { get; private set; }

    /// <summary>The size of the longer side, in tokens.</summary>
    public int Tokens => Math.Max(FirstEnd - FirstStart, SecondEnd - SecondStart);

    /// <summary>Whether both sides lie in one method, whose two places may not overlap.</summary>
    public bool OneMethod => ReferenceEquals(first, second);

    /// <summary>Whether the two sides lie apart: always in two methods, and in one method when the first ends before the second starts.</summary>
    public bool Apart => !OneMethod || FirstEnd <= SecondStart;

    /// <summary>The share of the longer side's tokens the other side matches.</summary>
    public double Similarity => (double)Matched / Tokens;

    /// <summary>Each matched stretch, a run or a token the sides share between or past the runs, by its start on each side and its length.</summary>
    public IReadOnlyList<(int First, int Second, int Length)> Matches => matches;

    /// <summary>
    /// The part of the run that follows the match on both sides, its front cut off where it overlaps the match, or
    /// null when nothing of it follows. In one method the first side may not reach the second.
    /// </summary>
    public ExactRun? Following(ExactRun run)
    {
        var overlap = Math.Max(0, Math.Max(FirstEnd - run.FirstStart, SecondEnd - run.SecondStart));
        if (overlap >= run.Length)
        {
            return null;
        }

        var following = run with { FirstStart = run.FirstStart + overlap, SecondStart = run.SecondStart + overlap, Length = run.Length - overlap };
        if (OneMethod && following.FirstEnd > SecondStart)
        {
            return null;
        }

        return following;
    }

    /// <summary>
    /// Joins a run that follows the match when the joined match keeps at least the given similarity, and says whether
    /// it did. The tokens the gap's two sides share, in order, count as matched.
    /// </summary>
    public bool TryJoin(ExactRun run, double floor)
    {
        var firstGap = run.FirstStart - FirstEnd;
        var secondGap = run.SecondStart - SecondEnd;
        var tokens = Math.Max(run.FirstEnd - FirstStart, run.SecondEnd - SecondStart);
        var atBest = Matched + Math.Min(firstGap, secondGap) + run.Length;
        if ((double)atBest / tokens < floor || (long)firstGap * secondGap > LargestGap)
        {
            return false;
        }

        var gap = new TokenAlignment(new Stretch(first, FirstEnd, 1, firstGap), new Stretch(second, SecondEnd, 1, secondGap));
        var matched = Matched + gap.Shared(firstGap, secondGap) + run.Length;
        if ((double)matched / tokens < floor)
        {
            return false;
        }

        AddShared(gap.Pairs(firstGap, secondGap));
        matches.Add((run.FirstStart, run.SecondStart, run.Length));
        Matched = matched;
        FirstEnd = run.FirstEnd;
        SecondEnd = run.SecondEnd;
        return true;
    }

    /// <summary>
    /// Grows the match past each end by the tokens the two sides still share there, where they outweigh the tokens left
    /// unmatched and the match keeps at least the given similarity, ending on a shared token. Two places in one method
    /// stay apart.
    /// </summary>
    public void GrowEnds(double floor)
    {
        var firstLimit = OneMethod ? SecondStart : first.Length;
        var secondFloor = OneMethod ? FirstEnd : 0;
        var after = new TokenAlignment(
            new Stretch(first, FirstEnd, 1, Math.Clamp(firstLimit - FirstEnd, 0, EndReach)),
            new Stretch(second, SecondEnd, 1, Math.Clamp(second.Length - SecondEnd, 0, EndReach)));
        var (firstAfter, secondAfter) = FurthestEnd(after, floor);
        AddShared(after.Pairs(firstAfter, secondAfter));
        Matched += after.Shared(firstAfter, secondAfter);
        FirstEnd += firstAfter;
        SecondEnd += secondAfter;

        var before = new TokenAlignment(
            new Stretch(first, FirstStart - 1, -1, Math.Clamp(FirstStart, 0, EndReach)),
            new Stretch(second, SecondStart - 1, -1, Math.Clamp(SecondStart - secondFloor, 0, EndReach)));
        var (firstBefore, secondBefore) = FurthestEnd(before, floor);
        AddShared(before.Pairs(firstBefore, secondBefore));
        Matched += before.Shared(firstBefore, secondBefore);
        FirstStart -= firstBefore;
        SecondStart -= secondBefore;
    }

    // How many tokens past the end on each side the match grows to: the end on a shared token where the shared tokens
    // outweigh the tokens left unmatched on both sides the most, while the match keeps the floor's similarity, the
    // nearest of equals; none when no end gains. So a line added before the last statement joins, and the unrelated
    // code around a copied block does not.
    private (int First, int Second) FurthestEnd(TokenAlignment past, double floor)
    {
        var best = (First: 0, Second: 0, Gain: 0);
        var firstLength = FirstEnd - FirstStart;
        var secondLength = SecondEnd - SecondStart;
        for (var down = 1; down <= past.FirstLength; down++)
        {
            for (var across = 1; across <= past.SecondLength; across++)
            {
                var shared = past.Shared(down, across);
                var unmatched = down + across - (2 * shared);
                var gain = shared - unmatched;
                var tokens = Math.Max(firstLength + down, secondLength + across);
                var keepsFloor = (double)(Matched + shared) / tokens >= floor;
                if (gain > best.Gain && keepsFloor && past.Ends(down, across))
                {
                    best = (down, across, gain);
                }
            }
        }

        return (best.First, best.Second);
    }

    private void AddShared(List<(int First, int Second)> pairs)
    {
        matches.AddRange(pairs.Select(pair => (pair.First, pair.Second, 1)));
    }
}
