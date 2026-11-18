using System.Diagnostics;
using CopyToMd.Interop;

namespace CopyToMd.Target;

public interface IForegroundWindowService
{
    TargetInfo Capture();
    bool IsForeground(nint handle);
}

public sealed class ForegroundWindowService : IForegroundWindowService
{
    public TargetInfo Capture()
    {
        var handle = NativeMethods.GetForegroundWindow();
        if (handle == nint.Zero)
        {
            throw new InvalidOperationException("无法识别当前前台窗口。");
        }

        NativeMethods.GetWindowThreadProcessId(handle, out var processId);
        var processName = string.Empty;
        try
        {
            processName = Process.GetProcessById((int)processId).ProcessName;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Unknown processes deliberately fall back to plain text.
        }

        return new TargetInfo(handle, (int)processId, processName, NativeMethods.GetWindowTitle(handle));
    }

    public bool IsForeground(nint handle) => handle != nint.Zero && NativeMethods.GetForegroundWindow() == handle;
}
