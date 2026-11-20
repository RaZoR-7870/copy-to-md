using CopyToMd.Conversion;
using CopyToMd.Target;
using Xunit;

namespace CopyToMd.Tests;

public class TableAndHeadingTests
{
    private const string DeepDashFragment = """
        <html><body><!--StartFragment-->
        <div style='margin-left:.38in;text-indent:-.38in'><span style='font-size:24.0pt'><span style='mso-special-format:bullet;font-family:Wingdings'>n</span></span><span style='font-size:24.0pt;font-weight:bold'>Properties of logic gates</span></div>
        <div style='margin-left:.81in;text-indent:-.31in'><span style='font-size:20.0pt'><span style='mso-special-format:bullet;font-family:Wingdings'>l</span></span><span style='font-size:20.0pt;font-weight:bold'>Completeness</span></div>
        <div style='margin-left:1.25in;text-indent:-.31in'><span style='font-size:18.0pt'><span style='mso-special-format:bullet;font-family:Arial'>-</span></span><span style='font-size:18.0pt;font-weight:bold'>can implement any truth table with AND, OR, NOT</span></div>
        <div style='margin-left:.81in;text-indent:-.31in'><span style='font-size:20.0pt'><span style='mso-special-format:bullet;font-family:Wingdings'>l</span></span><span style='font-size:20.0pt;font-weight:bold'>DeMorgan's Law</span></div>
        <div style='margin-left:1.25in;text-indent:-.31in'><span style='font-size:18.0pt'><span style='mso-special-format:bullet;font-family:Arial'>-</span></span><span style='font-size:18.0pt;font-weight:bold'>convert AND to OR by inverting inputs and output</span></div>
        <!--EndFragment--></body></html>
        """;

    [Fact]
    public void LiteralDashBulletsRecoverThirdLevelWithoutBlankLines()
    {
        var result = new ConversionService().Convert(DeepDashFragment, null, null, OutputMode.Markdown);
        Assert.Equal(
            "- **Properties of logic gates**\n" +
            "  - **Completeness**\n" +
            "    - **can implement any truth table with AND, OR, NOT**\n" +
            "  - **DeMorgan's Law**\n" +
            "    - **convert AND to OR by inverting inputs and output**",
            result.Text);
    }

    private const string TableFragment =
        "<table><tr><th>Signal</th><th>Meaning</th></tr><tr><td>ADD</td><td>add numbers</td></tr><tr><td>LD</td><td>load</td></tr></table>";

    [Fact]
    public void TableRendersGitHubMarkdown()
    {
        var result = new ConversionService().Convert(TableFragment, null, null, OutputMode.Markdown);
        Assert.Equal(
            "| Signal | Meaning |\n" +
            "| --- | --- |\n" +
            "| ADD | add numbers |\n" +
            "| LD | load |",
            result.Text);
    }

    [Fact]
    public void TableRendersTabSeparatedPlainText()
    {
        var result = new ConversionService().Convert(TableFragment, null, null, OutputMode.PlainText);
        Assert.Equal("Signal\tMeaning\nADD\tadd numbers\nLD\tload", result.Text);
    }

    [Fact]
    public void TableWordRtfPassesRoundTrip() => StaTestRunner.Run(() =>
    {
        var result = new ConversionService().Convert(TableFragment, null, null, OutputMode.Word);
        Assert.NotNull(result.Rtf);
        Assert.Equal("Signal\tMeaning\nADD\tadd numbers\nLD\tload", result.Text);
    });

    [Fact]
    public void TableEscapesPipesAndPadsUnevenRows()
    {
        var result = new ConversionService().Convert(
            "<table><tr><td>a|b</td><td><b>bold</b></td></tr><tr><td>c</td></tr></table>",
            null, null, OutputMode.Markdown);
        Assert.Contains(@"a\|b", result.Text);
        Assert.Contains("**bold**", result.Text);
        Assert.Contains("| c |  |", result.Text);
    }

