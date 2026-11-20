using System.Text;

namespace CopyToMd.Conversion;

public sealed class MarkdownRenderer
{
    private readonly StyleSettings style;

    public MarkdownRenderer(StyleSettings? style = null)
    {
        this.style = style ?? new StyleSettings();
        this.style.Normalize();
    }

    public string Render(RichDocument document)
    {
        var blocks = document.Blocks.Where(block => block.Text.Length > 0).ToList();
        var output = new StringBuilder();
        DocumentBlock? previous = null;

        foreach (var block in blocks)
        {
            if (previous is not null)
            {
                var tight = style.ParagraphSpacing == ParagraphSpacing.SingleNewline ||
                            (previous.Kind == BlockKind.ListItem && block.Kind == BlockKind.ListItem);
                output.Append(tight ? document.NewLine : document.NewLine + document.NewLine);
            }

            if (block.Kind == BlockKind.Table && block.Table is { Count: > 0 })
            {
                output.Append(RenderTable(block.Table, style));
                previous = block;
                continue;
            }

            switch (block.Kind)
            {
                case BlockKind.Heading:
                    output.Append('#', Math.Clamp(block.HeadingLevel, 1, 6)).Append(' ');
                    break;
                case BlockKind.ListItem:
                    output.Append(' ', Math.Max(0, block.Level) * style.ListIndentSpaces);
                    if (block.ListKind == ListKind.Ordered)
                    {
                        output.Append(Math.Max(1, block.Number)).Append(". ");
                    }
                    else
                    {
                        output.Append(style.BulletMarker switch
                        {
                            BulletMarker.Asterisk => "* ",
                            BulletMarker.Plus => "+ ",
                            _ => "- "
                        });
                    }

                    break;
            }

            output.Append(RenderInlines(block.Inlines, block.Kind == BlockKind.Heading, style));
            previous = block;
        }

        return output.ToString();
    }

    private static string RenderTable(List<TableRow> rows, StyleSettings style)
    {
        var width = rows.Max(row => row.Cells.Count);
        var output = new StringBuilder();
        for (var index = 0; index < rows.Count; index++)
        {
            if (index > 0)
            {
                output.Append('\n');
            }

            output.Append('|');
            for (var column = 0; column < width; column++)
            {
                var cell = column < rows[index].Cells.Count ? RenderTableCell(rows[index].Cells[column], style) : string.Empty;
                output.Append(' ').Append(cell).Append(" |");
            }

            if (index == 0)
            {
                output.Append('\n').Append('|');
                for (var column = 0; column < width; column++)
                {
                    output.Append(" --- |");
                }
            }
        }

        return output.ToString();
    }

    private static string RenderTableCell(TableCell cell, StyleSettings style)
    {
        var text = RenderInlines(cell.Inlines, false, style);
        return text.Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\n', ' ')
            .Replace('\v', ' ')
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Trim();
    }

    private static string RenderInlines(IEnumerable<InlineRun> runs, bool suppressBold, StyleSettings style)
    {
        var normalized = InlineScriptNormalizer.Normalize(runs);
        if (suppressBold)
        {
            normalized = normalized.Select(run => run with { Bold = false }).ToList();
        }

        if (style.MarkdownScriptMode != MarkdownScriptMode.LaTeX)
        {
            var simple = new StringBuilder();
            foreach (var run in normalized)
            {
                simple.Append(RenderRun(run, style));
            }

            return simple.ToString();
        }

        var output = new StringBuilder();
        for (var index = 0; index < normalized.Count;)
        {
            var run = normalized[index];
            if (run.Script == ScriptKind.None)
            {
                if (index + 1 < normalized.Count && normalized[index + 1].Script != ScriptKind.None &&
                    run.Link is null &&
                    TrySplitMathBase(run.Text, out var leading, out var prefix, out var baseToken, out var trailing))
                {
                    output.Append(leading);
                    if (prefix.Length > 0)
                    {
                        output.Append(RenderRun(run with { Text = prefix }, style));
                    }

                    var math = new StringBuilder("$").Append(EscapeLatex(baseToken));
                    index++;
                    string? remainderText = null;
                    InlineRun? remainderSource = null;
                    while (true)
                    {
                        while (index < normalized.Count && normalized[index].Script != ScriptKind.None)
                        {
                            AppendLatexScript(math, normalized[index]);
                            index++;
                        }

                        if (index < normalized.Count && normalized[index].Script == ScriptKind.None &&
                            normalized[index].Link is null &&
                            TrySplitLeadingMathToken(normalized[index].Text, out var nextToken, out var rest))
                        {
                            math.Append(EscapeLatex(nextToken));
                            if (rest.Length > 0)
                            {
                                remainderText = rest;
                                remainderSource = normalized[index];
                                index++;
                                break;
                            }

                            index++;
                            continue;
                        }

                        break;
                    }

                    output.Append(math.Append('$'));
                    output.Append(trailing);
                    if (remainderSource is not null)
                    {
                        output.Append(RenderRun(remainderSource with { Text = remainderText! }, style));
                    }

                    continue;
                }

                output.Append(RenderRun(run, style));
                index++;
                continue;
            }

            var standalone = new StringBuilder("$");
            while (index < normalized.Count && normalized[index].Script != ScriptKind.None)
            {
                AppendLatexScript(standalone, normalized[index]);
                index++;
            }

            output.Append(standalone.Append('$'));
        }

        return output.ToString();
    }

