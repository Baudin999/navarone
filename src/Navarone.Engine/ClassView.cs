using System.Text.Json.Nodes;
using Navarone.Engine.Configuration;
using Navarone.Engine.Facts;
using Navarone.Engine.Lenses;
using Navarone.Engine.Metrics;

namespace Navarone.Engine;

/// <summary>
/// The class view of one source file for a reader: every type declared in it with its assembly, namespace and lines,
/// and each of its members with its kind, whether it is declared public, its lines, its name in a lens and the piece of
/// the class it belongs to, as the check that splits a class into pieces counts them. It is built from facts and
/// writes nothing.
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

            PiecesView? measured = null;
            var numbers = PieceNumbers.None;
            var pieces = check?.Pieces(type);
            if (check is not null && pieces is not null)
            {
                measured = new PiecesView(check.Id, pieces.Count, limits.For(check.Id));
                numbers = new PieceNumbers(pieces);
            }

            var members = MembersOf(type, queries, numbers);
            viewed.Add(new ViewedType(type.Assembly, type.Namespace, type.Name, outer, type.Written.Line, type.Written.EndLine, measured, members));
        }

        List<ViewedType> ordered = [.. viewed.OrderBy(type => type.First).ThenBy(type => type.Name, StringComparer.Ordinal)];
        return new ClassView(path, ordered);
    }

    /// <summary>
    /// The class view as one JSON document: the commit, the file's path and every type in it in line order, each with
    /// its members in the order written. A type the check does not measure, such as an interface, has null pieces, and
    /// a member in no piece a null piece.
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

    // Each field, property and method a type declares in its file, in the order written. A property with a body stands
    // for its accessors, and a field for its initializer.
    private static List<ViewedMember> MembersOf(TypeFacts type, LensQueries queries, PieceNumbers pieces)
    {
        var file = type.Location.Path;
        var standIns = type.Fields.Select(field => field.Symbol).ToHashSet();
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

            var written = field.Written;
            members.Add(new ViewedMember(field.Name, kind, field.IsPublic, written.Line, written.EndLine, field.Location.Column, null, pieces.Of(field.Symbol), []));
        }

        foreach (var property in type.Properties)
        {
            List<SymbolId> accessors = [.. property.Accessors.Select(accessor => accessor.Method)];
            standIns.UnionWith(accessors);
            if (property.Location.Path != file)
            {
                continue;
            }

            var viewed = AccessorsOf(property, type.Methods, pieces);
            var written = property.Written;
            members.Add(new ViewedMember(property.Name, Property, property.IsPublic, written.Line, written.EndLine, property.Location.Column, null, pieces.Shared(accessors), viewed));
        }

        foreach (var method in type.Methods)
        {
            if (standIns.Contains(method.Symbol))
            {
                continue;
            }

            if (method.Location.Path != file)
            {
                continue;
            }

            var kind = Method;
            if (method.IsConstructor)
            {
                kind = Constructor;
            }

            var lens = queries.NameOf(method);
            var written = method.Written;
            members.Add(new ViewedMember(method.Name, kind, method.IsPublic, written.Line, written.EndLine, method.Location.Column, lens, pieces.Of(method.Symbol), []));
        }

        return [.. members.OrderBy(member => member.First).ThenBy(member => member.Column)];
    }

    // Each accessor of the property with its own lines, whether its method is declared public, and its piece.
    private static List<ViewedAccessor> AccessorsOf(PropertyFacts property, IReadOnlyList<MethodFacts> methods, PieceNumbers pieces)
    {
        var accessors = new List<ViewedAccessor>();
        foreach (var accessor in property.Accessors)
        {
            var method = methods.First(method => method.Symbol == accessor.Method);
            var written = accessor.Written;
            accessors.Add(new ViewedAccessor(accessor.Side, method.IsPublic, written.Line, written.EndLine, pieces.Of(accessor.Method)));
        }

        return accessors;
    }

    private static JsonNode TypeJson(ViewedType type)
    {
        JsonObject? pieces = null;
        if (type.Pieces is { } measured)
        {
            pieces = new JsonObject
            {
                ["check"] = measured.Check,
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
            ["pieces"] = pieces,
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
        ["accessors"] = new JsonArray([.. member.Accessors.Select(AccessorJson)]),
    };

    private static JsonNode AccessorJson(ViewedAccessor accessor) => new JsonObject
    {
        ["name"] = accessor.Side,
        ["public"] = accessor.Public,
        ["firstLine"] = accessor.First,
        ["lastLine"] = accessor.Last,
        ["piece"] = accessor.Piece,
    };

    private sealed record ViewedType(string Assembly, string Namespace, string Name, string? Outer, int First, int Last, PiecesView? Pieces, List<ViewedMember> Members);

    private sealed record PiecesView(string Check, int Value, int Limit);

    private sealed record ViewedMember(string Name, string Kind, bool Public, int First, int Last, int Column, string? Lens, int? Piece, List<ViewedAccessor> Accessors);

    private sealed record ViewedAccessor(string Side, bool Public, int First, int Last, int? Piece);

    // The number of each piece, from 1, by the symbols of its methods and fields.
    private sealed class PieceNumbers
    {
        private readonly Dictionary<SymbolId, int> numbers = [];

        public PieceNumbers(IReadOnlyList<ClassPiece> pieces)
        {
            for (var index = 0; index < pieces.Count; index++)
            {
                foreach (var symbol in pieces[index].Methods.Concat(pieces[index].Fields))
                {
                    numbers.TryAdd(symbol, index + 1);
                }
            }
        }

        // No piece for any symbol, for a type the check does not measure.
        public static PieceNumbers None { get; } = new([]);

        // The number of the piece that holds the symbol, or null for one in no piece.
        public int? Of(SymbolId symbol)
        {
            if (numbers.TryGetValue(symbol, out var number))
            {
                return number;
            }

            return null;
        }

        // The piece all the symbols are in, or null when they are in different pieces or in none.
        public int? Shared(IReadOnlyList<SymbolId> symbols)
        {
            var shared = symbols.Select(Of).Distinct().ToList();
            if (shared is not [{ } piece])
            {
                return null;
            }

            return piece;
        }
    }
}
