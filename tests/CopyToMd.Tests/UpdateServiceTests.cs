using System.IO.Compression;
using CopyToMd.Update;
using Xunit;

namespace CopyToMd.Tests;

public class UpdateServiceTests
{
    [Theory]
    [InlineData("v0.3.0", "0.2.0", true)]
    [InlineData("0.3.0", "0.2.0", true)]
    [InlineData("V1.0.0", "0.9.9", true)]
    [InlineData("v0.2.0", "0.2.0", false)]
    [InlineData("v0.1.0", "0.2.0", false)]
    [InlineData("v1.0.0-beta", "0.9.0", true)]
    [InlineData("", "0.1.0", false)]
    [InlineData("garbage", "0.1.0", false)]
    public void IsNewerComparesTags(string tag, string current, bool expected)
    {
        Assert.Equal(expected, UpdateService.IsNewer(tag, Version.Parse(current)));
    }

    [Fact]
    public void StageUpdateExtractsExeAndWritesUpdaterScript()
    {
        var root = Path.Combine(Path.GetTempPath(), "CopyToMd-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var payload = Path.Combine(root, "payload");
            Directory.CreateDirectory(payload);
            File.WriteAllText(Path.Combine(payload, "CopyToMd.exe"), "dummy");
            File.WriteAllText(Path.Combine(payload, "appsettings.json"), "{}");
            var zipPath = Path.Combine(root, "update.zip");
            ZipFile.CreateFromDirectory(payload, zipPath);

            var target = Path.Combine(root, "install", "CopyToMd.exe");
            var scriptPath = UpdateService.StageUpdate(zipPath, target);
            try
            {
                Assert.True(File.Exists(scriptPath));
                var script = File.ReadAllText(scriptPath);
                Assert.Contains(target, script);
                Assert.Contains("Wait-Process", script);
                Assert.Contains("Copy-Item", script);
                Assert.Contains("Start-Process", script);
                Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(scriptPath)!, "CopyToMd.exe")));
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(scriptPath)!, true);
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}