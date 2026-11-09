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
    private readonly List<MappedMethod> methods;
    private readonly List<MappedProblem> problems;

    private CodeMap(List<MappedMethod> methods, List<MappedProblem> problems)
    {
        this.methods = methods;
        this.problems = problems;
    }

    /// <summary>
    /// The map of the given facts. A method no lens can name, such as an accessor, is not in it. A problem's method is
    /// null when its subject is no method a lens can name.
    /// </summary>
    internal static CodeMap Of(CodebaseFacts facts, Code code, IReadOnlyList<SeenProblem> seen)
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
                var path = RepoPath(code, method.Location.Path);
                mapped.Add(new MappedMethod(path, name, method.Written.Line, method.Written.EndLine, type.Name));
            }
        }

        var found = new List<MappedProblem>();
        foreach (var each in seen)
        {
            var path = RepoPath(code, each.Path);
            found.Add(new MappedProblem(each.Id, each.Check, path, each.First, each.Last, names.GetValueOrDefault(each.Symbol), each.Clash, each.Says));
        }

        return new CodeMap(mapped, found);
    }

    /// <summary>
    /// The map as one JSON document: the commit, the files that differ from it, every file with its methods, and every
    /// red problem. A problem's id is null while Navarone has granted none.
    /// </summary>
    public JsonObject Json(string? commit, IReadOnlyList<string> changed)
    {
        var files = new JsonArray();
        foreach (var file in methods.GroupBy(method => method.Path).OrderBy(file => file.Key, StringComparer.Ordinal))
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

    private static JsonNode ProblemJson(MappedProblem problem)
    {
        var state = "red";
        if (problem.Clash)
        {
            state = "clash";
        }

        return new JsonObject
        {
            ["id"] = problem.Id,
            ["path"] = problem.Path,
            ["firstLine"] = problem.First,
            ["lastLine"] = problem.Last,
            ["method"] = problem.Method,
            ["check"] = problem.Check,
            ["state"] = state,
            ["question"] = problem.Says,
        };
    }

    // A fact's path is relative to its codebase's directory. The repo-relative path is the one of the codebases'
    // directories under which the file exists; with none, the path as it is.
    private static string RepoPath(Code code, string path)
    {
        var root = Path.GetFullPath(code.RepoRoot);
        foreach (var codebase in code.Codebases)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(codebase)) ?? root;
            var file = Path.GetFullPath(Path.Combine(directory, path));
            if (File.Exists(file))
            {
                return Path.GetRelativePath(root, file).Replace('\\', '/');
            }
        }

        return path;
    }

    private sealed record MappedMethod(string Path, string Name, int First, int Last, string Type);

    private sealed record MappedProblem(string? Id, string Check, string Path, int First, int Last, string? Method, bool Clash, string Says);
}

/// <summary>A red problem as the map sees it, before its path and method are named: where its subject is, and whether a clash holds it.</summary>
internal sealed record SeenProblem(string? Id, string Check, SymbolId Symbol, string Path, int First, int Last, bool Clash, string Says);
