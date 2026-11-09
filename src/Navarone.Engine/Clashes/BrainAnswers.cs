using System.Text.Json;
using Navarone.Engine.Events;

namespace Navarone.Engine.Clashes;

/// <summary>
/// The user's answers to Navarone's questions, where brain's window stores them: one file per answer,
/// &lt;question id&gt;.json, `{"id", "choice", "words", "answeredAt"}`, in brain's cache/answers/navarone. Navarone only
/// reads them and never writes in brain's folder. Without brain the folder does not exist, and no question has an
/// answer.
/// </summary>
public sealed class BrainAnswers(string folder)
{
    // brain writes its answers in camel case.
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// The answers in brain's home: BRAIN_HOME, or brain under the user's data home, ~/.local/share unless
    /// XDG_DATA_HOME says otherwise, as brain reads it.
    /// </summary>
    public static BrainAnswers Default()
    {
        var home = Environment.GetEnvironmentVariable("BRAIN_HOME");
        if (string.IsNullOrEmpty(home))
        {
            home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify), "brain");
        }

        return new BrainAnswers(Path.Combine(home, "cache", "answers", "navarone"));
    }

    /// <summary>
    /// The user's answer to the question with the given id, or null while there is none. An answer whose choice is
    /// neither keep nor replace, or a file Navarone cannot read, counts as no answer: the question keeps waiting.
    /// A missing file is no answer either. Blank words are no words.
    /// </summary>
    internal (Position User, string? Words)? Of(string questionId)
    {
        StoredAnswer? answer;
        try
        {
            answer = JsonSerializer.Deserialize<StoredAnswer>(File.ReadAllText(Path.Combine(folder, questionId + ".json")), Options);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }

        if (answer is null)
        {
            return null;
        }

        var words = answer.Words;
        if (string.IsNullOrWhiteSpace(words))
        {
            words = null;
        }

        if (answer.Choice == "keep")
        {
            return (Position.Keep, words);
        }

        if (answer.Choice == "replace")
        {
            return (Position.Replace, words);
        }

        return null;
    }

    // An answer as brain stores it, of which Navarone reads the choice and the words.
    private sealed record StoredAnswer(string? Choice, string? Words);
}