    private const string HeadingFragment = """
        <html><body><!--StartFragment-->
        <p style='margin-left:0in'><span style='font-size:24.0pt;font-weight:bold'>The data path of a computer is all the logic used to process information.</span></p>
        <div style='margin-left:.63in;text-indent:-.26in'><span style='font-size:20.0pt'><span style='mso-special-format:bullet;font-family:Wingdings'>l</span></span><span style='font-size:20.0pt;font-weight:bold'>See the data path of the LC-3 on next slide.</span></div>
        <p style='margin-left:0in'><span style='font-size:24.0pt;font-weight:bold'>Combinational Logic</span></p>
        <div style='margin-left:.63in;text-indent:-.26in'><span style='font-size:20.0pt'><span style='mso-special-format:bullet;font-family:Wingdings'>l</span></span><span style='font-size:20.0pt;font-weight:bold'>Decoders -- convert instructions into control signals</span></div>
        <div style='margin-left:.63in;text-indent:-.26in'><span style='font-size:20.0pt'><span style='mso-special-format:bullet;font-family:Wingdings'>l</span></span><span style='font-size:20.0pt;font-weight:bold'>Multiplexers -- select inputs and outputs</span></div>
        <p style='margin-left:0in'><span style='font-size:24.0pt;font-weight:bold'>Sequential Logic</span></p>
        <div style='margin-left:.63in;text-indent:-.26in'><span style='font-size:20.0pt'><span style='mso-special-format:bullet;font-family:Wingdings'>l</span></span><span style='font-size:20.0pt;font-weight:bold'>State machine -- coordinate control signals and data movement</span></div>
        <!--EndFragment--></body></html>
        """;

    [Fact]
    public void LargerFontShortParagraphsBecomeHeadings()
    {
        var result = new ConversionService().Convert(HeadingFragment, null, null, OutputMode.Markdown);
        Assert.Contains("# Combinational Logic", result.Text);
        Assert.Contains("# Sequential Logic", result.Text);
        Assert.Contains("- **Decoders -- convert instructions into control signals**", result.Text);
    }

    [Fact]
    public void LongSentenceStaysParagraphEvenWhenLarger()
    {
        var result = new ConversionService().Convert(HeadingFragment, null, null, OutputMode.Markdown);
        Assert.DoesNotContain("# The data path", result.Text);
        Assert.Contains("**The data path of a computer is all the logic used to process information.**", result.Text);
    }

    private const string ShiftedMinusOne =
        "**Properties of logic gates**\n" +
        "- **Completeness**\n" +
        "  - **can implement any truth table with AND, OR, NOT**\n" +
        "- **DeMorgan's Law**\n" +
        "  - **convert AND to OR by inverting inputs and output**";

    [Fact]
    public void ListLevelOffsetMinusOneTurnsTopLevelIntoText()
    {
        var style = new StyleSettings { ListLevelOffset = -1 };
        var result = new ConversionService(style).Convert(DeepDashFragment, null, null, OutputMode.Markdown);
        Assert.Equal(ShiftedMinusOne, result.Text);
    }

    [Fact]
    public void ListLevelOffsetClampsSoOnlyOneLevelBecomesText()
    {
        var style = new StyleSettings { ListLevelOffset = -5 };
        var result = new ConversionService(style).Convert(DeepDashFragment, null, null, OutputMode.Markdown);
        Assert.Equal(ShiftedMinusOne, result.Text);
    }

    private const string PartialHierarchyFragment = """
        <html><body><!--StartFragment-->
        <div style='margin-left:.81in;text-indent:-.31in'><span style='mso-special-format:bullet;font-family:Wingdings'>l</span><span style='font-weight:bold'>Completeness</span></div>
        <div style='margin-left:1.25in;text-indent:-.31in'><span style='mso-special-format:bullet;font-family:Arial'>-</span><span style='font-weight:bold'>can implement any truth table with AND, OR, NOT</span></div>
        <!--EndFragment--></body></html>
        """;

