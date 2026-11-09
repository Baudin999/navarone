using System.Text;

namespace Navarone.Cli;

/// <summary>A writer that passes everything to another writer and counts the characters it passed.</summary>
internal sealed class CountingWriter(TextWriter inner) : TextWriter
{
    /// <summary>The characters written so far.</summary>
    public int Characters { get; private set; }

    public override Encoding Encoding => inner.Encoding;

    public override void Write(char value)
    {
        Characters++;
        inner.Write(value);
    }

    public override void Write(string? value)
    {
        Characters += value?.Length ?? 0;
        inner.Write(value);
    }

    public override void Write(char[] buffer, int index, int count)
    {
        Characters += count;
        inner.Write(buffer, index, count);
    }

    public override void Write(ReadOnlySpan<char> buffer)
    {
        Characters += buffer.Length;
        inner.Write(buffer);
    }
}
