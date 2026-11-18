using CopyToMd.Configuration;
using CopyToMd.Target;
using Xunit;

namespace CopyToMd.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void LoadsStringEnumAndNormalizesNullRuleValues()
    {
        var path = Path.Combine(Path.GetTempPath(), $"copy-to-md-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """
                {
                  "hotKey": "Ctrl+Alt+Shift+V",
                  "userRules": [
                    null,
                    { "processName": "obsidian", "extensions": [null, " "], "mode": "Markdown" }
                  ]
                }
                """);
            var settings = AppSettings.Load(path);
            var rule = Assert.Single(settings.UserRules);
            Assert.Equal(OutputMode.Markdown, rule.Mode);
            Assert.Empty(rule.Extensions);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void NullUserRulesBecomesEmptyCollection()
    {
        var path = Path.Combine(Path.GetTempPath(), $"copy-to-md-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ \"userRules\": null }");
            Assert.Empty(AppSettings.Load(path).UserRules);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
