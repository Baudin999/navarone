using System.Text.Json.Nodes;
using Navarone.Engine.Configuration;
using Navarone.Engine.Facts;
using Navarone.Engine.Lenses;
using Navarone.Engine.Metrics;

namespace Navarone.Engine;

/// <summary>
/// The class view of one source file for a reader: every type declared in it with its assembly, namespace and lines,
/// and each of its members with its kind, whether it is public, its lines, its name in a lens and the piece of the
/// class it belongs to, as the check that splits a class into pieces counts them. It is built from facts and writes
/// nothing.
/// </summary>
public sealed class ClassView
{
    private const string Method = "method";
    private const string Constructor = "constructor";
    private const string Property = "property";
    private const string Field = "field";

    private readonly string path;
    private readonly List<ViewedType> types;

    private ClassView(string path, List<ViewedType> types)
    {
        this.path = path;
        this.types = types;
    }

    /// <summary>
    /// The class view of the source file at the given path, relative to the repo, from the facts of the code. The
    /// pieces are those of the first of the setup's checks that splits a class into pieces, against its limit.
    /// </summary>
    // navarone: 0000089
    public static async Task<ClassView> ReadAsync(EngineSetup setup, Code code, string path, CancellationToken cancellationToken)
    {
        var facts = await code.FactsAsync(cancellationToken).ConfigureAwait(false);
        var check = setup.Checks.OfType<IClassPieces>().FirstOrDefault();
        return Of(facts, code, check, setup.Limits, path);
    }

    /// <summary>
    /// The class view of the given facts for the file at the given repo-relative path. A type is in the file when its
    /// first declaration is, and only the members declared in that file are shown. Without a check there are no pieces.
    /// </summary>
    internal static ClassView Of(CodebaseFacts facts, Code code, IClassPieces? check, CheckLimits limits, string path)
    {
        var queries = new LensQueries(facts);
        var names = facts.Types.ToDictionary(type => type.Symbol, type => type.Name);
        var viewed = new List<ViewedType>();
        foreach (var type in facts.Types)
        {
            if (RepoPaths.Of(code.RepoRoot, code.Codebases, type.Location.Path) != path)
            {
                continue;
            }

            string? outer = null;
            if (type.ContainingType is { } containing)
            {
                outer = names.GetValueOrDefault(containing);
            }

            Lcom4View? lcom4 = null;
            var numbers = new Dictionary<SymbolId, int?>();
            if (check?.Pieces(type) is { } pieces)
            {
                lcom4 = new Lcom4View(pieces.Count, limits.For(check.Id));
                numbers = PieceNumbers(pieces);
            }

            var members = MembersOf(type, queries, numbers);
            viewed.Add(new ViewedType(type.Assembly, type.Namespace, type.Name, outer, type.Written.Line, type.Written.EndLine, lcom4, members));
        }

        List<ViewedType> ordered = [.. viewed.OrderBy(type => type.First).ThenBy(type => type.Name, StringComparer.Ordinal)];
        return new ClassView(path, ordered);
    }

    /// <summary>
    /// The class view as one JSON document: the commit, the file's path and every type in it in line order, each with
    /// its members in line order. A type the check does not measure, such as an interface, has a null lcom4, and a
    /// member in no piece a null piece.
    /// </summary>
    public JsonObject Json(string? commit)
    {
        return new JsonObject
        {
            ["commit"] = commit,
            ["path"] = path,
            ["types"] = new JsonArray([.. types.Select(TypeJson)]),
        };
    }

    // Each method a type declares in its file, but for an initializer, which its field stands for, and each field.
    private static List<ViewedMember> MembersOf(TypeFacts type, LensQueries queries, Dictionary<SymbolId, int?> pieces)
    {
        var file = type.Location.Path;
        var fields = type.Fields.Select(field => field.Symbol).ToHashSet();
        var members = new List<ViewedMember>();
        foreach (var field in type.Fields)
        {
            if (field.Location.Path != file)
            {
                continue;
            }

            var kind = Field;
            if (field.IsProperty)
            {
                kind = Property;
            }

            var piece = pieces.GetValueOrDefault(field.Symbol);
            members.Add(new ViewedMember(field.Name, kind, field.IsPublic, field.Written.Line, field.Written.EndLine, null, piece));
        }

        foreach (var method in type.Methods.DistinctBy(method => method.Symbol))
        {
            if (fields.Contains(method.Symbol))
            {
                continue;
            }

            if (method.Location.Path != file)
            {
                continue;
            }

            var lens = queries.NameOf(method);
            var piece = pieces.GetValueOrDefault(method.Symbol);
            members.Add(new ViewedMember(method.Name, KindOf(method), method.IsPublic, method.Written.Line, method.Written.EndLine, lens, piece));
        }

        return [.. members.OrderBy(member => member.First).ThenBy(member => member.Name, StringComparer.Ordinal)];
    }

    private static string KindOf(MethodFacts method)
    {
        if (method.IsConstructor)
        {
            return Constructor;
        }

        if (method.IsAccessor)
        {
            return Property;
        }

        return Method;
    }

    // The number of each piece, from 1, by the symbols of its methods and fields.
    private static Dictionary<SymbolId, int?> PieceNumbers(IReadOnlyList<ClassPiece> pieces)
    {
        var numbers = new Dictionary<SymbolId, int?>();
        for (var index = 0; index < pieces.Count; index++)
        {
            foreach (var symbol in pieces[index].Methods.Concat(pieces[index].Fields))
            {
                numbers.TryAdd(symbol, index + 1);
            }
        }

        return numbers;
    }

    private static JsonNode TypeJson(ViewedType type)
    {
        JsonObject? lcom4 = null;
        if (type.Lcom4 is { } measured)
        {
            lcom4 = new JsonObject
            {
                ["value"] = measured.Value,
                ["limit"] = measured.Limit,
                ["above"] = measured.Value > measured.Limit,
            };
        }

        return new JsonObject
        {
            ["assembly"] = type.Assembly,
            ["namespace"] = type.Namespace,
            ["name"] = type.Name,
            ["containingType"] = type.Outer,
            ["firstLine"] = type.First,
            ["lastLine"] = type.Last,
            ["lcom4"] = lcom4,
            ["members"] = new JsonArray([.. type.Members.Select(MemberJson)]),
        };
    }

    private static JsonNode MemberJson(ViewedMember member) => new JsonObject
    {
        ["name"] = member.Name,
        ["kind"] = member.Kind,
        ["public"] = member.Public,
        ["firstLine"] = member.First,
        ["lastLine"] = member.Last,
        ["lens"] = member.Lens,
        ["piece"] = member.Piece,
    };

    private sealed record ViewedType(string Assembly, string Namespace, string Name, string? Outer, int First, int Last, Lcom4View? Lcom4, List<ViewedMember> Members);

    private sealed record Lcom4View(int Value, int Limit);

    private sealed record ViewedMember(string Name, string Kind, bool Public, int First, int Last, string? Lens, int? Piece);
}
