using System.Runtime.InteropServices;
using System.Text;

namespace CopyToMd.Interop;

internal static partial class NativeMethods
{
    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12;
    public const int VK_SHIFT = 0x10;
    public const int VK_LCONTROL = 0xA2;
    public const int VK_RCONTROL = 0xA3;
    public const int VK_LMENU = 0xA4;
    public const int VK_RMENU = 0xA5;
    public const int VK_LSHIFT = 0xA0;
    public const int VK_RSHIFT = 0xA1;
    public const int VK_LWIN = 0x5B;
    public const int VK_RWIN = 0x5C;
    public const int VK_V = 0x56;

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(nint hWnd, int id);

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(nint hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    public static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool OpenClipboard(nint hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint GetClipboardData(uint uFormat);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint RegisterClipboardFormat(string lpszFormat);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint GlobalLock(nint hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalUnlock(nint hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nuint GlobalSize(nint hMem);

    public static byte[]? ReadClipboardFormatBytes(string format)
    {
        var formatId = RegisterClipboardFormat(format);
        if (formatId == 0 || !OpenClipboard(0))
        {
            return null;
        }

        try
        {
            var handle = GetClipboardData(formatId);
            if (handle == 0)
            {
                return null;
            }

            var size = GlobalSize(handle);
            if (size == 0)
            {
                return null;
            }

            var pointer = GlobalLock(handle);
            if (pointer == 0)
            {
                return null;
            }

            try
            {
                var bytes = new byte[(int)size];
                Marshal.Copy(pointer, bytes, 0, bytes.Length);
                return bytes;
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, INPUT[] inputs, int size);

    public static string GetWindowTitle(nint handle)
    {
        var text = new StringBuilder(1024);
        var length = GetWindowText(handle, text, text.Capacity);
        return length > 0 ? text.ToString(0, length) : string.Empty;
    }

    public static bool AreModifierKeysReleased(int timeoutMilliseconds = 1000)
    {
        var keys = new[] { VK_CONTROL, VK_MENU, VK_SHIFT, VK_LCONTROL, VK_RCONTROL, VK_LMENU, VK_RMENU, VK_LSHIFT, VK_RSHIFT, VK_LWIN, VK_RWIN };
        var deadline = Environment.TickCount64 + timeoutMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            if (keys.All(key => (GetAsyncKeyState(key) & 0x8000) == 0))
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return false;
    }

    public static bool SendControlV()
    {
        var inputs = new[]
        {
            KeyboardInput(VK_CONTROL, false),
            KeyboardInput(VK_V, false),
            KeyboardInput(VK_V, true),
            KeyboardInput(VK_CONTROL, true)
        };
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent == inputs.Length)
        {
            return true;
        }

        if (sent > 0)
        {
            // Do not replay Ctrl+V. Retry only the remaining key-up events.
            var cleanup = new[] { KeyboardInput(VK_V, true), KeyboardInput(VK_CONTROL, true) };
            var offset = 0;
            for (var attempt = 0; attempt < 3 && offset < cleanup.Length; attempt++)
            {
                var remaining = cleanup[offset..];
                var released = SendInput((uint)remaining.Length, remaining, Marshal.SizeOf<INPUT>());
                if (released > 0)
                {
                    offset += (int)released;
                }
            }
        }

        return false;
    }

    private static INPUT KeyboardInput(int key, bool keyUp) => new()
    {
        Type = INPUT_KEYBOARD,
        Data = new InputUnion
        {
            Keyboard = new KEYBDINPUT
            {
                VirtualKey = (ushort)key,
                Flags = keyUp ? KEYEVENTF_KEYUP : 0
            }
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT Keyboard;
        [FieldOffset(0)] public MOUSEINPUT Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }
}
