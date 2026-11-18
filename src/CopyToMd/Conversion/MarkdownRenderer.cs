using System.Text;

namespace CopyToMd.Conversion;

public sealed class MarkdownRenderer
{
    public string Render(RichDocument document)
    {
        var blocks = document.Blocks.Where(block => block.Text.Length > 0).ToList();
        var output = new StringBuilder();
        DocumentBlock? previous = null;

        foreach (var block in blocks)
        {
            if (previous is not null)
            {
                output.Append(previous.Kind == BlockKind.ListItem && block.Kind == BlockKind.ListItem
                    ? document.NewLine
                    : document.NewLine + document.NewLine);
            }

            switch (block.Kind)
            {
                case BlockKind.Heading:
                    output.Append('#', Math.Clamp(block.HeadingLevel, 1, 6)).Append(' ');
                    break;
                case BlockKind.ListItem:
                    output.Append(' ', Math.Max(0, block.Level) * 2);
                    output.Append(block.ListKind == ListKind.Ordered
                        ? $"{Math.Max(1, block.Number)}. "
                        : "- ");
                    break;
            }

            output.Append(RenderInlines(block.Inlines, block.Kind == BlockKind.Heading));
            previous = block;
        }

        return output.ToString();
    }

    private static string RenderInlines(IEnumerable<InlineRun> runs, bool suppressBold = false)
    {
        var normalized = InlineScriptNormalizer.Normalize(runs);
        if (suppressBold)
        {
            normalized = normalized.Select(run => run with { Bold = false }).ToList();
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
                        output.Append(RenderRun(run with { Text = prefix }));
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
                        output.Append(RenderRun(remainderSource with { Text = remainderText! }));
                    }

                    continue;
                }

                output.Append(RenderRun(run));
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

    private static string RenderRun(InlineRun run)
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

        if (run.Bold && run.Italic)
        {
            core = $"***{core}***";
        }
        else if (run.Bold)
        {
            core = $"**{core}**";
        }
        else if (run.Italic)
        {
            core = $"*{core}*";
        }

        if (SafeLink(run.Link) is { } link)
        {
            core = $"[{core}]({link})";
        }

        return leading + core + trailing;
    }

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
