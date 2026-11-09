using Navarone.Engine.Facts;

namespace Navarone.Engine.Duplication;

/// <summary>
/// Finds duplicated code in the normalised tokens of every method, as the provider hands them over. Exact runs of
/// equal tokens seed a match, runs that follow each other on both sides join into one near-miss match while it keeps
/// the floor's similarity, and a match at or above the floor is reported as a clone pair. A match may lie anywhere in
/// a method, so a block copied into a larger method is found as well as a whole copied method, and two places in one
/// method can be a pair. It reads facts only and writes nothing but the facts cache.
/// </summary>
public static class CloneDetector
{
    /// <summary>The facts of the given code and the clone pairs in them at or above the floor.</summary>
    // navarone: 0000024
    public static async Task<IReadOnlyList<ClonePair>> FindAsync(Code code, CloneFloor floor, CancellationToken cancellationToken)
    {
        var facts = await code.FactsAsync(cancellationToken).ConfigureAwait(false);
        return Find(facts, floor);
    }

    /// <summary>The clone pairs at or above the floor, the most similar first, then the largest.</summary>
    public static IReadOnlyList<ClonePair> Find(CodebaseFacts facts, CloneFloor floor)
    {
        var methods = facts.Types
            .SelectMany(type => type.Methods)
            .Where(method => method.Tokens.Count >= ExactRuns.SeedLength)
            .DistinctBy(method => method.Symbol)
            .ToList();
        var shapes = ShapesOf(methods);
        var written = methods.Select(method => method.Written.Text.Split('\n')).ToArray();
        var runs = ExactRuns.Of(shapes);
        var pairs = new List<ClonePair>();
        foreach (var between in runs.GroupBy(run => (run.First, run.Second)))
        {
            var (first, second) = between.Key;
            var chains = Chains(shapes[first], shapes[second], [.. between], floor.Similarity);
            var reported = chains.Where(chain => chain.Tokens >= floor.Tokens && chain.Similarity >= floor.Similarity);
            foreach (var chain in Largest(reported))
            {
                pairs.Add(PairOf(methods[first], written[first], methods[second], written[second], chain));
            }
        }

        return
        [
            .. pairs
                .OrderByDescending(pair => pair.Similarity)
                .ThenByDescending(pair => pair.Tokens)
                .ThenBy(pair => pair.First.Path, StringComparer.Ordinal)
                .ThenBy(pair => pair.First.FirstLine),
        ];
    }

    // Each method's tokens as numbers, one number per distinct token text, so tokens compare as integers.
    private static int[][] ShapesOf(List<MethodFacts> methods)
    {
        var numbers = new Dictionary<string, int>(StringComparer.Ordinal);
        var shapes = new int[methods.Count][];
        for (var index = 0; index < methods.Count; index++)
        {
            var tokens = methods[index].Tokens;
            var shape = new int[tokens.Count];
            for (var position = 0; position < tokens.Count; position++)
            {
                var text = tokens[position].Shape;
                if (!numbers.TryGetValue(text, out var number))
                {
                    number = numbers.Count;
                    numbers.Add(text, number);
                }

                shape[position] = number;
            }

            shapes[index] = shape;
        }

        return shapes;
    }

    // Joins the runs between two shapes into matches. Runs go in order along the first shape; a run inside a longer one
    // on its diagonal adds nothing. Each match starts at the first run no match holds yet and joins the nearest run
    // that follows it on both sides, while the joined match keeps the floor's similarity. Two places in one method, one
    // shape on both sides, may not overlap.
    private static List<CloneChain> Chains(int[] first, int[] second, List<ExactRun> runs, double floor)
    {
        var ordered = Longest(runs);
        var used = new bool[ordered.Count];
        var chains = new List<CloneChain>();
        for (var start = 0; start < ordered.Count; start++)
        {
            if (used[start])
            {
                continue;
            }

            used[start] = true;
            var chain = new CloneChain(first, second, ordered[start]);
            while (Nearest(chain, ordered, used) is { } next && chain.TryJoin(next.Run, floor))
            {
                used[next.Index] = true;
            }

            chain.GrowEnds(floor);
            if (chain.Apart)
            {
                chains.Add(chain);
            }
        }

        return chains;
    }

    // The runs in order along the first shape, without a run that a longer one on its diagonal holds.
    private static List<ExactRun> Longest(List<ExactRun> runs)
    {
        var kept = new List<ExactRun>();
        foreach (var diagonal in runs.GroupBy(run => run.Diagonal))
        {
            var reach = -1;
            foreach (var run in diagonal.OrderBy(run => run.FirstStart).ThenByDescending(run => run.Length))
            {
                if (run.FirstEnd > reach)
                {
                    kept.Add(run);
                    reach = run.FirstEnd;
                }
            }
        }

        return [.. kept.OrderBy(run => run.FirstStart).ThenBy(run => run.SecondStart)];
    }

