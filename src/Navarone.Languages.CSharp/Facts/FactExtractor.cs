using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Navarone.Engine.Configuration;
using Navarone.Engine.Facts;

namespace Navarone.Languages.CSharp.Facts;

/// <summary>
/// Turns a loaded solution into facts: it walks the solution's documents and hands each type to its TypeBuilder, each
/// member to MethodReader and InitializerReader. Each call and field use is the symbol the compiler binds it to, as
/// SymbolUses reads it, so the facts name declarations, not text. The `// navarone: &lt;id&gt;` comments at a method
/// or a class become its NavaroneIds.
/// </summary>
internal static class FactExtractor
{
    // The compiler's errors for a missing reference: a metadata file not found, a type defined in an assembly not
    // referenced, and a type or namespace not found, in a namespace or at all.
    private static readonly string[] MissingReferences = ["CS0006", "CS0012", "CS0234", "CS0246"];

    /// <summary>
    /// Walks every C# document of the solution and returns its facts. They are complete unless a project the ignores
    /// leave in misses a type, a namespace or an assembly it references, as an unrestored project does. A file two
    /// projects compile, such as a project built for two target frameworks, is read once. Generated files are not read,
    /// and neither is what the given ignores match: a project whose name a project glob matches, or a file a file glob
    /// matches. A field declared in an ignored file is left out of its type.
    /// </summary>
    // navarone: 0000078
    // navarone: 0000081
    public static async Task<CodebaseFacts> ExtractAsync(Solution solution, Ignores ignores, CancellationToken cancellationToken)
    {
        var root = Path.GetDirectoryName(solution.FilePath);
        var types = new Dictionary<SymbolId, TypeBuilder>();
        var readFiles = new HashSet<string>(StringComparer.Ordinal);
        var compilations = new List<Compilation>();
        foreach (var project in solution.Projects)
        {
            if (project.Language != LanguageNames.CSharp)
            {
                continue;
            }

            if (ignores.IgnoresProject(NameOf(project)))
            {
                continue;
            }

            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation is null)
            {
                continue;
            }

            compilations.Add(compilation);
            foreach (var document in project.Documents)
            {
                if (ignores.IgnoresFile(document.FilePath))
                {
                    continue;
                }

                var tree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
                if (tree is null)
                {
                    continue;
                }

                var path = SourcePlaces.PathOf(root, document.FilePath, document.Name);
                if (IsGenerated(path))
                {
                    continue;
                }

                var file = document.FilePath ?? path;
                if (readFiles.Contains(file))
                {
                    continue;
                }

                readFiles.Add(file);
                Read(compilation.GetSemanticModel(tree), root, path, types, ignores, cancellationToken);
            }
        }

        var facts = new List<TypeFacts>();
        foreach (var type in types.Values)
        {
            facts.Add(type.Build());
        }

        var complete = !compilations.Any(compilation => MissesReferences(compilation, cancellationToken));
        return new CodebaseFacts(facts, complete);
    }

    // Whether the compilation misses a type, a namespace or an assembly it references, as a project whose packages are
    // not restored does. Its symbols cannot bind, so its facts are not those a whole load builds.
    private static bool MissesReferences(Compilation compilation, CancellationToken cancellationToken)
    {
        var diagnostics = compilation.GetDiagnostics(cancellationToken);
        return diagnostics.Any(diagnostic => MissingReferences.Contains(diagnostic.Id));
    }

    // A project's name: its project file's name without the extension, so a project built for two target frameworks
    // has one name. A project without a file is named as the workspace names it.
    private static string NameOf(Project project)
    {
        if (string.IsNullOrEmpty(project.FilePath))
        {
            return project.Name;
        }

        return Path.GetFileNameWithoutExtension(project.FilePath);
    }

    // Each type declared in the document, with its members, and the program its top-level statements declare.
    private static void Read(SemanticModel model, string? directory, string path, Dictionary<SymbolId, TypeBuilder> types, Ignores ignores, CancellationToken cancellationToken)
    {
        var root = (CompilationUnitSyntax)model.SyntaxTree.GetRoot(cancellationToken);
        foreach (var declaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            if (model.GetDeclaredSymbol(declaration, cancellationToken) is not { } type)
            {
                continue;
            }

            var builder = TypeOf(type, types, directory, ignores);
            var location = SourcePlaces.LocationOf(type, declaration, path);
            var ids = NavaroneComments.IdsIn(declaration);
            var code = NavaroneComments.CodeWithout(declaration);
            var written = SourcePlaces.WrittenOf(declaration, declaration);
            builder.AddDeclaration(location, ids, code, written);
            foreach (var member in declaration.Members)
            {
                builder.Methods.AddRange(MethodReader.MethodsOf(member, model, path, cancellationToken));
                builder.Methods.AddRange(InitializerReader.InitializersOf(member, model, path, cancellationToken));
                var bodiless = MethodReader.BodilessOf(member, model, path, cancellationToken);
                if (bodiless is not null)
                {
                    builder.Bodiless.Add(bodiless);
                }
            }

            var primary = MethodReader.PrimaryConstructorOf(type, declaration, model, path);
            if (primary is not null)
            {
                builder.Methods.Add(primary);
            }
        }

        var globals = root.Members.OfType<GlobalStatementSyntax>().ToList();
        if (globals.Count == 0)
        {
            return;
        }

        if (model.GetDeclaredSymbol(root, cancellationToken) is not IMethodSymbol main)
        {
            return;
        }

        var program = TypeOf(main.ContainingType, types, directory, ignores);
        program.Methods.Add(MethodReader.MainOf(main, globals, model, path));
    }

    // The builder of the given type, made the first time one of its declarations is read.
    private static TypeBuilder TypeOf(INamedTypeSymbol type, Dictionary<SymbolId, TypeBuilder> types, string? directory, Ignores ignores)
    {
        var id = Symbols.Id(type);
        if (types.TryGetValue(id, out var builder))
        {
            return builder;
        }

        builder = TypeBuilder.Of(type, directory, ignores);
        types.Add(id, builder);
        return builder;
    }

    // Generated code is what the build generates under obj/. A source generator's files are not among a project's
    // documents. A file's name or an <auto-generated/> comment does not make it generated: anyone can write either.
    private static bool IsGenerated(string path) => path.Replace('\\', '/').Split('/').Contains("obj");
}
