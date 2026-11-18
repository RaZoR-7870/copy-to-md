using System.Text;
using CopyToMd.Clipboard;
using CopyToMd.Conversion;
using Xunit;

namespace CopyToMd.Tests;

public sealed class HtmlImporterTests
{
    private readonly HtmlImporter importer = new();
    private readonly MarkdownRenderer renderer = new();

    [Fact]
    public void ConvertsSemanticFormattingAndNestedLists()
    {
        const string html = "<h1>标题</h1><ul><li><strong>粗体</strong>与<em>斜体</em><ul><li><a href=\"https://example.com\">链接</a></li></ul></li></ul>";
        Assert.True(importer.TryImport(html, out var document));
        Assert.Equal("# 标题\n\n- **粗体**与*斜体*\n  - [链接](https://example.com/)", renderer.Render(document!));
    }

    [Fact]
    public void RecognizesWingdingsParagraphMarkers()
    {
        const string html = "<div><span style=\"font-family:Wingdings\">l</span><span> First</span></div>" +
                            "<div><span style=\"font-family:Wingdings\">m</span><span>\tSecond</span></div>";
        Assert.True(importer.TryImport(html, out var document));
        Assert.Equal("- First\n- Second", renderer.Render(document!));
    }

    [Fact]
    public void DoesNotRemoveNormalLetterInParagraph()
    {
        const string html = "<p>l normal text</p><p><span style=\"font-family:Wingdings\">l</span> bullet</p>";
        Assert.True(importer.TryImport(html, out var document));
        Assert.Equal("l normal text\n\n- bullet", renderer.Render(document!));
    }

    [Fact]
    public void UnsafeLinkKeepsDisplayTextOnly()
    {
        Assert.True(importer.TryImport("<p><a href=\"javascript:alert(1)\">显示文字</a></p>", out var document));
        Assert.Equal("显示文字", renderer.Render(document!));
    }

    [Fact]
    public void CfHtmlUsesByteOffsetsWithChineseText()
    {
        const string fragment = "<html><body><!--StartFragment--><h1>中文标题</h1><!--EndFragment--></body></html>";
        const string headerTemplate = "Version:1.0\r\nStartFragment:{0:D10}\r\nEndFragment:{1:D10}\r\n";
        var emptyHeader = string.Format(headerTemplate, 0, 0);
        var start = Encoding.UTF8.GetByteCount(emptyHeader);
        var end = start + Encoding.UTF8.GetByteCount(fragment);
        var html = string.Format(headerTemplate, start, end) + fragment;

        var extracted = CfHtmlExtractor.ExtractFragment(html);
        Assert.Equal(fragment, extracted);
        Assert.True(importer.TryImport(html, out var document));
        Assert.Equal("# 中文标题", renderer.Render(document!));
    }

    [Fact]
    public void InvalidCfHtmlOffsetsFallBackToMarkers()
    {
        const string html = "Version:1.0\r\nStartFragment:999999\r\nEndFragment:9999999\r\n" +
                            "<html><body><!--StartFragment--><p>正文</p><!--EndFragment--></body></html>";
        Assert.Equal("<p>正文</p>", CfHtmlExtractor.ExtractFragment(html));
    }

    [Fact]
    public void SeparatesMultipleParagraphsInsideListItem()
    {
        Assert.True(importer.TryImport("<ul><li><p>one</p><p>two</p></li></ul>", out var document));
        Assert.Equal("- one two", renderer.Render(document!));
    }

    [Fact]
    public void FindsNestedListInsideBlockWrapper()
    {
        Assert.True(importer.TryImport("<ul><li>parent<div><ul><li>child</li></ul></div></li></ul>", out var document));
        Assert.Equal("- parent\n  - child", renderer.Render(document!));
    }

    [Fact]
    public void PreservesOrderedListStartAndItemValues()
    {
        Assert.True(importer.TryImport("<ol start=\"3\"><li>A</li><li value=\"7\">B</li><li>C</li></ol>", out var document));
        Assert.Equal("3. A\n7. B\n8. C", renderer.Render(document!));
    }

    [Fact]
    public void MarkdownEscapesSpecialCharacters()
    {
        Assert.True(importer.TryImport("<p><strong>a*b[c]</strong></p>", out var document));
        Assert.Equal(@"**a\*b\[c\]**", renderer.Render(document!));
    }
}
