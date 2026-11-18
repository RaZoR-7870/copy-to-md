using CopyToMd.Configuration;
using CopyToMd.Target;
using Xunit;

namespace CopyToMd.Tests;

public sealed class TargetResolverTests
{
    [Theory]
    [InlineData("obsidian", "任意标题", OutputMode.Markdown)]
    [InlineData("typora", "note", OutputMode.Markdown)]
    [InlineData("code", "note.md - Visual Studio Code", OutputMode.Markdown)]
    [InlineData("code", "note.txt - Visual Studio Code", OutputMode.PlainText)]
    [InlineData("code", "main.cs - Visual Studio Code", OutputMode.PlainText)]
    [InlineData("code", "Untitled-1 - Visual Studio Code", OutputMode.PlainText)]
    [InlineData("winword", "Document1 - Word", OutputMode.Word)]
    [InlineData("notepad", "note.txt - Notepad", OutputMode.PlainText)]
    [InlineData("chrome", "Online editor", OutputMode.PlainText)]
    public void ResolvesDefaultTargets(string process, string title, OutputMode expected)
    {
        var resolver = new TargetResolver();
        var result = resolver.Resolve(new TargetInfo(1, 1, process, title));
        Assert.Equal(expected, result.Mode);
    }

    [Fact]
    public void UserRulesAndManualOverrideTakePriority()
    {
        var resolver = new TargetResolver([
            new TargetRule { Name = "自定义", ProcessName = "obsidian", Mode = OutputMode.PlainText }
        ]);
        var target = new TargetInfo(1, 1, "obsidian", "note");
        Assert.Equal(OutputMode.PlainText, resolver.Resolve(target).Mode);
        Assert.Equal(OutputMode.Markdown, resolver.Resolve(target, OutputMode.Markdown).Mode);
    }

    [Fact]
    public void InvalidUserRegexSafelySkipsRule()
    {
        var resolver = new TargetResolver([
            new TargetRule { ProcessName = "code", TitlePattern = "[", Mode = OutputMode.Markdown }
        ]);
        Assert.Equal(OutputMode.PlainText, resolver.Resolve(new TargetInfo(1, 1, "code", "note.txt")).Mode);
    }

    [Fact]
    public void ParsesDefaultHotKey()
    {
        var gesture = HotKeyGesture.Parse("Ctrl+Alt+Shift+V");
        Assert.Equal(Keys.V, gesture.Key);
        Assert.Throws<FormatException>(() => HotKeyGesture.Parse("V"));
    }
}
