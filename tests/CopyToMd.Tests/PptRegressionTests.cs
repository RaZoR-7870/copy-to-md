using System.Text;
using CopyToMd.Clipboard;
using CopyToMd.Conversion;
using CopyToMd.Target;
using Xunit;

namespace CopyToMd.Tests;

public class PptRegressionTests
{
    private const string PptFragment = """
        <html><body><!--StartFragment-->
        <div style='margin-left:.38in;text-indent:-.38in'><span style='font-size:24.0pt'><span style='mso-special-format:bullet;font-family:Wingdings'>n</span></span><span style='font-weight:bold'>We’ve touched on basic digital logic</span></div>
        <div style='margin-left:.81in;text-indent:-.31in'><span style='font-size:20.0pt'><span style='mso-special-format:bullet;font-family:Wingdings'>l</span></span><span style='font-weight:bold'>Transistors</span></div>
        <div style='margin-left:.81in;text-indent:-.31in'><span style='font-size:20.0pt'><span style='mso-special-format:bullet;font-family:Wingdings'>l</span></span><span style='font-weight:bold'>Gates</span></div>
        <div style='margin-left:.38in;text-indent:-.38in'><span style='font-size:24.0pt'><span style='mso-special-format:bullet;font-family:Wingdings'>n</span></span><span style='font-weight:bold'>Built some simple circuits</span></div>
        <div style='margin-left:.81in;text-indent:-.31in'><span style='font-size:20.0pt'><span style='mso-special-format:bullet;font-family:Wingdings'>l</span></span><span style='font-weight:bold'>adder, subtracter，Incrementer</span></div>
        <!--EndFragment--></body></html>
        """;

    [Fact]
    public void FlatPowerPointBulletsRecoverNestingFromMargin()
    {
        var importer = new HtmlImporter();
        Assert.True(importer.TryImport(PptFragment, out var document));
        Assert.NotNull(document);
        Assert.Equal(5, document!.Blocks.Count);
        Assert.All(document.Blocks, block => Assert.Equal(BlockKind.ListItem, block.Kind));
        Assert.Equal(new[] { 0, 1, 1, 0, 1 }, document.Blocks.Select(block => block.Level).ToArray());
        Assert.Equal("We’ve touched on basic digital logic", document.Blocks[0].Text);
        Assert.Equal("adder, subtracter，Incrementer", document.Blocks[4].Text);
    }

    [Fact]
    public void FlatPowerPointBulletsRenderNestedMarkdown()
    {
        var importer = new HtmlImporter();
        Assert.True(importer.TryImport(PptFragment, out var document));
        var markdown = new MarkdownRenderer().Render(document!);
        Assert.Contains("- **We’ve touched on basic digital logic**", markdown);
        Assert.Contains("  - **Transistors**", markdown);
        Assert.Contains("  - **Gates**", markdown);
        Assert.Contains("- **Built some simple circuits**", markdown);
        Assert.Contains("  - **adder, subtracter，Incrementer**", markdown);
    }

    private const string AutoWrapFragment = """
        <html><body><!--StartFragment--><p style='margin-left:0in'><span style='font-weight:bold'>The data path of a computer is all the logic used to<br>
        process information.</span></p><!--EndFragment--></body></html>
        """;

    [Fact]
    public void AutoWrapSoftBreakJoinsIntoOneSentence()
    {
        var service = new ConversionService();
        var result = service.Convert(AutoWrapFragment, null, null, OutputMode.Markdown);
        Assert.Equal("**The data path of a computer is all the logic used to process information.**", result.Text);
    }

    [Fact]
    public void SoftBreakKeepModePreservesLineBreak()
    {
        var service = new ConversionService(new StyleSettings { SoftBreakMode = SoftBreakMode.Keep });
        var result = service.Convert(AutoWrapFragment, null, null, OutputMode.Markdown);
        Assert.Contains('\n', result.Text);
    }

    [Fact]
    public void CfHtmlExtractorUsesUtf8ByteOffsets()
    {
        const string fragment = "<div>你好，世界</div>";
        const string prefix = "<html><body><!--StartFragment-->";
        const string suffix = "<!--EndFragment--></body></html>";
        const string header = "Version:1.0\r\nStartHTML:0000000000\r\nEndHTML:0000000000\r\nStartFragment:0000000000\r\nEndFragment:0000000000\r\n";

        var headerLength = Encoding.UTF8.GetByteCount(header);
        var startFragment = headerLength + Encoding.UTF8.GetByteCount(prefix);
        var endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
        var full = header + prefix + fragment + suffix;
        var endHtml = Encoding.UTF8.GetByteCount(full);
        full = full
            .Replace("EndHTML:0000000000", $"EndHTML:{endHtml:D10}")
            .Replace("StartFragment:0000000000", $"StartFragment:{startFragment:D10}")
            .Replace("EndFragment:0000000000", $"EndFragment:{endFragment:D10}");

        var extracted = CfHtmlExtractor.ExtractFragment(full);
        Assert.Equal(fragment, extracted.Trim());
    }
}