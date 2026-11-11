using System.Text;

namespace Venture.OS.Shell;

/// <summary>
/// A <see cref="TextWriter"/> that passes complete lines to an <see cref="IShellOutput"/>.
/// </summary>
/// <remarks>
/// Lua writes text in pieces, such as <c>io.write("a")</c> then <c>print("b")</c>. This collects the pieces and
/// writes each line once it ends with <c>\n</c>. A <c>\r</c> before the <c>\n</c> is dropped. Text left without a
/// line break is written as a last line when the writer is disposed, not on <see cref="Flush"/>, so a flush in the
/// middle of a line doesn't split it.
/// </remarks>
public sealed class ShellOutputWriter(IShellOutput output) : TextWriter
{
    private readonly IShellOutput output = output ?? throw new ArgumentNullException(nameof(output));
    private readonly StringBuilder line = new();

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        if (value == '\n')
            EndLine();
        else
            line.Append(value);
    }

    public override void Write(string? value)
    {
        if (value is null)
            return;

        foreach (var character in value)
            Write(character);
    }

    /// <summary>
    /// Writes any text left without a line break as a line of its own, so what's written next starts on a new line.
    /// </summary>
    public void FinishLine()
    {
        if (line.Length > 0)
            EndLine();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            FinishLine();

        base.Dispose(disposing);
    }

    private void EndLine()
    {
        if (line.Length > 0 && line[^1] == '\r')
            line.Length--;

        output.WriteLine(line.ToString());
        line.Clear();
    }
}
