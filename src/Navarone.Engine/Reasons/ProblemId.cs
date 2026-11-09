using System.Globalization;

namespace Navarone.Engine.Reasons;

/// <summary>
/// The id Navarone grants to a new problem, such as 0001234. The agent passes it to `navarone &lt;id&gt; "&lt;reason&gt;"`
/// and puts it in a comment at the code, `// navarone: 0001234`. It belongs to one problem: its check and its
/// resolved symbol. A change to the code voids the problem's reason, but the problem keeps its id. An id is seven
/// digits; any other string is no id, and the constructor refuses it.
/// </summary>
public sealed record ProblemId
{
    private const int Digits = 7;

    /// <summary>Makes the id of the given text. Text that is not seven ASCII digits throws an InvalidDataException.</summary>
    public ProblemId(string value)
    {
        if (!IsShape(value))
        {
            throw new InvalidDataException($"\"{value}\" is not a problem id; an id is {Digits} digits, such as {Numbered(1).Value}");
        }

        Value = value;
    }

    /// <summary>The id as text: seven digits.</summary>
    public string Value { get; }

    /// <summary>The id with the given counter value, zero padded to seven digits.</summary>
    public static ProblemId Numbered(int number) => new(number.ToString("D7", CultureInfo.InvariantCulture));

    /// <summary>True when the text has an id's shape: seven ASCII digits.</summary>
    public static bool IsShape(string? text)
    {
        if (text is null || text.Length != Digits)
        {
            return false;
        }

        return text.All(char.IsAsciiDigit);
    }

    /// <summary>The counter value of this id.</summary>
    public int Number => int.Parse(Value, NumberStyles.None, CultureInfo.InvariantCulture);
}
