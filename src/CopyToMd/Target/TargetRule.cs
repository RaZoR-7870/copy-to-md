namespace CopyToMd.Target;

public sealed class TargetRule
{
    public string Name { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string? TitlePattern { get; set; }
    public string[] Extensions { get; set; } = [];
    public OutputMode Mode { get; set; } = OutputMode.PlainText;
}

public sealed record TargetInfo(nint WindowHandle, int ProcessId, string ProcessName, string WindowTitle);
public sealed record TargetResolution(OutputMode Mode, string RuleName);
