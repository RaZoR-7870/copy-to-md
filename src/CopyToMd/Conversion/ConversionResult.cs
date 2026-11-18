using CopyToMd.Target;

namespace CopyToMd.Conversion;

public sealed record ConversionResult(
    OutputMode Mode,
    RichDocument Document,
    string Text,
    string? Rtf = null);