    // The unused run that follows the chain on both sides with the smallest gaps, its front cut where it overlaps the
    // chain, or null when none follows.
    private static (int Index, ExactRun Run)? Nearest(CloneChain chain, List<ExactRun> runs, bool[] used)
    {
        (int Index, ExactRun Run)? nearest = null;
        var smallest = int.MaxValue;
        for (var index = 0; index < runs.Count; index++)
        {
            if (used[index] || chain.Following(runs[index]) is not { } run)
            {
                continue;
            }

            var gap = run.FirstStart - chain.FirstEnd + run.SecondStart - chain.SecondEnd;
            if (gap < smallest)
            {
                nearest = (index, run);
                smallest = gap;
            }
        }

        return nearest;
    }

    // The largest matches, each without the matches that overlap it on both sides. A repeated shape, such as a list of
    // arguments, matches itself shifted by a step many times over; each shift is the same pair of places.
    private static List<CloneChain> Largest(IEnumerable<CloneChain> chains)
    {
        var kept = new List<CloneChain>();
        foreach (var chain in chains.OrderByDescending(chain => chain.Matched))
        {
            if (!kept.Exists(larger => Overlaps(larger, chain)))
            {
                kept.Add(chain);
            }
        }

        return kept;
    }

    private static bool Overlaps(CloneChain one, CloneChain other) =>
        one.FirstStart < other.FirstEnd
        && other.FirstStart < one.FirstEnd
        && one.SecondStart < other.SecondEnd
        && other.SecondStart < one.SecondEnd;

    // The pair the chain matches, with the lines of each method's written code.
    private static ClonePair PairOf(MethodFacts first, string[] firstWritten, MethodFacts second, string[] secondWritten, CloneChain chain)
    {
        var firstMatched = new HashSet<int>();
        var secondMatched = new HashSet<int>();
        foreach (var match in chain.Matches)
        {
            for (var offset = 0; offset < match.Length; offset++)
            {
                firstMatched.Add(match.First + offset);
                secondMatched.Add(match.Second + offset);
            }
        }

        var firstPiece = PieceOf(first, firstWritten, chain.FirstStart, chain.FirstEnd, firstMatched);
        var secondPiece = PieceOf(second, secondWritten, chain.SecondStart, chain.SecondEnd, secondMatched);
        return new ClonePair(firstPiece, secondPiece, chain.Similarity, chain.Tokens, SameNames(first, second, chain));
    }

    // The share of the matched identifiers spelled the same on both sides, 0 when no identifier matched.
    private static double SameNames(MethodFacts first, MethodFacts second, CloneChain chain)
    {
        var identifiers = 0;
        var same = 0;
        foreach (var match in chain.Matches)
        {
            for (var offset = 0; offset < match.Length; offset++)
            {
                var token = first.Tokens[match.First + offset];
                if (token.Shape != CodeToken.Identifier)
                {
                    continue;
                }

                identifiers++;
                if (token.Text == second.Tokens[match.Second + offset].Text)
                {
                    same++;
                }
            }
        }

        if (identifiers == 0)
        {
            return 0;
        }

        return (double)same / identifiers;
    }

    // The lines of the method the tokens from start up to end span, as written, each marked when it holds a token the
    // other side does not match. A token outside the match on its first or last line is such a token too. Every token
    // lies inside the method's written code, so every line is there to show.
    private static ClonePiece PieceOf(MethodFacts method, string[] written, int start, int end, HashSet<int> matched)
    {
        var tokens = method.Tokens;
        var firstLine = tokens[start].Line;
        var lastLine = tokens[end - 1].Line;
        var differing = new HashSet<int>();
        for (var position = 0; position < tokens.Count; position++)
        {
            var line = tokens[position].Line;
            var shown = line >= firstLine && line <= lastLine;
            if (shown && !matched.Contains(position))
            {
                differing.Add(line);
            }
        }

        var lines = new List<CloneLine>();
        for (var number = firstLine; number <= lastLine; number++)
        {
            var text = written[number - method.Written.Line].TrimEnd('\r');
            lines.Add(new CloneLine(number, text, differing.Contains(number)));
        }

        return new ClonePiece(method.Symbol, method.Location.Path, firstLine, lastLine, lines);
    }
}
