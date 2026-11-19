using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using CopyToMd.Clipboard;

namespace CopyToMd.Conversion;

public sealed class HtmlImporter
{
    private sealed record InlineStyle(
        bool Bold = false,
        bool Italic = false,
        string? Link = null,
        bool SymbolFont = false,
        ScriptKind Script = ScriptKind.None,
        bool Underline = false,
        bool Strikethrough = false,
        string? Color = null);

    private sealed class InlineAccumulator
    {
        public List<InlineRun> Runs { get; } = [];
        public bool FoundSymbolBullet { get; private set; }
        public bool AtStart { get; private set; } = true;
        public bool HasContent => Runs.Any(run => !string.IsNullOrWhiteSpace(run.Text));
        private bool skipWhitespaceAfterBullet;

        public void MarkSymbolBullet()
        {
            if (AtStart)
            {
                FoundSymbolBullet = true;
                skipWhitespaceAfterBullet = true;
            }
        }

        public void Add(string? value, InlineStyle style)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            var text = WhitespaceRegex().Replace(value, " ");
            if (skipWhitespaceAfterBullet)
            {
                text = text.TrimStart();
                skipWhitespaceAfterBullet = false;
            }

            if (AtStart && string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (text.Length == 0)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                AtStart = false;
            }

            if (Runs.Count > 0)
            {
                var previous = Runs[^1];
                if (previous.Bold == style.Bold && previous.Italic == style.Italic &&
                    previous.Link == style.Link && previous.Script == style.Script &&
                    previous.Underline == style.Underline && previous.Strikethrough == style.Strikethrough &&
                    previous.Color == style.Color)
                {
                    Runs[^1] = previous with { Text = previous.Text + text };
                    return;
                }
            }

            Runs.Add(new InlineRun(text, style.Bold, style.Italic, style.Link, style.Script,
                style.Underline, style.Strikethrough, style.Color));
        }

        public void AddLineBreak()
        {
            if (!AtStart)
            {
                Runs.Add(new InlineRun("\n"));
            }
        }

        public void AddBlockSeparator()
        {
            if (HasContent && Runs[^1].Text.EndsWith(' '))
            {
                return;
            }

            if (HasContent)
            {
                Runs.Add(new InlineRun(" "));
            }
        }

