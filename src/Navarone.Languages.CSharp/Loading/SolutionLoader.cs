using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Navarone.Languages.CSharp.Loading;

/// <summary>
/// Where Roslyn loads a solution: MSBuild evaluates the projects, Roslyn builds a semantic model of them. Every failure
/// MSBuild reports goes to stderr, since the methods of a project it could not read may be missing from the facts. Only
/// a NuGet audit warning, NU1900 to NU1905, does not: it says a package has a known vulnerability, not that code is missing.
/// </summary>
internal static partial class SolutionLoader
{
    /// <summary>
    /// Opens the .sln or .slnx at the given path in the given workspace, which must outlive the solution's use. Failed
    /// is true when MSBuild reported a failure that goes to stderr.
    /// </summary>
    public static async Task<(Solution Solution, bool Failed)> OpenAsync(MSBuildWorkspace workspace, string solutionPath, CancellationToken cancellationToken)
    {
        var failures = new List<WorkspaceDiagnostic>();
        using var subscription = workspace.RegisterWorkspaceFailedHandler(failure => failures.Add(failure.Diagnostic));
        var solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken).ConfigureAwait(false);
        var reported = failures.Where(failure => failure.Kind == WorkspaceDiagnosticKind.Failure && !IsNuGetAudit(failure.Message))
            .Select(failure => failure.Message)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        foreach (var failure in reported)
        {
            await Console.Error.WriteLineAsync($"navarone: {failure}").ConfigureAwait(false);
        }

        return (solution, reported.Count > 0);
    }

    /// <summary>
    /// Whether the failure is a NuGet audit warning, the one failure that does not go to stderr. MSBuild passes on the
    /// warning's text without its code, so the text is matched: NU1900, NU1901 to NU1904, and NU1905.
    /// </summary>
    public static bool IsNuGetAudit(string message) => NuGetAudit().IsMatch(message);

    [GeneratedRegex(@"(?:^|with message: )(?:(?:[^:\n]+ : )?warning NU190[0-5]: [^\n]*|Package '[^']+' \S+ has a known (?:low|moderate|high|critical) severity vulnerability, \S+|Error occurred while getting package vulnerability data: [^\n]*|Audit source '[^']*' did not provide any vulnerability data\.?)\s*$")]
    private static partial Regex NuGetAudit();
}
