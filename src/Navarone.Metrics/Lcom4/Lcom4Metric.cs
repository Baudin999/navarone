using System.Globalization;
using Navarone.Engine.Configuration;
using Navarone.Engine.Facts;
using Navarone.Engine.Findings;
using Navarone.Engine.Metrics;

namespace Navarone.Metrics.Lcom4;

/// <summary>Hitz and Montazeri: the pieces the method graph of a class falls into.</summary>
public sealed class Lcom4Metric : ISignal
{
    /// <inheritdoc />
    public string Id => "lcom4";

    /// <inheritdoc />
    public SubjectKind Subject => SubjectKind.Class;

    /// <inheritdoc />
    public IReadOnlyList<Finding> Measure(CodebaseFacts facts, CheckLimits limits)
    {
        var limit = limits.For(Id);
        var findings = new List<Finding>();
        foreach (var type in facts.Types.Where(type => !type.IsInterface && !type.IsTest))
        {
            var pieces = PiecesOf(type);
            if (pieces.Count > limit)
            {
                findings.Add(new Finding(Id, type.Symbol, Subject, type.Location, pieces.Count, limit, Question(pieces, limit)));
            }
        }

        return findings;
    }

    // Union-find over the measured methods. Two methods join when they use the same instance field or one calls the
    // other. A static method that calls a constructor of its class, a factory, uses the fields that constructor and the
    // constructors it chains to use. A method that uses no instance field, calls no other method of the class and is
    // called by none says nothing and is left out. Each piece lists its methods, then the fields its methods use, in
    // declaration order.
    // navarone: 0000086
    private static List<(List<string> Methods, List<string> Fields)> PiecesOf(TypeFacts type)
    {
        var names = type.Fields.Where(field => !field.IsStatic).ToDictionary(field => field.Symbol, field => field.Name);
        var constructors = type.Methods.Where(method => method.IsConstructor).DistinctBy(method => method.Symbol).ToDictionary(method => method.Symbol);
        var uses = type.Methods.Where(method => !method.IsConstructor).DistinctBy(method => method.Symbol).ToDictionary(method => method.Symbol, method => UsesOf(method, constructors));
        var own = uses.Keys.ToHashSet();
        var measured = type.Methods.Where(method => !method.IsConstructor).ToList();
        var called = new HashSet<SymbolId>();
        foreach (var method in measured)
        {
            called.UnionWith(OwnCallees(method, own));
        }

        var methods = new List<MethodFacts>();
        foreach (var method in measured)
        {
            var usesAField = uses[method.Symbol].Any(names.ContainsKey);
            var callsAnother = OwnCallees(method, own).Any();
            var isCalled = called.Contains(method.Symbol);
            if (usesAField || callsAnother || isCalled)
            {
                methods.Add(method);
            }
        }

        var parent = Enumerable.Range(0, methods.Count).ToArray();
        var owner = new Dictionary<SymbolId, int>();
        for (var i = 0; i < methods.Count; i++)
        {
            owner.TryAdd(methods[i].Symbol, i);
        }

        var fieldOwner = new Dictionary<SymbolId, int>();
        for (var i = 0; i < methods.Count; i++)
        {
            foreach (var callee in methods[i].Calls.Where(owner.ContainsKey))
            {
                Join(i, owner[callee]);
            }

            foreach (var field in uses[methods[i].Symbol].Where(names.ContainsKey))
            {
                Join(i, fieldOwner.GetValueOrDefault(field, i));
                fieldOwner.TryAdd(field, i);
            }
        }

        var roots = new int[methods.Count];
        for (var i = 0; i < methods.Count; i++)
        {
            roots[i] = Find(i);
        }

        var pieces = new List<(List<string> Methods, List<string> Fields)>();
        foreach (var group in Enumerable.Range(0, methods.Count).GroupBy(i => roots[i]))
        {
            var members = group.Select(i => methods[i].Name).ToList();
            var fields = group.SelectMany(i => uses[methods[i].Symbol]).Where(names.ContainsKey).Distinct().Select(field => names[field]).ToList();
            pieces.Add((members, fields));
        }

        return pieces;

        int Find(int node)
        {
            while (parent[node] != node)
            {
                parent[node] = parent[parent[node]];
                node = parent[node];
            }

            return node;
        }

        void Join(int a, int b) => parent[Find(a)] = Find(b);
    }

    // The methods of the class the given method calls, itself left out.
    private static IEnumerable<SymbolId> OwnCallees(MethodFacts method, HashSet<SymbolId> own) =>
        method.Calls.Where(callee => callee != method.Symbol && own.Contains(callee));

    // The fields a method uses, and for a static factory the fields of each constructor of its class it calls.
    private static List<SymbolId> UsesOf(MethodFacts method, Dictionary<SymbolId, MethodFacts> constructors)
    {
        var uses = new List<SymbolId>(method.FieldUses);
        if (!method.IsStatic)
        {
            return uses;
        }

        foreach (var callee in method.Calls.Where(constructors.ContainsKey))
        {
            uses.AddRange(Constructed(callee, constructors, []));
        }

        return uses;
    }

    // The fields a constructor uses, with those of the constructors it chains to. A constructor already seen adds none.
    private static List<SymbolId> Constructed(SymbolId constructor, Dictionary<SymbolId, MethodFacts> constructors, HashSet<SymbolId> seen)
    {
        var uses = new List<SymbolId>();
        if (seen.Contains(constructor))
        {
            return uses;
        }

        seen.Add(constructor);
        var facts = constructors[constructor];
        uses.AddRange(facts.FieldUses);
        foreach (var chained in facts.Calls.Where(constructors.ContainsKey))
        {
            uses.AddRange(Constructed(chained, constructors, seen));
        }

        return uses;
    }

    private static string Question(List<(List<string> Methods, List<string> Fields)> pieces, int limit)
    {
        var described = new List<string>();
        for (var index = 0; index < pieces.Count; index++)
        {
            described.Add(Describe(pieces[index], index));
        }

        var jobs = string.Join("; ", described);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"lcom4 {pieces.Count} is above the limit of {limit}: this class does {pieces.Count} separate jobs. {jobs}. Why are they one class?");
    }

    private static string Describe((List<string> Methods, List<string> Fields) piece, int index)
    {
        var methods = string.Join(", ", piece.Methods);
        if (piece.Fields.Count == 0)
        {
            return $"piece {index + 1} holds {methods} and uses no field";
        }

        var fields = string.Join(", ", piece.Fields);
        return $"piece {index + 1} holds {methods} over {fields}";
    }
}
