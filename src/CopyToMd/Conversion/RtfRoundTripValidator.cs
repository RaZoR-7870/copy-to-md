using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CopyToMd.Conversion;

public static class RtfRoundTripValidator
{
    public static bool IsValid(string rtf, string expectedPlainText)
    {
        try
        {
            using var richTextBox = new RichTextBox { Multiline = true, Visible = false };
            richTextBox.CreateControl();
            richTextBox.Rtf = rtf;
            return Normalize(richTextBox.Text) == Normalize(expectedPlainText);
        }
        catch (Exception exception) when (exception is ArgumentException or ExternalException or InvalidOperationException)
        {
            return false;
        }
    }

    private static string Normalize(string value)
    {
        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd('\n');
        normalized = string.Concat(InlineScriptNormalizer.Normalize([new InlineRun(normalized)])
            .Select(run => run.Text));
        return string.Join('\n', normalized.Split('\n').Select(line => line.Replace('\t', ' ').Trim()));
    }
}
