using System.Windows.Forms;
using CopyToMd.App;
using CopyToMd.Configuration;

namespace CopyToMd;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var singleInstance = new Mutex(true, @"Local\CopyToMd.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("Copy to MD 已在后台运行。", "Copy to MD", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        try
        {
            AppSettings.SaveDefault(settingsPath);
            var settings = AppSettings.Load(settingsPath);
            Application.Run(new TrayApplicationContext(settings, settingsPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or FormatException or System.Text.Json.JsonException)
        {
            MessageBox.Show(exception.Message, "Copy to MD 启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
