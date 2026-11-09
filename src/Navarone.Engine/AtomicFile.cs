namespace Navarone.Engine;

/// <summary>
/// Writes a file whole or not at all: the text goes to a file of its own beside the target and is moved over it, so a
/// crash mid-write never leaves a truncated target and a reader never sees half of it.
/// </summary>
internal static class AtomicFile
{
    /// <summary>
    /// Writes the text to the path. Fails with the IOException or UnauthorizedAccessException of the write, and leaves
    /// no temporary file behind.
    /// </summary>
    public static void Write(string path, string text)
    {
        var directory = Path.GetDirectoryName(path) ?? ".";
        var written = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(written, text);
            File.Move(written, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            File.Delete(written);
            throw;
        }
    }

    /// <summary>
    /// Writes the text as Write does, for a file Navarone owns in the analysed repo: a write that fails throws an
    /// InvalidDataException with one line that names the file and the cause, which a command reports as a usage error.
    /// </summary>
    public static void WriteOrRefuse(string path, string text)
    {
        try
        {
            Write(path, text);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"{path} was not written: {exception.Message}", exception);
        }
    }
}
