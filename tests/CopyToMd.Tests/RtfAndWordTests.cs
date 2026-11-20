using CopyToMd.Conversion;
using CopyToMd.Target;
using Xunit;

namespace CopyToMd.Tests;

public sealed class RtfAndWordTests
{
    [Fact]
    public void RtfUsesSymbolFontEvidenceAndFormatting() => StaTestRunner.Run(() =>
    {
        var rtf = string.Concat(
            @"{\rtf1\ansi{\fonttbl{\f0 Calibri;}{\f1 Wingdings;}}",
            @"\pard\f0\fs48\b Title\b0\par",
            @"\pard\f0\fs22 Normal\par",
            @"\pard\f1\fs22 l\tab\f0 First\par",
            @"\pard\f1\fs22 m\tab\f0\b Second\b0\par",
            @"}");
        var importer = new RtfImporter();
        Assert.True(importer.TryImport(rtf, "Title\r\nNormal\r\nl\tFirst\r\nm\tSecond", out var document));
        var markdown = new MarkdownRenderer().Render(document!);
        Assert.Equal("# Title\nNormal\n- First\n- **Second**", markdown);
    });

    [Fact]
    public void RejectsRtfWhenPlainTextHasAdditionalContent() => StaTestRunner.Run(() =>
    {
        var importer = new RtfImporter();
        Assert.False(importer.TryImport(@"{\rtf1\ansi abc}", "abcdef", out _));
    });

    [Fact]
    public void ReadsOrderedRtfParagraphs() => StaTestRunner.Run(() =>
    {
        const string rtf = @"{\rtf1\ansi\pard 3. Third\par\pard 4. Fourth\par}";
        var importer = new RtfImporter();
        Assert.True(importer.TryImport(rtf, "3. Third\r\n4. Fourth", out var document));
        Assert.Equal("3. Third\n4. Fourth", new MarkdownRenderer().Render(document!));
    });

    [Fact]
    public void InvalidRtfFallsBackToConservativePlainText() => StaTestRunner.Run(() =>
    {
        var service = new ConversionService();
        var result = service.Convert(null, "{not-valid-rtf", "l one\nl two\nl three", OutputMode.PlainText);
        Assert.Equal("• one\n• two\n• three", result.Text);
        Assert.True(result.Document.UsedFallback);
    });

    [Fact]
    public void WordRtfRoundTripPreservesChineseListsAndInlineFormatting() => StaTestRunner.Run(() =>
    {
        var document = new RichDocument();
        var heading = DocumentBlock.Create(BlockKind.Heading, "中文标题", true);
        document.Blocks.Add(heading);
        var first = DocumentBlock.Create(BlockKind.ListItem, "第一项", true, true);
        first.ListKind = ListKind.Bullet;
        document.Blocks.Add(first);
        var nested = DocumentBlock.Create(BlockKind.ListItem, "链接");
        nested.ListKind = ListKind.Bullet;
        nested.Level = 1;
        nested.Inlines[0] = nested.Inlines[0] with { Link = "https://example.com" };
        document.Blocks.Add(nested);

        var plainText = new PlainTextRenderer().Render(document);
        var rtf = new WordRenderer().RenderRtf(document);
        using var richTextBox = new RichTextBox { Multiline = true };
        richTextBox.Rtf = rtf;
        Assert.Equal(Normalize(plainText), Normalize(richTextBox.Text));
        Assert.True(RtfRoundTripValidator.IsValid(rtf, plainText));
        Assert.Contains(@"48", rtf);
        Assert.DoesNotContain(@"\object", rtf);
    });

    [Fact]
    public void ConversionProducesWordRtfAndPlainText() => StaTestRunner.Run(() =>
    {
        var result = new ConversionService().Convert(
            "<h2>标题</h2><ul><li><strong>项目</strong></li></ul>",
            null,
            "标题\r\n项目",
            OutputMode.Word);
        Assert.NotNull(result.Rtf);
        Assert.Equal("标题\n• 项目", result.Text);
    });

    private static string Normalize(string value)
    {
        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd('\n');
        return string.Join('\n', normalized.Split('\n').Select(line => line.Replace('\t', ' ').Trim()));
    }
}