        public List<InlineRun> Finish()
        {
            if (Runs.Count == 0)
            {
                return Runs;
            }

            Runs[0] = Runs[0] with { Text = Runs[0].Text.TrimStart() };
            Runs[^1] = Runs[^1] with { Text = Runs[^1].Text.TrimEnd() };
            return Runs.Where(run => run.Text.Length > 0).ToList();
        }
    }

    public bool TryImport(string? html, out RichDocument? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        try
        {
            var fragment = CfHtmlExtractor.ExtractFragment(html);
            if (string.IsNullOrWhiteSpace(fragment))
            {
                return false;
            }

            var parsed = new HtmlParser().ParseDocument(fragment);
            var result = new RichDocument { Source = DocumentSource.Html };
            ProcessContainer(parsed.Body ?? parsed.DocumentElement, result);
            if (result.Blocks.Count == 0)
            {
                return false;
            }

            document = result;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static void ProcessContainer(INode container, RichDocument document)
    {
        var inlineBuffer = new InlineAccumulator();

        void FlushInlineBuffer()
        {
            var runs = inlineBuffer.Finish();
            if (runs.Count > 0)
            {
                AddBlock(document, BlockKind.Paragraph, runs, inlineBuffer.FoundSymbolBullet);
            }

            inlineBuffer = new InlineAccumulator();
        }

        foreach (var node in container.ChildNodes)
        {
            if (node is not IElement element)
            {
                if (node.NodeType == NodeType.Text)
                {
                    ReadInline(node, new InlineStyle(), inlineBuffer);
                }

                continue;
            }

            var tag = element.TagName;
            if (tag is "SCRIPT" or "STYLE" or "HEAD")
            {
                continue;
            }

            if (tag is "UL" or "OL")
            {
                FlushInlineBuffer();
                ProcessList(element, document, tag == "OL" ? ListKind.Ordered : ListKind.Bullet, 0);
                continue;
            }

            if (HeadingLevel(tag) is { } headingLevel)
            {
                FlushInlineBuffer();
                var accumulator = ReadBlockInline(element);
                var runs = accumulator.Finish();
                if (runs.Count > 0)
                {
                    var block = AddBlock(document, BlockKind.Heading, runs, accumulator.FoundSymbolBullet);
                    block.HeadingLevel = headingLevel;
                }

                continue;
            }

            if (IsBlockElement(tag))
            {
                FlushInlineBuffer();
                if (tag == "LI")
                {
                    ProcessListItem(element, document, ListKind.Bullet, 0, 1);
                }
                else if (HasBlockChildren(element))
                {
                    ProcessContainer(element, document);
                }
                else
                {
                    var accumulator = ReadBlockInline(element);
                    var runs = accumulator.Finish();
                    if (runs.Count > 0)
                    {
                        AddBlock(document, BlockKind.Paragraph, runs, accumulator.FoundSymbolBullet);
                    }
                }

                continue;
            }

            ReadInline(element, new InlineStyle(), inlineBuffer);
        }

        FlushInlineBuffer();
    }

    private static void ProcessList(IElement list, RichDocument document, ListKind kind, int level)
    {
        var number = PositiveNumber(list.GetAttribute("start"), 1);
        foreach (var item in list.Children.Where(child => child.TagName == "LI"))
        {
            number = PositiveNumber(item.GetAttribute("value"), number);
            ProcessListItem(item, document, kind, level, number);
            number++;
        }
    }

    private static void ProcessListItem(IElement item, RichDocument document, ListKind kind, int level, int number)
    {
        var accumulator = new InlineAccumulator();
        foreach (var node in item.ChildNodes)
        {
            if (node is IElement { TagName: "UL" or "OL" })
            {
                continue;
            }

            if (node is IElement blockElement && IsBlockElement(blockElement.TagName))
            {
                accumulator.AddBlockSeparator();
            }

            ReadInline(node, new InlineStyle(), accumulator);
        }

        var runs = accumulator.Finish();
        if (runs.Count > 0)
        {
            var block = AddBlock(document, BlockKind.ListItem, runs, false);
            block.ListKind = kind;
            block.Level = level;
            block.Number = number;
        }

        foreach (var nested in FindNestedLists(item))
        {
            ProcessList(nested, document, nested.TagName == "OL" ? ListKind.Ordered : ListKind.Bullet, level + 1);
        }
    }

    private static IEnumerable<IElement> FindNestedLists(IElement item)
    {
        foreach (var child in item.Children)
        {
            if (child.TagName is "UL" or "OL")
            {
                yield return child;
            }
            else if (child.TagName != "LI")
            {
                foreach (var nested in FindNestedLists(child))
                {
                    yield return nested;
                }
            }
        }
    }

    private static int PositiveNumber(string? value, int fallback) =>
        int.TryParse(value, out var number) && number > 0 ? number : fallback;

    private static DocumentBlock AddBlock(RichDocument document, BlockKind kind, List<InlineRun> runs, bool symbolBullet)
    {
        var block = new DocumentBlock
        {
            Kind = symbolBullet ? BlockKind.ListItem : kind,
            ListKind = symbolBullet ? ListKind.Bullet : ListKind.None
        };
        block.Inlines.AddRange(runs);
        document.Blocks.Add(block);
        return block;
    }

    private static InlineAccumulator ReadBlockInline(INode node)
    {
        var accumulator = new InlineAccumulator();
        foreach (var child in node.ChildNodes)
        {
            ReadInline(child, new InlineStyle(), accumulator);
        }

        return accumulator;
    }

    private static void ReadInline(INode node, InlineStyle style, InlineAccumulator accumulator)
    {
        if (node.NodeType == NodeType.Text)
        {
            var text = node.TextContent;
            if (style.SymbolFont && accumulator.AtStart && IsSymbolBullet(text))
            {
                accumulator.MarkSymbolBullet();
                return;
            }

            accumulator.Add(text, style);
            return;
        }

        if (node is not IElement element || element.TagName is "SCRIPT" or "STYLE" or "UL" or "OL")
        {
            return;
        }

        if (element.TagName == "BR")
        {
            accumulator.AddLineBreak();
            return;
        }

        var current = style;
        if (element.TagName is "B" or "STRONG")
        {
            current = current with { Bold = true };
        }

        if (element.TagName is "I" or "EM")
        {
            current = current with { Italic = true };
        }

        if (element.TagName == "SUB")
        {
            current = current with { Script = ScriptKind.Subscript };
        }

        if (element.TagName == "SUP")
        {
            current = current with { Script = ScriptKind.Superscript };
        }

        if (element.TagName is "U" or "INS")
        {
            current = current with { Underline = true };
        }

        if (element.TagName is "S" or "STRIKE" or "DEL")
        {
            current = current with { Strikethrough = true };
        }

        var css = element.GetAttribute("style") ?? string.Empty;
        if (Regex.IsMatch(css, @"font-weight\s*:\s*(?:bold|[6-9]00)", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50)))
        {
            current = current with { Bold = true };
        }

        if (Regex.IsMatch(css, @"font-style\s*:\s*italic", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50)))
        {
            current = current with { Italic = true };
        }

        var verticalAlign = Regex.Match(css, @"vertical-align\s*:\s*(sub|super|baseline)(?![\w-])", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50));
        if (verticalAlign.Success)
        {
            current = current with
            {
                Script = verticalAlign.Groups[1].Value.Equals("sub", StringComparison.OrdinalIgnoreCase)
                    ? ScriptKind.Subscript
                    : verticalAlign.Groups[1].Value.Equals("super", StringComparison.OrdinalIgnoreCase)
                        ? ScriptKind.Superscript
                        : ScriptKind.None
            };
        }

        if (Regex.IsMatch(css, @"text-decoration(?:-line)?\s*:[^;]*underline", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50)))
        {
            current = current with { Underline = true };
        }

        if (Regex.IsMatch(css, @"text-decoration(?:-line)?\s*:[^;]*line-through", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50)))
        {
            current = current with { Strikethrough = true };
        }

        if (ParseColor(css) is { } color)
        {
            current = current with { Color = color };
        }

        if (Regex.IsMatch(css, @"font-family\s*:\s*[^;]*(?:Wingdings|Webdings|Symbol)", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50)))
        {
            current = current with { SymbolFont = true };
        }

        if (element.TagName == "A")
        {
            current = current with { Link = MarkdownRenderer.SafeLink(element.GetAttribute("href")) ?? current.Link };
        }

        if (current.SymbolFont && accumulator.AtStart && IsSymbolBullet(element.TextContent) &&
            !element.ChildNodes.Any(child => child is IElement))
        {
            accumulator.MarkSymbolBullet();
            return;
        }

        foreach (var child in element.ChildNodes)
        {
            ReadInline(child, current, accumulator);
        }
    }

    private static string? ParseColor(string css)
    {
        var match = Regex.Match(css, @"(?:^|;)\s*color\s*:\s*([^;]+)", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50));
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups[1].Value.Trim();
        try
        {
            var color = ColorTranslator.FromHtml(value);
            return color.IsEmpty ? null : $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            return null;
        }
    }

    private static bool IsSymbolBullet(string? text)
    {
        var trimmed = text?.Trim();
        return trimmed is "l" or "m" or "n" or "•" or "◦" or "▪" or "·";
    }

    private static bool HasBlockChildren(IElement element) =>
        element.Children.Any(child => IsBlockElement(child.TagName) || HeadingLevel(child.TagName) is not null);

    private static bool IsBlockElement(string tag) => tag is
        "P" or "DIV" or "SECTION" or "ARTICLE" or "BLOCKQUOTE" or "LI" or "TABLE" or "PRE";

    private static int? HeadingLevel(string tag) => tag switch
    {
        "H1" => 1,
        "H2" => 2,
        "H3" => 3,
        "H4" => 4,
        "H5" => 5,
        "H6" => 6,
        _ => null
    };

    private static Regex WhitespaceRegex() => new(@"\s+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
}