    private static bool TrySplitMathBase(
        string text,
        out string leading,
        out string prefix,
        out string baseToken,
        out string trailing)
    {
        var start = 0;
        while (start < text.Length && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        var end = text.Length;
        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        leading = text[..start];
        trailing = text[end..];
        var core = text[start..end];
        var tokenStart = core.Length;
        while (tokenStart > 0 && char.IsAsciiLetterOrDigit(core[tokenStart - 1]))
        {
            tokenStart--;
        }

        prefix = core[..tokenStart];
        baseToken = core[tokenStart..];
        return baseToken.Length > 0;
    }

    private static bool TrySplitLeadingMathToken(string text, out string token, out string remainder)
    {
        token = string.Empty;
        remainder = text;
        if (text.Length == 0 || !char.IsAsciiLetterOrDigit(text[0]))
        {
            return false;
        }

        var end = 0;
        while (end < text.Length && char.IsAsciiLetterOrDigit(text[end]))
        {
            end++;
        }

        token = text[..end];
        remainder = text[end..];
        return true;
    }

    private static void AppendLatexScript(StringBuilder output, InlineRun run)
    {
        var content = EscapeLatex(run.Text.Trim());
        if (content.Length == 0)
        {
            return;
        }

        output.Append(run.Script == ScriptKind.Superscript ? "^{" : "_{")
            .Append(content)
            .Append('}');
    }

    private static string EscapeLatex(string text)
    {
        var output = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (character is '\\' or '{' or '}' or '$' or '&' or '#' or '%' or '_' or '^')
            {
                output.Append('\\');
            }

            output.Append(character);
        }

        return output.ToString();
    }

    private static string RenderRun(InlineRun run, StyleSettings style)
    {
        var escaped = Escape(run.Text);
        var leadingLength = escaped.Length - escaped.TrimStart().Length;
        var trailingLength = escaped.Length - escaped.TrimEnd().Length;
        var coreLength = Math.Max(0, escaped.Length - leadingLength - trailingLength);
        var leading = escaped[..leadingLength];
        var core = escaped.Substring(leadingLength, coreLength);
        var trailing = coreLength > 0 ? escaped[(leadingLength + coreLength)..] : string.Empty;

        if (core.Length == 0)
        {
            return escaped;
        }

        if (run.Script != ScriptKind.None && style.MarkdownScriptMode != MarkdownScriptMode.LaTeX)
        {
            core = style.MarkdownScriptMode == MarkdownScriptMode.Html
                ? $"<{(run.Script == ScriptKind.Superscript ? "sup" : "sub")}>{core}</{(run.Script == ScriptKind.Superscript ? "sup" : "sub")}>"
                : Escape(InlineScriptNormalizer.ToUnicode(run.Text, run.Script));
        }

        var bold = style.KeepBold && run.Bold;
        var italic = style.KeepItalic && run.Italic;
        if (bold && italic)
        {
            core = $"***{core}***";
        }
        else if (bold)
        {
            core = $"**{core}**";
        }
        else if (italic)
        {
            core = $"*{core}*";
        }

        if (style.KeepStrikethrough && run.Strikethrough)
        {
            core = $"~~{core}~~";
        }

        if (style.KeepUnderline && run.Underline && style.MarkdownAllowHtml)
        {
            core = $"<u>{core}</u>";
        }

        if (style.KeepColor && run.Color is { } color && style.MarkdownAllowHtml && IsHexColor(color))
        {
            core = $"<span style=\"color:{color}\">{core}</span>";
        }

        if (style.LinkMode == LinkMode.Keep && SafeLink(run.Link) is { } link)
        {
            core = $"[{core}]({link})";
        }

        return leading + core + trailing;
    }

    private static bool IsHexColor(string value) =>
        value.Length == 7 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);

    internal static string Escape(string text)
    {
        var output = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (character is '\\' or '`' or '*' or '_' or '[' or ']')
            {
                output.Append('\\');
            }

            output.Append(character);
        }

        return output.ToString();
    }

    internal static string? SafeLink(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeMailto))
        {
            return null;
        }

        return uri.AbsoluteUri.Replace(" ", "%20", StringComparison.Ordinal)
            .Replace("(", "%28", StringComparison.Ordinal)
            .Replace(")", "%29", StringComparison.Ordinal);
    }
}
