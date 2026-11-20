using System.Drawing;
using System.Globalization;
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
        bool BulletMarker = false,
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
            NormalizeFlatListLevels(result);
            NormalizeHeadings(result);
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

            if (tag == "TABLE")
            {
                FlushInlineBuffer();
                ProcessTable(element, document);
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
                        var block = AddBlock(document, BlockKind.Paragraph, runs, accumulator.FoundSymbolBullet);
                        block.IndentHint = ParseMarginLeft(element);
                        block.FontSizeHint = ParseMaxFontSize(element);
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
            block.FontSizeHint = ParseMaxFontSize(item);
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

    private static void NormalizeFlatListLevels(RichDocument document)
    {
        // PowerPoint emits flat <div>/<p> bullets distinguished only by margin-left,
        // not nested <ul>. Recover nesting from the distinct indent values.
        var levels = document.Blocks
            .Where(block => block.Kind == BlockKind.ListItem && block.IndentHint.HasValue)
            .Select(block => Math.Round(block.IndentHint!.Value, 3))
            .Distinct()
            .OrderBy(indent => indent)
            .ToList();
        if (levels.Count < 2)
        {
            return;
        }

        foreach (var block in document.Blocks)
        {
            if (block.Kind == BlockKind.ListItem && block.IndentHint.HasValue)
            {
                block.Level = Math.Max(0, levels.IndexOf(Math.Round(block.IndentHint.Value, 3)));
            }
        }
    }

    private static double? ParseMarginLeft(IElement element)
    {
        var css = element.GetAttribute("style");
        if (string.IsNullOrEmpty(css))
        {
            return null;
        }

        var match = MarginLeftRegex().Match(css);
        if (!match.Success ||
            !double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        var inches = match.Groups[2].Value.ToLowerInvariant() switch
        {
            "pt" => value / 72.0,
            "px" => value / 96.0,
            "cm" => value / 2.54,
            "mm" => value / 25.4,
            _ => value
        };
        return Math.Max(0, inches);
    }

    private static Regex MarginLeftRegex() =>
        new(@"margin-left\s*:\s*(-?[0-9.]+)\s*(in|pt|px|cm|mm)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));

    private static void ProcessTable(IElement table, RichDocument document)
    {
        var rows = new List<TableRow>();
        foreach (var tr in table.QuerySelectorAll("tr"))
        {
            // Ignore rows belonging to a nested table.
            var nested = false;
            for (var parent = tr.ParentElement; parent is not null && !ReferenceEquals(parent, table); parent = parent.ParentElement)
            {
                if (parent.TagName == "TABLE")
                {
                    nested = true;
                    break;
                }
            }

            if (nested)
            {
                continue;
            }

            var row = new TableRow();
            foreach (var cell in tr.Children.Where(child => child.TagName is "TD" or "TH"))
            {
                var tableCell = new TableCell();
                tableCell.Inlines.AddRange(ReadBlockInline(cell).Finish());
                row.Cells.Add(tableCell);
            }

            if (row.Cells.Count > 0)
            {
                rows.Add(row);
            }
        }

        if (rows.Count == 0)
        {
            return;
        }

        var width = rows.Max(row => row.Cells.Count);
        foreach (var row in rows)
        {
            while (row.Cells.Count < width)
            {
                row.Cells.Add(new TableCell());
            }
        }

        document.Blocks.Add(new DocumentBlock { Kind = BlockKind.Table, Table = rows });
    }

    private static double? ParseMaxFontSize(IElement element)
    {
        double? max = null;
        foreach (Match match in FontSizeRegex().Matches(element.InnerHtml))
        {
            if (double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
                value > 0 && (max is null || value > max))
            {
                max = value;
            }
        }

        return max;
    }

    private static Regex FontSizeRegex() =>
        new(@"font-size\s*:\s*([0-9.]+)\s*pt", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));

    private static void NormalizeHeadings(RichDocument document)
    {
        // PPT section headings are larger-font paragraphs. Detect them relative to the
        // body font size (estimated from paragraphs AND bullets), conservatively
        // excluding long sentences.
        var sized = document.Blocks
            .Where(block => block.Kind is BlockKind.Paragraph or BlockKind.ListItem && block.FontSizeHint.HasValue)
            .ToList();
        if (sized.Count < 2)
        {
            return;
        }

        var bodySize = sized
            .GroupBy(block => Math.Round(block.FontSizeHint!.Value, 1))
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .First().Key;

        var candidates = sized
            .Where(block => block.Kind == BlockKind.Paragraph &&
                            block.FontSizeHint!.Value > bodySize + 0.5 && IsHeadingText(block.Text))
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var sizes = candidates
            .Select(block => Math.Round(block.FontSizeHint!.Value, 1))
            .Distinct()
            .OrderByDescending(size => size)
            .ToList();
        foreach (var block in candidates)
        {
            block.Kind = BlockKind.Heading;
            block.HeadingLevel = Math.Clamp(sizes.IndexOf(Math.Round(block.FontSizeHint!.Value, 1)) + 1, 1, 6);
        }
    }

    private static bool IsHeadingText(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length is > 0 and <= 80 &&
               !trimmed.Contains('\n') &&
               trimmed[^1] is not ('.' or '。' or '!' or '！' or '?' or '？' or ';' or '；' or ',' or '，');
    }

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
            if (accumulator.AtStart && IsBulletGlyph(style, text))
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

        if (Regex.IsMatch(css, @"mso-special-format\s*:\s*bullet", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50)))
        {
            current = current with { BulletMarker = true };
        }

        if (element.TagName == "A")
        {
            current = current with { Link = MarkdownRenderer.SafeLink(element.GetAttribute("href")) ?? current.Link };
        }

        if (accumulator.AtStart && IsBulletGlyph(current, element.TextContent) &&
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

    private static bool IsBulletGlyph(InlineStyle style, string? text)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        // PPT marks real bullets with mso-special-format:bullet regardless of the glyph
        // character (Wingdings l/n, literal "-", "§", etc.).
        if (style.BulletMarker && trimmed.Length <= 3)
        {
            return true;
        }

        return style.SymbolFont && IsSymbolBullet(text);
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
