using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CopyToMd.Conversion;

public sealed partial class RtfImporter
{
    private sealed class ParagraphData
    {
        public required string Text { get; init; }
        public List<InlineRun> Runs { get; } = [];
        public bool IsList { get; set; }
        public bool IsOrdered { get; set; }
        public int Number { get; set; } = 1;
        public int Indent { get; init; }
        public float FontSize { get; set; }
    }

    public bool TryImport(string? rtf, string? unicodeText, out RichDocument? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(rtf) || rtf.Length > 5_000_000)
        {
            return false;
        }

        try
        {
            using var richTextBox = new RichTextBox
            {
                Multiline = true,
                WordWrap = false,
                DetectUrls = false,
                Visible = false
            };
            richTextBox.CreateControl();
            richTextBox.Rtf = rtf;
            var richText = richTextBox.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            if (richText.Length > 500_000 || !HasPlausibleTextCoverage(unicodeText, richText))
            {
                return false;
            }

            var paragraphs = ReadParagraphs(richTextBox, richText);
            if (paragraphs.Count == 0)
            {
                return false;
            }

            var result = new RichDocument { Source = DocumentSource.Rtf };
            var listIndents = paragraphs.Where(paragraph => paragraph.IsList)
                .Select(paragraph => paragraph.Indent)
                .Distinct()
                .OrderBy(indent => indent)
                .ToList();
            var bodySize = paragraphs.Where(paragraph => paragraph.Text.Trim().Length > 0)
                .Select(paragraph => paragraph.FontSize)
                .Where(size => size > 0)
                .GroupBy(size => Math.Round(size, 1))
                .OrderByDescending(group => group.Count())
                .Select(group => (float)group.Key)
                .FirstOrDefault(11f);

            foreach (var paragraph in paragraphs)
            {
                var text = paragraph.Text.Trim();
                var block = new DocumentBlock();
                block.Inlines.AddRange(paragraph.Runs);

                if (paragraph.IsList)
                {
                    block.Kind = BlockKind.ListItem;
                    block.ListKind = paragraph.IsOrdered ? ListKind.Ordered : ListKind.Bullet;
                    block.Number = paragraph.Number;
                    block.Level = Math.Max(0, listIndents.IndexOf(paragraph.Indent));
                }
                else if (text.Length is > 0 and <= 120 && paragraph.FontSize >= Math.Max(14, bodySize * 1.25f))
                {
                    block.Kind = BlockKind.Heading;
                    block.HeadingLevel = paragraph.FontSize >= 24 ? 1 : paragraph.FontSize >= 20 ? 2 : 3;
                }

                result.Blocks.Add(block);
            }

            result.Warnings.Add("RTF 中的超链接字段未完整解析时仅保留显示文字。");
            document = result;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or ExternalException or InvalidOperationException)
        {
            return false;
        }
    }

    private static List<ParagraphData> ReadParagraphs(RichTextBox richTextBox, string text)
    {
        var paragraphs = new List<ParagraphData>();
        var lines = text.Split('\n');
        var lineStart = 0;

        foreach (var line in lines)
        {
            richTextBox.Select(lineStart, 0);
            var paragraph = new ParagraphData
            {
                Text = line,
                IsList = richTextBox.SelectionBullet,
                Indent = Math.Max(0, richTextBox.SelectionIndent),
                FontSize = richTextBox.SelectionFont?.Size ?? richTextBox.Font.Size
            };

            var contentStart = 0;
            if (TryGetSymbolBulletLength(richTextBox, line, lineStart, out var symbolLength))
            {
                paragraph.IsList = true;
                contentStart = symbolLength;
            }
            else
            {
                var standardBullet = StandardBulletRegex().Match(line);
                if (standardBullet.Success)
                {
                    paragraph.IsList = true;
                    contentStart = standardBullet.Length;
                }
                else
                {
                    var ordered = OrderedListRegex().Match(line);
                    if (ordered.Success)
                    {
                        paragraph.IsList = true;
                        paragraph.IsOrdered = true;
                        paragraph.Number = int.Parse(ordered.Groups["number"].Value);
                        contentStart = ordered.Length;
                    }
                }
            }

            var hasScript = false;
            if (line.Length > contentStart)
            {
                richTextBox.Select(lineStart + contentStart, line.Length - contentStart);
                hasScript = ScriptCommandRegex().IsMatch(richTextBox.SelectedRtf);
            }

            InlineStyle? currentStyle = null;
            var runStart = contentStart;
            for (var index = contentStart; index < line.Length; index++)
            {
                richTextBox.Select(lineStart + index, 1);
                var font = richTextBox.SelectionFont;
                var script = hasScript ? ReadScriptKind(richTextBox.SelectedRtf) : ScriptKind.None;
                var color = richTextBox.SelectionColor;
                var colorHex = color.IsEmpty || color.ToArgb() == Color.Black.ToArgb()
                    ? null
                    : $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                var style = new InlineStyle(
                    font?.Bold == true,
                    font?.Italic == true,
                    font?.Size ?? paragraph.FontSize,
                    script,
                    font?.Underline == true,
                    font?.Strikeout == true,
                    colorHex);

                paragraph.FontSize = Math.Max(paragraph.FontSize, style.Size);
                if (currentStyle != style && index > runStart)
                {
                    AddRun(paragraph, line[runStart..index], currentStyle!);
                    runStart = index;
                }

                currentStyle = style;
            }

            if (currentStyle is not null && runStart < line.Length)
            {
                AddRun(paragraph, line[runStart..], currentStyle);
            }

            if (paragraph.Runs.Count == 0 && contentStart == 0 && line.Length > 0)
            {
                paragraph.Runs.Add(new InlineRun(line));
            }

            paragraphs.Add(paragraph);
            lineStart += line.Length + 1;
        }

        return paragraphs;
    }

    private static bool TryGetSymbolBulletLength(RichTextBox richTextBox, string line, int lineStart, out int length)
    {
        length = 0;
        var marker = 0;
        while (marker < line.Length && line[marker] is ' ' or '\t')
        {
            marker++;
        }

        if (marker >= line.Length || line[marker] is not ('l' or 'm' or 'n') ||
            marker + 1 >= line.Length || line[marker + 1] is not (' ' or '\t'))
        {
            return false;
        }

        richTextBox.Select(lineStart + marker, 1);
        var fontName = richTextBox.SelectionFont?.Name;
        if (fontName is null ||
            (!fontName.Contains("Wingdings", StringComparison.OrdinalIgnoreCase) &&
             !fontName.Contains("Webdings", StringComparison.OrdinalIgnoreCase) &&
             !fontName.Contains("Symbol", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        length = marker + 1;
        while (length < line.Length && line[length] is ' ' or '\t')
        {
            length++;
        }

        return true;
    }

    private static ScriptKind ReadScriptKind(string selectedRtf)
    {
        var match = ScriptCommandRegex().Match(selectedRtf);
        if (!match.Success)
        {
            return ScriptKind.None;
        }

        return match.Value == @"\super" ? ScriptKind.Superscript : ScriptKind.Subscript;
    }

    private static void AddRun(ParagraphData paragraph, string text, InlineStyle style)
    {
        text = text.Replace('\v', '\n');
        if (text.Length > 0)
        {
            paragraph.Runs.Add(new InlineRun(text, style.Bold, style.Italic, null, style.Script,
                style.Underline, style.Strikethrough, style.Color));
        }
    }

    private static bool HasPlausibleTextCoverage(string? unicodeText, string richText)
    {
        if (string.IsNullOrWhiteSpace(unicodeText))
        {
            return richText.Trim().Length > 0;
        }

        return CanonicalizeForCoverage(unicodeText) == CanonicalizeForCoverage(richText);
    }

    private static string CanonicalizeForCoverage(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Replace('\v', '\n');
        var withoutListMarkers = ListPrefixRegex().Replace(normalized, string.Empty);
        return CoverageRegex().Replace(withoutListMarkers, string.Empty);
    }

    private sealed record InlineStyle(
        bool Bold,
        bool Italic,
        float Size,
        ScriptKind Script = ScriptKind.None,
        bool Underline = false,
        bool Strikethrough = false,
        string? Color = null);

    [GeneratedRegex(@"^[ \t]*[•◦▪·∙](?:\t| +)", RegexOptions.CultureInvariant)]
    private static partial Regex StandardBulletRegex();

    [GeneratedRegex(@"^[ \t]*(?<number>\d{1,9})[.)](?:\t| +)", RegexOptions.CultureInvariant)]
    private static partial Regex OrderedListRegex();

    [GeneratedRegex(@"(?m)^[ \t]*(?:[lmn](?=[ \t])|[•◦▪·∙]|[-*+](?=[ \t])|\d{1,9}[.)](?=[ \t]))[ \t]*", RegexOptions.CultureInvariant)]
    private static partial Regex ListPrefixRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex CoverageRegex();

    [GeneratedRegex(@"\\(?:super|sub)(?![A-Za-z])", RegexOptions.CultureInvariant)]
    private static partial Regex ScriptCommandRegex();
}
