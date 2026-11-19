using CopyToMd.Update;

namespace CopyToMd.App;

public sealed class UpdateForm : Form
{
    private readonly string? proxy;
    private readonly string targetExePath;
    private readonly Label header = new() { AutoSize = true, Font = new Font(SystemFonts.MessageBoxFont!, FontStyle.Bold), Text = "正在检查更新..." };
    private readonly Label versionLabel = new() { AutoSize = true, Text = string.Empty };
    private readonly TextBox notesBox = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, WordWrap = true };
    private readonly ProgressBar progress = new() { Dock = DockStyle.Top, Height = 18, Visible = false };
    private readonly Label statusLabel = new() { AutoSize = true, Text = string.Empty };
    private readonly Button updateButton = new() { Text = "立即更新", Width = 110, Enabled = false };
    private readonly Button closeButton = new() { Text = "关闭", Width = 90 };
    private readonly CancellationTokenSource cancellation = new();
    private UpdateCheckResult? result;
    private bool updating;

    public UpdateForm(string? proxy, string targetExePath)
    {
        this.proxy = proxy;
        this.targetExePath = targetExePath;
        Text = "Copy to MD 更新";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(560, 460);
        Size = new Size(620, 520);
        ShowInTaskbar = false;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(12), WrapContents = false };
        top.Controls.Add(header);
        top.Controls.Add(versionLabel);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 52, Padding = new Padding(12) };
        updateButton.Click += async (_, _) => await RunUpdateAsync();
        closeButton.Click += (_, _) => Close();
        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(updateButton);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(12, 0, 12, 0), WrapContents = false };
        bottom.Controls.Add(progress);
        bottom.Controls.Add(statusLabel);

        Controls.Add(notesBox);
        Controls.Add(bottom);
        Controls.Add(buttons);
        Controls.Add(top);

        FormClosed += (_, _) => cancellation.Cancel();
        Shown += async (_, _) => await CheckAsync();
    }

    private async Task CheckAsync()
    {
        try
        {
            result = await UpdateService.CheckAsync(proxy, cancellation.Token);
            versionLabel.Text = $"当前版本 {UpdateService.CurrentVersion.ToString(3)}    最新版本 {result.LatestVersion.ToString(3)}";
            notesBox.Text = string.IsNullOrWhiteSpace(result.Notes) ? "（无更新说明）" : result.Notes;
            if (result.HasUpdate)
            {
                header.Text = "发现新版本";
                updateButton.Enabled = true;
                statusLabel.Text = $"下载大小约 {result.AssetSize / 1024.0 / 1024.0:F1} MB。更新会保留 appsettings.json 配置。";
            }
            else
            {
                header.Text = "已是最新版本";
                updateButton.Visible = false;
                closeButton.Text = "确定";
                statusLabel.Text = "无需更新。";
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException)
        {
            header.Text = "检查更新失败";
            versionLabel.Text = string.Empty;
            notesBox.Text = exception.Message;
            statusLabel.Text = "请检查网络，或在设置中填写可用的更新代理地址。";
            updateButton.Visible = false;
            closeButton.Text = "确定";
        }
    }

    private async Task RunUpdateAsync()
    {
        if (result is null || updating)
        {
            return;
        }

        updating = true;
        updateButton.Enabled = false;
        closeButton.Text = "取消";
        progress.Visible = true;
        progress.Style = ProgressBarStyle.Continuous;
        progress.Minimum = 0;
        progress.Maximum = result.AssetSize > 0 ? (int)Math.Min(result.AssetSize, int.MaxValue) : 100;
        var zipPath = Path.Combine(Path.GetTempPath(), "CopyToMd-download-" + Guid.NewGuid().ToString("N") + ".zip");
        var progressReporter = new Progress<long>(bytes =>
        {
            if (result.AssetSize > 0)
            {
                progress.Value = (int)Math.Min(bytes, progress.Maximum);
                statusLabel.Text = $"正在下载 {bytes / 1024.0 / 1024.0:F1} / {result.AssetSize / 1024.0 / 1024.0:F1} MB";
            }
            else
            {
                statusLabel.Text = $"正在下载 {bytes / 1024.0 / 1024.0:F1} MB";
            }
        });

        try
        {
            await UpdateService.DownloadAsync(result.DownloadUrl, zipPath, proxy, progressReporter, cancellation.Token);
            statusLabel.Text = "下载完成，正在准备替换...";
            var scriptPath = UpdateService.StageUpdate(zipPath, targetExePath);
            try { File.Delete(zipPath); } catch (IOException) { }
            UpdateService.LaunchUpdater(scriptPath);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (OperationCanceledException)
        {
            statusLabel.Text = "更新已取消。";
            updateButton.Enabled = true;
            closeButton.Text = "关闭";
            progress.Visible = false;
            updating = false;
            try { File.Delete(zipPath); } catch (IOException) { }
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"更新失败：{exception.Message}", "Copy to MD", MessageBoxButtons.OK, MessageBoxIcon.Error);
            statusLabel.Text = "更新失败，可重试。";
            updateButton.Enabled = true;
            closeButton.Text = "关闭";
            progress.Visible = false;
            updating = false;
            try { File.Delete(zipPath); } catch (IOException) { }
        }
    }
}