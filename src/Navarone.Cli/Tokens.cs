namespace Navarone.Cli;

/// <summary>The cost a command logs, in tokens: one for every four characters it printed, rounded down.</summary>
internal static class Tokens
{
    private const int CharactersPerToken = 4;

    /// <summary>The tokens the given count of printed characters costs.</summary>
    public static int Of(int characters)
    {
        return characters / CharactersPerToken;
    }
}
