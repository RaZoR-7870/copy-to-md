using System.Runtime.InteropServices;
using System.Windows.Forms;
using CopyToMd.Conversion;
using CopyToMd.Interop;
using CopyToMd.Target;

namespace CopyToMd.Clipboard;

public interface IClipboardService
{
    ClipboardSnapshot Capture(int maxCharacters);
    uint Write(ConversionResult result, uint expectedSequence, nint expectedWindow);
    void EnsureCurrent(uint expectedSequence, nint expectedWindow);
}

public sealed class ClipboardService : IClipboardService
{
    private const string OwnerFormat = "CopyToMd.SmartPaste.Owner";

    public ClipboardSnapshot Capture(int maxCharacters)
    {
        var sequence = NativeMethods.GetClipboardSequenceNumber();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                var data = System.Windows.Forms.Clipboard.GetDataObject() ?? throw new InvalidOperationException("剪贴板当前没有可读内容。");
                var html = ReadString(data, DataFormats.Html);
                var rtf = ReadString(data, DataFormats.Rtf);
                var text = ReadString(data, DataFormats.UnicodeText) ?? ReadString(data, DataFormats.Text);
                var totalLength = (html?.Length ?? 0) + (rtf?.Length ?? 0) + (text?.Length ?? 0);
                if (totalLength > maxCharacters * 3)
                {
                    throw new InvalidOperationException("剪贴板内容过大，已停止转换。");
                }

                if (html is null && rtf is null && text is null)
                {
                    throw new InvalidOperationException("剪贴板中没有可转换的文本。请先复制 PPT 文本，而不是仅复制形状或图片。");
                }

                if (NativeMethods.GetClipboardSequenceNumber() != sequence)
                {
                    throw new ClipboardChangedException("读取期间剪贴板已变化，请重新执行智能粘贴。");
                }

                return new ClipboardSnapshot(sequence, html, rtf, text);
            }
            catch (ExternalException) when (attempt < 4)
            {
                Thread.Sleep(50);
            }
            catch (ExternalException exception)
            {
                throw new InvalidOperationException("剪贴板正被其他程序占用，请稍后重试。", exception);
            }
        }

        throw new InvalidOperationException("剪贴板正被其他程序占用，请稍后重试。");
    }

    public uint Write(ConversionResult result, uint expectedSequence, nint expectedWindow)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            EnsureCurrent(expectedSequence, expectedWindow);
            try
            {
                var owner = Guid.NewGuid().ToString("N");
                var data = new DataObject();
                data.SetData(OwnerFormat, owner);
                data.SetData(DataFormats.UnicodeText, result.Text);
                if (result.Mode == OutputMode.Word && result.Rtf is not null)
                {
                    data.SetData(DataFormats.Rtf, result.Rtf);
                }

                System.Windows.Forms.Clipboard.SetDataObject(data, true);
                var writtenSequence = NativeMethods.GetClipboardSequenceNumber();
                string? actualOwner;
                try
                {
                    actualOwner = System.Windows.Forms.Clipboard.GetDataObject()?.GetData(OwnerFormat) as string;
                }
                catch (ExternalException exception)
                {
                    throw new ClipboardChangedException("无法确认剪贴板所有权，已取消模拟粘贴。", exception);
                }

                if (actualOwner != owner)
                {
                    throw new ClipboardChangedException("剪贴板已被其他程序更新，已取消模拟粘贴。");
                }

                return writtenSequence;
            }
            catch (ExternalException) when (attempt < 4)
            {
                if (NativeMethods.GetClipboardSequenceNumber() != expectedSequence)
                {
                    throw new ClipboardChangedException("写入前剪贴板已变化，未覆盖新内容。");
                }

                Thread.Sleep(50);
            }
            catch (ExternalException exception)
            {
                throw new InvalidOperationException("无法写入剪贴板，请关闭占用剪贴板的程序后重试。", exception);
            }
        }

        throw new InvalidOperationException("无法写入剪贴板，请关闭占用剪贴板的程序后重试。");
    }

    public void EnsureCurrent(uint expectedSequence, nint expectedWindow)
    {
        if (NativeMethods.GetClipboardSequenceNumber() != expectedSequence)
        {
            throw new ClipboardChangedException("剪贴板已变化，已中止以避免覆盖新内容。");
        }

        if (NativeMethods.GetForegroundWindow() != expectedWindow)
        {
            throw new InvalidOperationException("前台窗口已切换，已取消本次智能粘贴。");
        }
    }

    private static string? ReadString(IDataObject data, string format) => data.GetData(format) as string;
}
