using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Navarone.Engine.Configuration;
using Navarone.Engine.Facts;
using Navarone.Languages.CSharp.Facts;

namespace Navarone.Tests.Support;

/// <summary>Reads C# source through the provider's fact extractor, in memory, without MSBuild.</summary>
internal static class CSharpSnippet
{
    private static readonly MetadataReference[] References =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)),
    ];

    /// <summary>The facts of the given files, keyed by file name, compiled as one assembly named Snippet.</summary>
    public static Task<CodebaseFacts> FactsAsync(params (string Name, string Code)[] files) =>
        FactsAsync(Ignores.Nothing("/snippet"), [.. files.Select(file => ("Snippet", file.Name, file.Code))]);

    /// <summary>
    /// The facts of the given files, each in the project of its name, read with the given ignores. Each file lies at
    /// /snippet/&lt;project&gt;/&lt;name&gt;, and the projects compile as one assembly each without references between them.
    /// </summary>
    public static async Task<CodebaseFacts> FactsAsync(Ignores ignores, params (string Project, string Name, string Code)[] files)
    {
        using var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;
        foreach (var group in files.GroupBy(file => file.Project, StringComparer.Ordinal))
        {
            var project = solution
                .AddProject(group.Key, group.Key, LanguageNames.CSharp)
                .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.ConsoleApplication))
                .WithMetadataReferences(References);
            foreach (var (_, name, code) in group)
            {
                project = project.AddDocument(name, SourceText.From(code), filePath: $"/snippet/{group.Key}/{name}").Project;
            }

            solution = project.Solution;
        }

        return await FactExtractor.ExtractAsync(solution, ignores, TestContext.Current.CancellationToken);
    }

    /// <summary>The facts of one file.</summary>
    public static Task<CodebaseFacts> FactsAsync(string code) => FactsAsync(("Snippet.cs", code));

    /// <summary>The method whose symbol ends with the given text.</summary>
    public static MethodFacts Method(this CodebaseFacts facts, string symbolEnd) =>
        facts.Types.SelectMany(type => type.Methods).Single(method => method.Symbol.Value.EndsWith(symbolEnd, StringComparison.Ordinal));
}
