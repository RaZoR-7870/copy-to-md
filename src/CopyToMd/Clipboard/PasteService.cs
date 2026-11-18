using CopyToMd.Interop;

namespace CopyToMd.Clipboard;

public interface IPasteService
{
    bool WaitForModifierKeys();
    bool SendControlV();
}

public sealed class PasteService : IPasteService
{
    public bool WaitForModifierKeys() => NativeMethods.AreModifierKeysReleased();
    public bool SendControlV() => NativeMethods.SendControlV();
}
