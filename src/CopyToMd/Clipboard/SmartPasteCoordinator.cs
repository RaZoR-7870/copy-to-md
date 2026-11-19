using CopyToMd.Configuration;
using CopyToMd.Conversion;
using CopyToMd.Target;

namespace CopyToMd.Clipboard;

public sealed record SmartPasteStatus(bool Success, string Message);

public sealed class SmartPasteCoordinator
{
    private readonly IClipboardService clipboardService;
    private readonly IForegroundWindowService foregroundWindowService;
    private readonly IPasteService pasteService;
    private ConversionService conversionService;
    private TargetResolver targetResolver;
    private int running;

    public SmartPasteCoordinator(
        IClipboardService clipboardService,
        IForegroundWindowService foregroundWindowService,
        IPasteService pasteService,
        ConversionService conversionService,
        TargetResolver targetResolver)
    {
        this.clipboardService = clipboardService;
        this.foregroundWindowService = foregroundWindowService;
        this.pasteService = pasteService;
        this.conversionService = conversionService;
        this.targetResolver = targetResolver;
    }

    public int MaxClipboardCharacters { get; set; } = 500_000;
    public OutputMode OverrideMode { get; set; } = OutputMode.Auto;
    public event Action<SmartPasteStatus>? StatusChanged;

    public void ApplySettings(AppSettings settings)
    {
        MaxClipboardCharacters = settings.MaxClipboardCharacters;
        conversionService = new ConversionService(settings.Style);
        targetResolver = new TargetResolver(settings.UserRules);
    }

    public void Execute()
    {
        if (Interlocked.Exchange(ref running, 1) != 0)
        {
            Report(new SmartPasteStatus(false, "上一次智能粘贴仍在处理，请稍候。"));
            return;
        }

        try
        {
            var target = foregroundWindowService.Capture();
            var resolution = targetResolver.Resolve(target, OverrideMode);
            var snapshot = clipboardService.Capture(MaxClipboardCharacters);
            clipboardService.EnsureCurrent(snapshot.SequenceNumber, target.WindowHandle);
            var result = conversionService.Convert(snapshot.Html, snapshot.Rtf, snapshot.UnicodeText, resolution.Mode);
            var newSequence = clipboardService.Write(result, snapshot.SequenceNumber, target.WindowHandle);

            if (!pasteService.WaitForModifierKeys())
            {
                throw new InvalidOperationException("快捷键仍处于按下状态，已转换剪贴板但未模拟粘贴。请松开按键后手动按 Ctrl+V。");
            }

            clipboardService.EnsureCurrent(newSequence, target.WindowHandle);
            if (!pasteService.SendControlV())
            {
                throw new InvalidOperationException("已转换剪贴板，但无法完整发送粘贴按键。请检查 Ctrl/V 键状态，必要时手动按 Ctrl+V；目标应用也可能以更高权限运行。");
            }

            var modeName = resolution.Mode switch
            {
                OutputMode.Markdown => "Markdown",
                OutputMode.Word => "Word",
                _ => "纯文本"
            };
            var warning = result.Document.Warnings.Count > 0 && result.Document.UsedFallback ? "（已使用纯文本回退）" : string.Empty;
            var targetName = string.IsNullOrWhiteSpace(target.ProcessName) ? "当前应用" : target.ProcessName;
            Report(new SmartPasteStatus(true, $"已向 {targetName} 发送 {modeName} 粘贴{warning}。"));
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            Report(new SmartPasteStatus(false, exception.Message));
        }
        finally
        {
            Interlocked.Exchange(ref running, 0);
        }
    }

    private void Report(SmartPasteStatus status) => StatusChanged?.Invoke(status);
}
