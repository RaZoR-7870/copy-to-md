using CopyToMd.Configuration;
using CopyToMd.Conversion;
using CopyToMd.Target;
using Xunit;

namespace CopyToMd.Tests;

public sealed class StyleConversionTests
{
    [Fact]
    public void MinimalStyleRemovesInlineFormattingAndLinks()
    {
        var service = new ConversionService(StyleSettings.Minimal());
        var result = service.Convert(
            "<p><strong><em><span style=\"color:red\">Text</span></em></strong> <a href=\"https://example.com\">link</a></p>",
            null,
            "Text link",
            OutputMode.Markdown);
        Assert.Equal("Text link", result.Text);
    }

    [Fact]
    public void CaseTransformLowercasesText()
    {
        var service = new ConversionService(new StyleSettings { CaseTransform = CaseTransform.Lowercase });
        var result = service.Convert("<p>ABC</p>", null, "ABC", OutputMode.Markdown);
        Assert.Equal("abc", result.Text);
    }

    [Fact]
    public void MarkdownBulletMarkerAndIndentAreConfigurable()
    {
        var service = new ConversionService(new StyleSettings { BulletMarker = BulletMarker.Asterisk, ListIndentSpaces = 4 });
        var result = service.Convert("<ul><li>parent<ul><li>child</li></ul></li></ul>", null, "parent\nchild", OutputMode.Markdown);
        Assert.Equal("* parent\n    * child", result.Text);
    }

    [Fact]
    public void HeadingLevelOffsetIsApplied()
    {
        var service = new ConversionService(new StyleSettings { HeadingLevelOffset = 1 });
        var result = service.Convert("<h1>Title</h1>", null, "Title", OutputMode.Markdown);
        Assert.Equal("## Title", result.Text);
    }

    [Fact]
    public void ListsCanBeConvertedToParagraphs()
    {
        var service = new ConversionService(new StyleSettings { ListMode = ListMode.ToParagraph });
        var result = service.Convert("<ul><li>item</li></ul>", null, "item", OutputMode.Markdown);
        Assert.Equal("item", result.Text);
    }

    [Fact]
    public void OrderedListsCanBeRestarted()
    {
        var service = new ConversionService(new StyleSettings { RestartOrderedLists = true });
        var result = service.Convert("<ol start=\"5\"><li>A</li><li>B</li></ol>", null, "A\nB", OutputMode.Markdown);
        Assert.Equal("1. A\n2. B", result.Text);
    }

    [Fact]
    public void MarkdownScriptModesAreConfigurable()
    {
        var unicode = new ConversionService(new StyleSettings { MarkdownScriptMode = MarkdownScriptMode.Unicode });
        Assert.Equal("x²", unicode.Convert("<p>x<sup>2</sup></p>", null, "x2", OutputMode.Markdown).Text);

        var html = new ConversionService(new StyleSettings { MarkdownScriptMode = MarkdownScriptMode.Html });
        Assert.Equal("x<sup>2</sup>", html.Convert("<p>x<sup>2</sup></p>", null, "x2", OutputMode.Markdown).Text);
    }

    [Fact]
    public void PlainTextAsciiFallbackUsesNotation()
    {
        var service = new ConversionService(new StyleSettings { PlainScriptMode = PlainScriptMode.AsciiFallback });
        Assert.Equal("x^2", service.Convert("<p>x<sup>2</sup></p>", null, "x2", OutputMode.PlainText).Text);
    }

    [Fact]
    public void SuspectCleaningModesAreConfigurable()
    {
        var aggressive = new PlainTextImporter().Import("l single", new StyleSettings { SuspectBulletCleaning = SuspectBulletCleaning.Aggressive });
        Assert.Equal("- single", new MarkdownRenderer().Render(aggressive));

        var off = new PlainTextImporter().Import("l single", new StyleSettings { SuspectBulletCleaning = SuspectBulletCleaning.Off });
        Assert.Equal("l single", new PlainTextRenderer().Render(off));
    }

    [Fact]
    public void WordKeepsColorUnderlineAndStrikeWhenEnabled() => StaTestRunner.Run(() =>
    {
        var style = new StyleSettings { KeepColor = true, KeepUnderline = true, KeepStrikethrough = true };
        var document = new RichDocument();
        var block = new DocumentBlock();
        block.Inlines.Add(new InlineRun("styled", Underline: true, Strikethrough: true, Color: "#FF0000"));
        document.Blocks.Add(block);

        var rtf = new WordRenderer(style).RenderRtf(document);
        Assert.Contains(@"\colortbl", rtf);
        Assert.Contains(@"\cf1", rtf);
        Assert.Contains(@"\ul", rtf);
        Assert.Contains(@"\strike", rtf);
        Assert.True(RtfRoundTripValidator.IsValid(rtf, "styled"));
    });

    [Fact]
    public void AppSettingsRoundTripsStyle()
    {
        var path = Path.GetTempFileName();
        try
        {
            var settings = new AppSettings { HotKey = "Ctrl+Shift+P" };
            settings.Style.KeepItalic = false;
            settings.Style.BulletMarker = BulletMarker.Plus;
            settings.Save(path);

            var loaded = AppSettings.Load(path);
            Assert.Equal("Ctrl+Shift+P", loaded.HotKey);
            Assert.False(loaded.Style.KeepItalic);
            Assert.Equal(BulletMarker.Plus, loaded.Style.BulletMarker);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
