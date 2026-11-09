namespace Navarone.Engine.Duplication;

/// <summary>
/// A run of equal normalised tokens shared by two methods, or by two places in one method: First is the method that
/// comes first, and in one method the place that comes first.
/// </summary>
internal readonly record struct ExactRun(int First, int Second, int FirstStart, int SecondStart, int Length)
{
    /// <summary>The position in the first method just past the run.</summary>
    public int FirstEnd => FirstStart + Length;

    /// <summary>The position in the second method just past the run.</summary>
    public int SecondEnd => SecondStart + Length;

    /// <summary>How far the run lies along the second method compared to the first; runs on one diagonal line up.</summary>
    public int Diagonal => SecondStart - FirstStart;
}

/// <summary>
/// Finds every maximal run of at least SeedLength equal tokens between the methods' token shapes. Each run of
/// SeedLength tokens is hashed, equal hashes seed a run, and the run is grown token by token as far as the tokens stay
/// equal, so a hash collision never makes a run.
/// </summary>
internal static class ExactRuns
{
    /// <summary>The fewest equal tokens in a row that seed a match.</summary>
    public const int SeedLength = 10;

    // A seed written in more places than this is common, such as a call with two arguments or a block copied into many
    // methods. Pairing every two of its places would cost the square of their count, so each place is paired with the
    // next one in order instead: every copy still joins a pair, at a cost that grows with the count.
    private const int CommonSeed = 100;

    // A prime base for the polynomial hash of a seed.
    private const ulong HashBase = 1_000_003;

    /// <summary>Every maximal run, once each, from the token shapes of the methods, one array per method.</summary>
    public static List<ExactRun> Of(int[][] shapes)
    {
        var seeds = SeedsOf(shapes);
        seeds.Sort(InOrder);
        var groups = GroupsOf(seeds);
        var places = new Places(shapes, seeds, groups);
        var runs = new List<ExactRun>();
        foreach (var group in groups)
        {
            AddRuns(shapes, seeds, group, places, runs);
        }

        return runs;
    }

    // Seeds by hash, then by method and position, so the places of one seed lie in order.
    private static int InOrder(Seed left, Seed right)
    {
        var byHash = left.Hash.CompareTo(right.Hash);
        if (byHash != 0)
        {
            return byHash;
        }

        var byMethod = left.Method.CompareTo(right.Method);
        return byMethod != 0 ? byMethod : left.Position.CompareTo(right.Position);
    }

    // One seed per position that has SeedLength tokens from it on, with the hash of those tokens.
    private static List<Seed> SeedsOf(int[][] shapes)
    {
        var power = 1UL;
        for (var step = 1; step < SeedLength; step++)
        {
            power *= HashBase;
        }

        var seeds = new List<Seed>();
        for (var method = 0; method < shapes.Length; method++)
        {
            var shape = shapes[method];
            var hash = 0UL;
            for (var position = 0; position < shape.Length; position++)
            {
                if (position >= SeedLength)
                {
                    hash -= (ulong)shape[position - SeedLength] * power;
                }

                hash = (hash * HashBase) + (ulong)shape[position];
                if (position >= SeedLength - 1)
                {
                    seeds.Add(new Seed(hash, method, position - SeedLength + 1));
                }
            }
        }

        return seeds;
    }

    // The stretches of the sorted seeds that share one hash.
    private static List<SeedGroup> GroupsOf(List<Seed> sorted)
    {
        var groups = new List<SeedGroup>();
        var start = 0;
        while (start < sorted.Count)
        {
            var end = start + 1;
            while (end < sorted.Count && sorted[end].Hash == sorted[start].Hash)
            {
                end++;
            }

            groups.Add(new SeedGroup(start, end));
            start = end;
        }

        return groups;
    }

    // Pairs the places that share one seed: every two of them, or for a common seed each with the next. A pair whose
    // tokens one step back are equal too lies inside a run that the same pair one step back starts, when that seed
    // paired them; then it adds nothing.
    private static void AddRuns(int[][] shapes, List<Seed> seeds, SeedGroup group, Places places, List<ExactRun> runs)
    {
        var common = group.Count > CommonSeed;
        for (var left = group.Start; left < group.End; left++)
        {
            var last = common ? Math.Min(left + 2, group.End) : group.End;
            for (var right = left + 1; right < last; right++)
            {
                var (first, second) = Ordered(seeds[left], seeds[right]);
                var firstShape = shapes[first.Method];
                var secondShape = shapes[second.Method];
                var continues = first.Position > 0
                    && second.Position > 0
                    && firstShape[first.Position - 1] == secondShape[second.Position - 1]
                    && places.Paired(first.Method, first.Position - 1, second.Method, second.Position - 1);
                if (continues)
                {
                    continue;
                }

                var length = EqualFrom(firstShape, first.Position, secondShape, second.Position);
                if (length >= SeedLength)
                {
                    runs.Add(new ExactRun(first.Method, second.Method, first.Position, second.Position, length));
                }
            }
        }
    }

    private static (Seed First, Seed Second) Ordered(Seed one, Seed other)
    {
        var oneFirst = one.Method < other.Method || (one.Method == other.Method && one.Position < other.Position);
        return oneFirst ? (one, other) : (other, one);
    }

    // How many tokens from the two positions on are equal.
    private static int EqualFrom(int[] first, int firstStart, int[] second, int secondStart)
    {
        var length = 0;
        while (firstStart + length < first.Length && secondStart + length < second.Length && first[firstStart + length] == second[secondStart + length])
        {
            length++;
        }

        return length;
    }

    private readonly record struct Seed(ulong Hash, int Method, int Position);

    // Where each place's seed lies among the sorted seeds, and whether that seed is common, so a pair can tell whether
    // the seed one step back paired the same two places.
    private sealed class Places
    {
        private readonly int[][] rank;
        private readonly bool[][] common;

        public Places(int[][] shapes, List<Seed> sorted, List<SeedGroup> groups)
        {
            rank = shapes.Select(shape => new int[shape.Length]).ToArray();
            common = shapes.Select(shape => new bool[shape.Length]).ToArray();
            foreach (var group in groups)
            {
                for (var index = group.Start; index < group.End; index++)
                {
                    rank[sorted[index].Method][sorted[index].Position] = index;
                    common[sorted[index].Method][sorted[index].Position] = group.Count > CommonSeed;
                }
            }
        }

        // Whether the seed at the two places, which share it, paired them: every seed that is not common pairs every
        // two of its places, and a common one each place with the next.
        public bool Paired(int firstMethod, int firstPosition, int secondMethod, int secondPosition)
        {
            if (!common[firstMethod][firstPosition])
            {
                return true;
            }

            return Math.Abs(rank[firstMethod][firstPosition] - rank[secondMethod][secondPosition]) == 1;
        }
    }

    // A stretch of the sorted seeds, from Start up to End.
    private readonly record struct SeedGroup(int Start, int End)
    {
        public int Count => End - Start;
    }
}
