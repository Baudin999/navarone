using System.Globalization;
using System.Text.Json.Nodes;
using Navarone.Engine;
using Navarone.Engine.Duplication;
using Navarone.Engine.Lenses;
using Navarone.Engine.Reasons;

namespace Navarone.Cli;

/// <summary>The research commands: what the lens shows, a problem's evidence and the clone pairs. Each is read-only: it grants no id.</summary>
internal sealed class ResearchCommands(Repo repo)
{
    /// <summary>`navarone lens --json ...`: the lens as one JSON document for brain's window. It logs no cost.</summary>
    public static InRepo LensJson(string[] words)
    {
        var text = string.Join(' ', words);
        return new InRepo("lens", Costed: false, current => new ResearchCommands(current).LensJsonAsync(text));
    }

    /// <summary>`navarone lens ...`: the lens as markdown. Its words are joined and read as one lens, so nothing is quoted.</summary>
    public static InRepo LensMarkdown(string[] words)
    {
        var text = string.Join(' ', words);
        return new InRepo("lens", Costed: true, current => new ResearchCommands(current).LensAsync(text));
    }

    /// <summary>`navarone map --json`: the map of the code for brain's window. It logs no cost.</summary>
    public static InRepo Map()
    {
        return new InRepo("map", Costed: false, current => new ResearchCommands(current).MapAsync());
    }

    /// <summary>`navarone classes &lt;path&gt; --json`: the class view of a source file for brain's window. It logs no cost.</summary>
    public static InRepo Classes(string path)
    {
        return new InRepo("classes", Costed: false, current => new ResearchCommands(current).ClassesAsync(path));
    }

    /// <summary>`navarone evidence &lt;id&gt; --json`: the problem's evidence document.</summary>
    public static InRepo Evidence(ProblemId id)
    {
        return new InRepo("evidence", Costed: true, current => new ResearchCommands(current).EvidenceAsync(id));
    }

    /// <summary>`navarone duplicates ...`: the clone pairs at or above the floor.</summary>
    // navarone: 0000074
    public static InRepo Duplicates(string[] options)
    {
        return new InRepo("duplicates", Costed: true, current => new ResearchCommands(current).DuplicatesAsync(options));
    }

    // `navarone evidence <id> --json`: prints the problem's evidence document from the code as it is now. It never
    // writes reasons.json. A problem whose check builds no evidence, or an id that matches no problem in the code as it
    // is now, is a usage error.
    private async Task<int> EvidenceAsync(ProblemId id)
    {
        var finding = await Analysis.FindingOfAsync(repo.Setup, repo.Codebases, id, CancellationToken.None).ConfigureAwait(false);
        if (finding is null)
        {
            await repo.Error.WriteLineAsync($"navarone: no problem with id {id.Value} in the code as it is now").ConfigureAwait(false);
            return ExitCodes.UsageError;
        }

        if (finding.Evidence is null)
        {
            await repo.Error.WriteLineAsync($"navarone: the {finding.Check} check builds no evidence yet").ConfigureAwait(false);
            return ExitCodes.UsageError;
        }

        await repo.Output.WriteLineAsync(finding.Evidence.Document(finding)).ConfigureAwait(false);
        return ExitCodes.Printed;
    }

    // `navarone lens <kind> <name> <view>...`: prints what the lens shows as markdown. Its words are joined and read as
    // one lens, so nothing is quoted. It writes only its cost. It opens with the version of the code. A lens that does
    // not parse, or a name that matches no method, is a usage error that says where in the lens. A git status that fails
    // or times out while the lens reads the version is a usage error too.
    private async Task<int> LensAsync(string text)
    {
        var lens = Lens.Parse(text);
        var shown = await lens.ShowAsync(repo.Code, CancellationToken.None).ConfigureAwait(false);
        var version = LensVersion.Of(Path.GetFullPath(repo.Setup.RepoRoot));
        await repo.Output.WriteAsync(version + shown).ConfigureAwait(false);
        return ExitCodes.Printed;
    }

