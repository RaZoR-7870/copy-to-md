namespace CopyToMd.Clipboard;

public sealed record ClipboardSnapshot(
    uint SequenceNumber,
    string? Html,
    string? Rtf,
    string? UnicodeText,
    bool IsOwnOutput = false);

public sealed class ClipboardChangedException : InvalidOperationException
{
    public ClipboardChangedException(string message) : base(message)
    {
    }

    public ClipboardChangedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