    [Fact]
    public void PartialHierarchyKeepsAbsoluteLevels()
    {
        var result = new ConversionService().Convert(PartialHierarchyFragment, null, null, OutputMode.Markdown);
        Assert.Equal(
            "  - **Completeness**\n" +
            "    - **can implement any truth table with AND, OR, NOT**",
            result.Text);
    }

    [Fact]
    public void ListLevelOffsetMinusTwoWorksWhenTopLevelIsSecond()
    {
        var style = new StyleSettings { ListLevelOffset = -2 };
        var result = new ConversionService(style).Convert(PartialHierarchyFragment, null, null, OutputMode.Markdown);
        Assert.Equal(
            "**Completeness**\n" +
            "- **can implement any truth table with AND, OR, NOT**",
            result.Text);
    }

    [Fact]
    public void OverNegativeOffsetStillConvertsOnlyOneLevel()
    {
        var style = new StyleSettings { ListLevelOffset = -6 };
        var result = new ConversionService(style).Convert(PartialHierarchyFragment, null, null, OutputMode.Markdown);
        Assert.Equal(
            "**Completeness**\n" +
            "- **can implement any truth table with AND, OR, NOT**",
            result.Text);
    }

    [Fact]
    public void ListLevelOffsetPositiveDeepensIndent()
    {
        var style = new StyleSettings { ListLevelOffset = 1 };
        var result = new ConversionService(style).Convert(DeepDashFragment, null, null, OutputMode.Markdown);
        Assert.Equal(
            "  - **Properties of logic gates**\n" +
            "    - **Completeness**\n" +
            "      - **can implement any truth table with AND, OR, NOT**\n" +
            "    - **DeMorgan's Law**\n" +
            "      - **convert AND to OR by inverting inputs and output**",
            result.Text);
    }

    private const string MixedProseFragment = """
        <html><body><!--StartFragment-->
        <p style='margin-left:0in'><span style='font-weight:bold'>Intro sentence.</span></p>
        <div style='margin-left:.38in;text-indent:-.38in'><span style='mso-special-format:bullet;font-family:Wingdings'>n</span><span style='font-weight:bold'>Top item</span></div>
        <div style='margin-left:.81in;text-indent:-.31in'><span style='mso-special-format:bullet;font-family:Wingdings'>l</span><span style='font-weight:bold'>Sub item</span></div>
        <!--EndFragment--></body></html>
        """;

    [Fact]
    public void OffsetSkippedWhenSourceContainsPlainText()
    {
        var style = new StyleSettings { ListLevelOffset = -1 };
        var result = new ConversionService(style).Convert(MixedProseFragment, null, null, OutputMode.Markdown);
        Assert.Equal("**Intro sentence.**\n- **Top item**\n  - **Sub item**", result.Text);
    }

    [Fact]
    public void HeadingsDoNotBlockOffset()
    {
        const string html = "<h2>Section</h2>" +
            "<div style='margin-left:.38in;text-indent:-.38in'><span style='mso-special-format:bullet;font-family:Wingdings'>n</span><span style='font-weight:bold'>Top item</span></div>" +
            "<div style='margin-left:.81in;text-indent:-.31in'><span style='mso-special-format:bullet;font-family:Wingdings'>l</span><span style='font-weight:bold'>Sub item</span></div>";
        var style = new StyleSettings { ListLevelOffset = -1 };
        var result = new ConversionService(style).Convert(html, null, null, OutputMode.Markdown);
        Assert.Equal("## Section\n**Top item**\n- **Sub item**", result.Text);
    }

    [Fact]
    public void EmptyParagraphsDoNotBlockOffset()
    {
        const string html = "<p></p>" +
            "<div style='margin-left:.38in;text-indent:-.38in'><span style='mso-special-format:bullet;font-family:Wingdings'>n</span><span style='font-weight:bold'>Top item</span></div>";
        var style = new StyleSettings { ListLevelOffset = -1 };
        var result = new ConversionService(style).Convert(html, null, null, OutputMode.Markdown);
        Assert.Equal("**Top item**", result.Text);
    }
}