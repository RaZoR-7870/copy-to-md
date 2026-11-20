using CopyToMd.Configuration;
using CopyToMd.Conversion;
using Xunit;

namespace CopyToMd.Tests;

public class AppSettingsMigrationTests
{
    [Fact]
    public void OldConfigMigratesToCompactSpacingAndJoinedSoftBreaks()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{"HotKey":"Ctrl+Alt+V","Style":{"ParagraphSpacing":0,"SoftBreakMode":1}}""");
            var settings = AppSettings.Load(path);
            Assert.Equal(ParagraphSpacing.SingleNewline, settings.Style.ParagraphSpacing);
            Assert.Equal(SoftBreakMode.Join, settings.Style.SoftBreakMode);
            Assert.Equal(AppSettings.CurrentConfigVersion, settings.ConfigVersion);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CurrentConfigKeepsExplicitSpacingChoice()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{"ConfigVersion":2,"Style":{"ParagraphSpacing":0}}""");
            var settings = AppSettings.Load(path);
            Assert.Equal(ParagraphSpacing.BlankLine, settings.Style.ParagraphSpacing);
        }
        finally
        {
            File.Delete(path);
        }
    }
}