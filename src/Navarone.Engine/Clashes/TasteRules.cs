using System.Globalization;
using System.Text.RegularExpressions;
using Navarone.Engine.Events;

namespace Navarone.Engine.Clashes;

/// <summary>
/// The rules of the user's taste, the numbered list under "## The rules" in NAVARONE.md: the global file, which
/// `install.sh` links into Claude Code's rules, or the repo's own at its root, which wins in that repo when it
/// numbers a rule. The validator's replace names the rule the code breaks by its number there. A rule runs from its
/// numbered line through the indented lines under it.
/// </summary>
public sealed partial class TasteRules
{
    private readonly List<TasteRule> rules;

    private TasteRules(List<TasteRule> rules) => this.rules = rules;

    /// <summary>
    /// The NAVARONE.md Claude Code loads: rules/NAVARONE.md under CLAUDE_CONFIG_DIR, or under ~/.claude when that is
    /// not set.
    /// </summary>
    public static string DefaultPath()
    {
        var config = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        if (string.IsNullOrEmpty(config))
        {
            config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        }

        return Path.Combine(config, "rules", "NAVARONE.md");
    }

    /// <summary>
    /// The rules a replace in the repo at the given root names: those of the NAVARONE.md at the root of the repo
    /// when it numbers a rule under its rules heading, which wins in that repo, and otherwise those of the NAVARONE.md
    /// at the given global path. A repo file that lists no rule contradicts none.
    /// </summary>
    public static TasteRules For(string repoRoot, string globalPath)
    {
        var own = Path.Combine(repoRoot, "NAVARONE.md");
        if (File.Exists(own))
        {
            var ownRules = Parse(Read(own));
            if (ownRules.Count > 0)
            {
                return new TasteRules(ownRules);
            }
        }

        return Load(globalPath);
    }

    /// <summary>
    /// The rules in the NAVARONE.md at the given path, with LF or CRLF line ends. A file that cannot be read, or one
    /// with no numbered rule under its rules heading, throws an InvalidDataException.
    /// </summary>
    public static TasteRules Load(string path)
    {
        var rules = Parse(Read(path));
        if (rules.Count == 0)
        {
            throw new InvalidDataException($"replace names the rule the code breaks, and {path} lists no numbered rule under \"## The rules\"");
        }

        return new TasteRules(rules);
    }

    // The text of the file with LF line ends. A file that cannot be read throws an InvalidDataException.
    private static string Read(string path)
    {
        try
        {
            return File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"replace names the rule the code breaks, and the rules of NAVARONE.md cannot be read at {path}: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// The rule with the given number, as the validator wrote it. A word that is no number of a rule throws an
    /// InvalidDataException that names the numbers there are.
    /// </summary>
    public TasteRule Of(string number)
    {
        var rule = rules.Find(rule => rule.Number.ToString(CultureInfo.InvariantCulture) == number);
        if (rule is null)
        {
            throw new InvalidDataException($"replace names the rule the code breaks by its number in NAVARONE.md, {rules[0].Number} to {rules[^1].Number}, not \"{number}\": navarone <id> replace <rule> \"<finding>\"");
        }

        return rule;
    }

    // The numbered rules from the rules heading to the next heading, each with its indented lines joined to it, in number
    // order whatever order subheadings put them in.
    private static List<TasteRule> Parse(string text)
    {
        var rules = new List<TasteRule>();
        var section = Section().Match(text);
        if (!section.Success)
        {
            return rules;
        }

        foreach (Match rule in Numbered().Matches(section.Groups[1].Value))
        {
            var number = int.Parse(rule.Groups[1].Value, CultureInfo.InvariantCulture);
            var words = Space().Replace(rule.Groups[2].Value, " ").Trim();
            rules.Add(new TasteRule(number, words));
        }

        rules.Sort((left, right) => left.Number.CompareTo(right.Number));

        return rules;
    }

    [GeneratedRegex(@"^## The rules$(.*?)(?=^## |\z)", RegexOptions.Singleline | RegexOptions.Multiline)]
    private static partial Regex Section();

    // A numbered line and the indented lines under it.
    [GeneratedRegex(@"^(\d+)\.[ \t]+(.*(?:\n[ \t]+\S.*)*)", RegexOptions.Multiline)]
    private static partial Regex Numbered();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Space();
}
