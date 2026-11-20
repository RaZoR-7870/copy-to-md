using System.Text;
using System.Text.RegularExpressions;

namespace CopyToMd.Clipboard;

public sealed partial class CfHtmlExtractor
{
    public static string ExtractFragment(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        // Prefer the fragment comment markers: they delimit the intended content exactly,
        // while byte offsets can drift (e.g. after re-encoding) and cut the last tag short,
        // which drops the final list bullet and introduces stray blank lines.
        const string startMarker = "<!--StartFragment-->";
        const string endMarker = "<!--EndFragment-->";
        var markerStart = html.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        var markerEnd = html.IndexOf(endMarker, StringComparison.OrdinalIgnoreCase);
        if (markerStart >= 0 && markerEnd > markerStart)
        {
            markerStart += startMarker.Length;
            return RepairTruncatedTag(html[markerStart..markerEnd]);
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(html);
            var startMatch = StartFragmentRegex().Match(html);
            var endMatch = EndFragmentRegex().Match(html);
            if (startMatch.Success && endMatch.Success &&
                int.TryParse(startMatch.Groups[1].Value, out var start) &&
                int.TryParse(endMatch.Groups[1].Value, out var end) &&
                start >= 0 && end > start && end <= bytes.Length)
            {
                return RepairTruncatedTag(Encoding.UTF8.GetString(bytes, start, end - start));
            }
        }
        catch (EncoderFallbackException)
        {
            // Fall through to body-based extraction.
        }

        var body = BodyRegex().Match(html);
        return body.Success ? RepairTruncatedTag(body.Groups[1].Value) : html;
    }

    private static string RepairTruncatedTag(string fragment)
    {
        // A fragment may end inside an unfinished tag when offsets are stale; drop the
        // partial tag so the parser does not swallow the preceding content.
        var lastOpen = fragment.LastIndexOf('<');
        if (lastOpen >= 0 && fragment.IndexOf('>', lastOpen) < 0)
        {
            return fragment[..lastOpen];
        }

        return fragment;
    }

    [GeneratedRegex(@"(?im)^\s*StartFragment\s*:\s*(\d+)\s*$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex StartFragmentRegex();

    [GeneratedRegex(@"(?im)^\s*EndFragment\s*:\s*(\d+)\s*$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex EndFragmentRegex();

    [GeneratedRegex(@"<body[^>]*>([\s\S]*?)</body>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)]
    private static partial Regex BodyRegex();
}
