using System.Text.Json;
using CopyToMd.Conversion;
using CopyToMd.Target;

namespace CopyToMd.Configuration;

public sealed class AppSettings
{
    public string HotKey { get; set; } = "Ctrl+Alt+Shift+V";
    public bool ShowSuccessNotifications { get; set; }
    public int MaxClipboardCharacters { get; set; } = 500_000;
    public StyleSettings Style { get; set; } = new();
    public List<TargetRule> UserRules { get; set; } = [];

    public static AppSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), options) ?? new AppSettings();
        settings.HotKey = string.IsNullOrWhiteSpace(settings.HotKey) ? "Ctrl+Alt+Shift+V" : settings.HotKey;
        settings.MaxClipboardCharacters = Math.Clamp(settings.MaxClipboardCharacters, 1_000, 5_000_000);
        settings.Style ??= new StyleSettings();
        settings.Style.Normalize();
        settings.UserRules = settings.UserRules?.Where(rule => rule is not null).ToList() ?? [];
        foreach (var rule in settings.UserRules)
        {
            rule.Name ??= string.Empty;
            rule.ProcessName ??= string.Empty;
            rule.Extensions = rule.Extensions?.Where(extension => !string.IsNullOrWhiteSpace(extension)).ToArray() ?? [];
        }

        settings.UserRules = settings.UserRules
            .Where(rule => Enum.IsDefined(rule.Mode) && rule.Mode != OutputMode.Auto &&
                           (!string.IsNullOrWhiteSpace(rule.ProcessName) || !string.IsNullOrWhiteSpace(rule.TitlePattern) || rule.Extensions.Length > 0))
            .ToList();
        return settings;
    }

    public static void SaveDefault(string path)
    {
        if (File.Exists(path))
        {
            return;
        }

        new AppSettings().Save(path);
    }

    public void Save(string path)
    {
        Style ??= new StyleSettings();
        Style.Normalize();
        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
    }
}
