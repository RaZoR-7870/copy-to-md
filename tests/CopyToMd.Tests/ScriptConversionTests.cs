using CopyToMd.Conversion;
using CopyToMd.Target;
using Xunit;

namespace CopyToMd.Tests;

public sealed class ScriptConversionTests
{
    private readonly HtmlImporter htmlImporter = new();
    private readonly MarkdownRenderer markdown = new();
    private readonly PlainTextRenderer plain = new();

    [Fact]
    public void HtmlSuperscriptAndSubscriptBecomeLatex()
    {
        Assert.True(htmlImporter.TryImport("<p>x<sup>2</sup> + H<sub>2</sub>O</p>", out var document));
        Assert.Equal("$x^{2}$ + $H_{2}O$", markdown.Render(document!));
    }

    [Fact]
    public void CssVerticalAlignBecomeLatex()
    {
        Assert.True(htmlImporter.TryImport("<p>x<span style=\"vertical-align:super\">2</span></p>", out var document));
        Assert.Equal("$x^{2}$", markdown.Render(document!));
        Assert.True(htmlImporter.TryImport("<p>H<span style=\"vertical-align:sub\">2</span>O</p>", out var subDocument));
        Assert.Equal("$H_{2}O$", markdown.Render(subDocument!));
    }

    [Fact]
    public void UnicodeScriptsInPlainTextBecomeLatex()
    {
        var document = new PlainTextImporter().Import("x² + H₂O");
        Assert.Equal("$x^{2}$ + $H_{2}O$", markdown.Render(document));
    }

    [Fact]
    public void CombinedSubscriptAndSuperscriptStayInOneMathSpan()
    {
        Assert.True(htmlImporter.TryImport("<p>x<sub>i</sub><sup>2</sup></p>", out var document));
        Assert.Equal("$x_{i}^{2}$", markdown.Render(document!));
    }

    [Fact]
    public void ChineseProseIsNotSwallowedIntoMath()
    {
        Assert.True(htmlImporter.TryImport("<p>变量x<sup>2</sup></p>", out var document));
        Assert.Equal("变量$x^{2}$", markdown.Render(document!));
    }

    [Fact]
    public void PlainTextRendererUsesUnicodeScripts()
    {
        Assert.True(htmlImporter.TryImport("<p>x<sup>2</sup> + H<sub>2</sub>O</p>", out var document));
        Assert.Equal("x² + H₂O", plain.Render(document!));
    }

    [Fact]
    public void PlainTextRendererFallsBackForUnmappableScripts()
    {
        Assert.True(htmlImporter.TryImport("<p>x<sup>ab</sup></p>", out var document));
        Assert.Equal("x^(ab)", plain.Render(document!));
    }

    [Fact]
    public void RtfCharacterOffsetsBecomeLatex() => StaTestRunner.Run(() =>
    {
        var importer = new RtfImporter();
        Assert.True(importer.TryImport(@"{\rtf1\ansi\pard x\super 2\nosupersub\par}", "x2", out var document));
        Assert.Equal("$x^{2}$", markdown.Render(document!));
        Assert.True(importer.TryImport(@"{\rtf1\ansi\pard H\sub 2\nosupersub O\par}", "H2O", out var subDocument));
        Assert.Equal("$H_{2}O$", markdown.Render(subDocument!));
    });

    [Fact]
    public void UnicodeInsideScriptTagsIsDecoded()
    {
        Assert.True(htmlImporter.TryImport("<p>x<sup>²</sup></p>", out var document));
        Assert.Equal("$x^{2}$", markdown.Render(document!));
        Assert.Equal("x²", plain.Render(document!));
    }

    [Fact]
    public void ConversionServiceKeepsWordRtfForScripts() => StaTestRunner.Run(() =>
    {
        var result = new ConversionService().Convert(
            "<p>x<sup>2</sup> + H<sub>2</sub>O</p>",
            null,
            "x2 + H2O",
            OutputMode.Word);
        Assert.NotNull(result.Rtf);
        Assert.Equal("x² + H₂O", result.Text);
        Assert.True(RtfRoundTripValidator.IsValid(result.Rtf!, result.Text));
    });

    [Fact]
    public void WordRtfKeepsRealScriptFormatting() => StaTestRunner.Run(() =>
    {
        var document = new RichDocument();
        var block = new DocumentBlock();
        block.Inlines.Add(new InlineRun("x"));
        block.Inlines.Add(new InlineRun("2", Script: ScriptKind.Superscript));
        block.Inlines.Add(new InlineRun(" + H"));
        block.Inlines.Add(new InlineRun("2", Script: ScriptKind.Subscript));
        block.Inlines.Add(new InlineRun("O"));
        document.Blocks.Add(block);

        var rtf = new WordRenderer().RenderRtf(document);
        Assert.Contains(@"\super", rtf);
        Assert.Contains(@"\sub", rtf);
        Assert.Contains(@"\nosupersub", rtf);

        using var richTextBox = new RichTextBox { Multiline = true };
        richTextBox.Rtf = rtf;
        Assert.Equal("x2 + H2O", richTextBox.Text.Replace("\r\n", "\n").TrimEnd('\n'));
        richTextBox.Select(1, 1);
        Assert.Contains(@"\super", richTextBox.SelectedRtf);
        Assert.Equal("x² + H₂O", plain.Render(document));
        Assert.True(RtfRoundTripValidator.IsValid(rtf, plain.Render(document)));
    });
}
