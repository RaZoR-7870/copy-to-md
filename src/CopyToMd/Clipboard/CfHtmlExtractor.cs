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
                return Encoding.UTF8.GetString(bytes, start, end - start);
            }
        }
        catch (EncoderFallbackException)
        {
            // Fall through to marker-based extraction.
        }

        const string startMarker = "<!--StartFragment-->";
        const string endMarker = "<!--EndFragment-->";
        var markerStart = html.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        var markerEnd = html.IndexOf(endMarker, StringComparison.OrdinalIgnoreCase);
        if (markerStart >= 0 && markerEnd > markerStart)
        {
            markerStart += startMarker.Length;
            return html[markerStart..markerEnd];
        }

        var body = BodyRegex().Match(html);
        return body.Success ? body.Groups[1].Value : html;
    }

    [GeneratedRegex(@"(?im)^\s*StartFragment\s*:\s*(\d+)\s*$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex StartFragmentRegex();

    [GeneratedRegex(@"(?im)^\s*EndFragment\s*:\s*(\d+)\s*$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex EndFragmentRegex();

    [GeneratedRegex(@"<body[^>]*>([\s\S]*?)</body>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)]
    private static partial Regex BodyRegex();
}