    // `navarone lens --json <kind> <name> <view>...`: the same lens as one JSON document for brain's window, the commit
    // of HEAD and the sections. Unlike the markdown lens it writes no cost.
    private async Task<int> LensJsonAsync(string text)
    {
        var lens = Lens.Parse(text);
        var page = await lens.PageAsync(repo.Code, CancellationToken.None).ConfigureAwait(false);
        var commit = LensVersion.Commit(Path.GetFullPath(repo.Setup.RepoRoot));
        await repo.Output.WriteLineAsync(page.Json(commit).ToJsonString()).ConfigureAwait(false);
        return ExitCodes.Printed;
    }

    // `navarone map --json`: the map of the code as one JSON document: the version, every file with its methods and every
    // red problem. It writes nothing, not even an id.
    private async Task<int> MapAsync()
    {
        var map = await Analysis.MapAsync(repo.Setup, repo.Codebases, CancellationToken.None).ConfigureAwait(false);
        var root = Path.GetFullPath(repo.Setup.RepoRoot);
        var commit = LensVersion.Commit(root);
        var changed = LensVersion.Changed(root);
        await repo.Output.WriteLineAsync(map.Json(commit, changed).ToJsonString()).ConfigureAwait(false);
        return ExitCodes.Printed;
    }

    // `navarone classes <path> --json`: the class view of the source file at the path, relative to the repo, as one JSON
    // document: the commit and every type in the file with its members and their pieces. It writes nothing, not even an
    // id. A path to no file under the repo is a usage error.
    private async Task<int> ClassesAsync(string path)
    {
        var root = Path.GetFullPath(repo.Setup.RepoRoot);
        var file = Path.GetFullPath(Path.Combine(root, path));
        var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
        var outside = relative.StartsWith("../", StringComparison.Ordinal);
        if (outside || !File.Exists(file))
        {
            await repo.Error.WriteLineAsync($"navarone: no file {path} in the repo").ConfigureAwait(false);
            return ExitCodes.UsageError;
        }

        var view = await ClassView.ReadAsync(repo.Setup, repo.Code, relative, CancellationToken.None).ConfigureAwait(false);
        var commit = LensVersion.Commit(root);
        await repo.Output.WriteLineAsync(view.Json(commit).ToJsonString()).ConfigureAwait(false);
        return ExitCodes.Printed;
    }

    // `navarone duplicates [--similarity <share>] [--tokens <count>]`: prints every clone pair at or above the floor as
    // markdown, the research floor unless the options set it. It writes only its cost. An option Navarone cannot read is
    // a usage error.
    private async Task<int> DuplicatesAsync(string[] options)
    {
        var floor = FloorOf(options);
        if (floor is null)
        {
            await repo.Error.WriteLineAsync("navarone: duplicates takes --similarity <share from 0 to 1> and --tokens <count of 1 or more>").ConfigureAwait(false);
            return ExitCodes.UsageError;
        }

        var provider = repo.Setup.Provider;
        var pairs = await CloneDetector.FindAsync(repo.Code, floor, CancellationToken.None).ConfigureAwait(false);
        await CloneReport.WriteAsync(repo.Output, pairs, floor, provider.Language).ConfigureAwait(false);
        return ExitCodes.Printed;
    }

    // The floor the options set over the research floor; null when an option is unknown, given twice or without a
    // value, or its value is out of range.
    private static CloneFloor? FloorOf(string[] options)
    {
        if (options.Length % 2 != 0)
        {
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < options.Length; index += 2)
        {
            if (!values.TryAdd(options[index], options[index + 1]))
            {
                return null;
            }
        }

        var floor = CloneFloor.Research;
        if (values.Remove("--similarity", out var share))
        {
            var similarity = Share(share);
            if (similarity is null)
            {
                return null;
            }

            floor = floor with { Similarity = similarity.Value };
        }

        if (values.Remove("--tokens", out var count))
        {
            var tokens = Count(count);
            if (tokens is null)
            {
                return null;
            }

            floor = floor with { Tokens = tokens.Value };
        }

        if (values.Count > 0)
        {
            return null;
        }

        return floor;
    }

    // A share above 0 and at most 1, or null.
    private static double? Share(string text)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var share))
        {
            return null;
        }

        if (share <= 0 || share > 1)
        {
            return null;
        }

        return share;
    }

    // A count of 1 or more, or null.
    private static int? Count(string text)
    {
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
        {
            return null;
        }

        if (count < 1)
        {
            return null;
        }

        return count;
    }
}
