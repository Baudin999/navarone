using System.Text.Json.Nodes;
using Navarone.Engine.Facts;
using Navarone.Engine.Lenses;

namespace Navarone.Engine;

/// <summary>
/// A map of the code for a reader: each source file with the methods a lens can name in it, and every red problem with
/// the method it sits in. Paths are relative to the repo. The map is built from facts and writes nothing.
/// </summary>
public sealed class CodeMap
{
    private readonly string repoRoot;
    private readonly IReadOnlyList<string> codebases;
    private readonly List<MappedMethod> methods;
    private readonly Dictionary<SymbolId, string> names;
    private readonly List<SeenProblem> problems;

    private CodeMap(string repoRoot, IReadOnlyList<string> codebases, List<MappedMethod> methods, Dictionary<SymbolId, string> names, List<SeenProblem> problems)
    {
        this.repoRoot = repoRoot;
        this.codebases = codebases;
        this.methods = methods;
        this.names = names;
        this.problems = problems;
    }

    /// <summary>
    /// The map of the given facts. A method no lens can name, such as an accessor, is not in it. A problem's method is
    /// null when its subject is no method a lens can name.
    /// </summary>
    internal static CodeMap Of(CodebaseFacts facts, string repoRoot, IReadOnlyList<string> codebases, IReadOnlyList<SeenProblem> seen)
    {
        var queries = new LensQueries(facts);
        var names = new Dictionary<SymbolId, string>();
        var mapped = new List<MappedMethod>();
        foreach (var type in facts.Types)
        {
            foreach (var method in type.Methods)
            {
                var name = queries.NameOf(method);
                if (name is null)
                {
                    continue;
                }

                names.TryAdd(method.Symbol, name);
                mapped.Add(new MappedMethod(method.Location.Path, name, method.Written.Line, method.Written.EndLine, type.Name));
            }
        }

        return new CodeMap(repoRoot, codebases, mapped, names, [.. seen]);
    }

    /// <summary>
    /// The map as one JSON document: the commit, the files that differ from it, every file with its methods, and every
    /// red problem. A problem's id is null while Navarone has granted none.
    /// </summary>
    public JsonObject Json(string? commit, IReadOnlyList<string> changed)
    {
        var files = new JsonArray();
        var byPath = methods.GroupBy(method => RepoPath(method.Path)).OrderBy(file => file.Key, StringComparer.Ordinal);
        foreach (var file in byPath)
        {
            var inFile = file.OrderBy(method => method.First).ThenBy(method => method.Name, StringComparer.Ordinal);
            files.Add(new JsonObject
            {
                ["path"] = file.Key,
                ["methods"] = new JsonArray([.. inFile.Select(MethodJson)]),
            });
        }

        var ordered = problems
            .OrderBy(problem => problem.Id is null)
            .ThenBy(problem => problem.Id, StringComparer.Ordinal)
            .ThenBy(problem => problem.Path, StringComparer.Ordinal)
            .ThenBy(problem => problem.First);
        return new JsonObject
        {
            ["commit"] = commit,
            ["changed"] = new JsonArray([.. changed.Select(path => JsonValue.Create(path))]),
            ["files"] = files,
            ["problems"] = new JsonArray([.. ordered.Select(ProblemJson)]),
        };
    }

    private static JsonNode MethodJson(MappedMethod method) => new JsonObject
    {
        ["name"] = method.Name,
        ["firstLine"] = method.First,
        ["lastLine"] = method.Last,
        ["type"] = method.Type,
    };

    private JsonNode ProblemJson(SeenProblem problem)
    {
        var state = "red";
        if (problem.Clash)
        {
            state = "clash";
        }

        var method = names.GetValueOrDefault(problem.Symbol);
        return new JsonObject
        {
            ["id"] = problem.Id,
            ["path"] = RepoPath(problem.Path),
            ["firstLine"] = problem.First,
            ["lastLine"] = problem.Last,
            ["method"] = method,
            ["check"] = problem.Check,
            ["state"] = state,
            ["question"] = problem.Says,
        };
    }

    private string RepoPath(string path) => RepoPaths.Of(repoRoot, codebases, path);

    private sealed record MappedMethod(string Path, string Name, int First, int Last, string Type);
}

/// <summary>A red problem as the map sees it: where its subject is, relative to its codebase, and whether a clash holds it.</summary>
internal sealed record SeenProblem(string? Id, string Check, SymbolId Symbol, string Path, int First, int Last, bool Clash, string Says);
