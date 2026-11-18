using CopyToMd.Conversion;
using Xunit;

namespace CopyToMd.Tests;

public sealed class PlainTextImporterTests
{
    private readonly PlainTextImporter importer = new();
    private readonly MarkdownRenderer markdown = new();
    private readonly PlainTextRenderer plain = new();

    [Fact]
    public void ConvertsMultiLineSuspectBulletsToMarkdown()
    {
        var document = importer.Import("l 第一项\r\nm 第二项\r\nn 第三项");
        Assert.Equal("- 第一项\r\n- 第二项\r\n- 第三项", markdown.Render(document));
    }

    [Fact]
    public void KeepsSingleSuspectLine()
    {
        var document = importer.Import("l single item");
        Assert.Equal("l single item", plain.Render(document));
        Assert.False(document.HasSemanticFormatting);
    }

    [Fact]
    public void KeepsNormalEnglishAndFormula()
    {
        const string text = "lorem ipsum\nlink is useful\nl = m + n";
        Assert.Equal(text, plain.Render(importer.Import(text)));
    }

    [Fact]
    public void KeepsSingleSuspectLineAdjacentToKnownList()
    {
        var document = importer.Import("• first\nn normal text");
        Assert.Equal("• first\nn normal text", plain.Render(document));
    }

    [Fact]
    public void KeepsTwoDifferentLeadingLettersWithoutStrongListContext()
    {
        const string text = "l is length\nm is mass";
        Assert.Equal(text, plain.Render(importer.Import(text)));
    }

    [Fact]
    public void KeepsTwoIdenticalLeadingLettersWithoutRichTextEvidence()
    {
        const string text = "l is length\nl is luminous intensity";
        Assert.Equal(text, plain.Render(importer.Import(text)));
    }

    [Fact]
    public void PreservesNestedListLevel()
    {
        var document = importer.Import("l first\n    m nested\nn last");
        Assert.Equal("- first\n  - nested\n- last", markdown.Render(document));
    }

    [Fact]
    public void DoesNotCleanFencedCode()
    {
        const string text = "```\nl value\nn value\n```";
        Assert.Equal(text, plain.Render(importer.Import(text)));
    }

    [Fact]
    public void ConvertsOrderedLists()
    {
        var document = importer.Import("1. first\n2. second");
        Assert.Equal("1. first\n2. second", markdown.Render(document));
    }

    [Fact]
    public void PreservesCrLfForPlainText()
    {
        var document = importer.Import("l first\r\nl second\r\nl third");
        Assert.Equal("• first\r\n• second\r\n• third", plain.Render(document));
    }
}
