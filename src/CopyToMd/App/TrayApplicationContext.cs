using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using CopyToMd.Clipboard;
using CopyToMd.Configuration;
using CopyToMd.Conversion;
using CopyToMd.Interop;
using CopyToMd.Target;

namespace CopyToMd.App;

public sealed class TrayApplicationContext : ApplicationContext
{
    private const int HotKeyId = 1;
    private AppSettings settings;
    private readonly string settingsPath;
    private readonly SmartPasteCoordinator coordinator;
    private readonly NotifyIcon notifyIcon;
    private readonly HotKeyMessageWindow messageWindow;
    private readonly ToolStripMenuItem statusItem;
    private readonly List<ToolStripMenuItem> modeItems = [];

    public TrayApplicationContext(AppSettings settings, string settingsPath)
    {
        this.settings = settings;
        this.settingsPath = settingsPath;
        var targetResolver = new TargetResolver(settings.UserRules);
        coordinator = new SmartPasteCoordinator(
            new ClipboardService(),
            new ForegroundWindowService(),
            new PasteService(),
            new ConversionService(),
            targetResolver)
        {
            MaxClipboardCharacters = settings.MaxClipboardCharacters
        };

        statusItem = new ToolStripMenuItem("最近状态：等待智能粘贴") { Enabled = false };
        var menu = new ContextMenuStrip();
        menu.Items.Add(statusItem);
        menu.Items.Add(new ToolStripSeparator());
        AddModeItem(menu, "自动识别目标", OutputMode.Auto);
        AddModeItem(menu, "强制 Markdown", OutputMode.Markdown);
        AddModeItem(menu, "强制 Word", OutputMode.Word);
        AddModeItem(menu, "强制纯文本", OutputMode.PlainText);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("检查更新...", null, (_, _) => CheckForUpdates());
        menu.Items.Add("设置...", null, (_, _) => OpenSettingsDialog());
        menu.Items.Add("打开配置文件", null, (_, _) => OpenSettings());
        menu.Items.Add("退出", null, (_, _) => Shutdown());

        notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Text = $"Copy to MD - {settings.HotKey}",
            Visible = true,
            ContextMenuStrip = menu,
            BalloonTipTitle = "Copy to MD"
        };
        coordinator.StatusChanged += OnStatus;

        messageWindow = new HotKeyMessageWindow(() => coordinator.Execute());
        _ = messageWindow.Handle;
        RegisterHotKeyOrThrow(settings.HotKey);
    }

    private void RegisterHotKeyOrThrow(string hotKey)
    {
        var gesture = HotKeyGesture.Parse(hotKey);
        if (!NativeMethods.RegisterHotKey(messageWindow.Handle, HotKeyId, gesture.Modifiers, (uint)gesture.Key))
        {
            throw new InvalidOperationException($"无法注册快捷键 {hotKey}。它可能已被其他程序占用，请在设置中修改。");
        }
    }

    private void CheckForUpdates()
    {
        var exePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "CopyToMd.exe");
        using var form = new UpdateForm(settings.UpdateProxy, exePath);
        if (form.ShowDialog() == DialogResult.OK)
        {
            Shutdown();
        }
    }

    private void OpenSettingsDialog()
    {
        using var form = new SettingsForm(settings);
        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        var updated = form.Result;
        var previousHotKey = settings.HotKey;
        NativeMethods.UnregisterHotKey(messageWindow.Handle, HotKeyId);
        try
        {
            RegisterHotKeyOrThrow(updated.HotKey);
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException)
        {
            try
            {
                RegisterHotKeyOrThrow(previousHotKey);
            }
            catch (Exception rollbackException) when (rollbackException is FormatException or InvalidOperationException)
            {
                MessageBox.Show($"新快捷键注册失败，且原快捷键也无法恢复：{rollbackException.Message}", "Copy to MD", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show(exception.Message, "快捷键未更新", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        settings = updated;
        coordinator.ApplySettings(settings);
        notifyIcon.Text = $"Copy to MD - {settings.HotKey}";
        statusItem.Text = "最近状态：设置已更新";

        try
        {
            settings.Save(settingsPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"设置已生效，但写入配置文件失败：{exception.Message}", "Copy to MD", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void AddModeItem(ContextMenuStrip menu, string text, OutputMode mode)
    {
        var item = new ToolStripMenuItem(text) { CheckOnClick = false, Checked = mode == OutputMode.Auto, Tag = mode };
        item.Click += (_, _) =>
        {
            coordinator.OverrideMode = mode;
            foreach (var modeItem in modeItems)
            {
                modeItem.Checked = Equals(modeItem.Tag, mode);
            }
        };
        modeItems.Add(item);
        menu.Items.Add(item);
    }

    private void OnStatus(SmartPasteStatus status)
    {
        statusItem.Text = "最近状态：" + status.Message;
        if (!status.Success || settings.ShowSuccessNotifications)
        {
            notifyIcon.BalloonTipIcon = status.Success ? ToolTipIcon.Info : ToolTipIcon.Warning;
            notifyIcon.BalloonTipText = status.Message.Length > 250 ? status.Message[..250] : status.Message;
            notifyIcon.ShowBalloonTip(4000);
        }
    }

    private void OpenSettings()
    {
        AppSettings.SaveDefault(settingsPath);
        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{settingsPath}\"") { UseShellExecute = true });
    }

    private void Shutdown()
    {
        NativeMethods.UnregisterHotKey(messageWindow.Handle, HotKeyId);
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        messageWindow.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            notifyIcon.Dispose();
            messageWindow.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class HotKeyMessageWindow : Form
    {
        private readonly Action hotKeyPressed;

        public HotKeyMessageWindow(Action hotKeyPressed)
        {
            this.hotKeyPressed = hotKeyPressed;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
        }

        protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == NativeMethods.WM_HOTKEY && message.WParam.ToInt32() == HotKeyId)
            {
                hotKeyPressed();
                return;
            }

            base.WndProc(ref message);
        }
    }
}
