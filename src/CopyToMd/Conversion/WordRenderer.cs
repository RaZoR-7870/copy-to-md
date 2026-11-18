using System.Text;

namespace CopyToMd.Conversion;

public sealed class WordRenderer
{
    public string RenderRtf(RichDocument document)
    {
        var output = new StringBuilder();
        output.Append(@"{\rtf1\ansi\deff0{\fonttbl{\f0\fnil\fcharset0 Calibri;}}\viewkind4\uc1");

        foreach (var block in document.Blocks)
        {
            switch (block.Kind)
            {
                case BlockKind.Heading:
                    var level = Math.Clamp(block.HeadingLevel, 1, 6);
                    var fontSize = Math.Max(28, 48 - ((level - 1) * 4));
                    output.Append(@"\pard\sb240\sa120\f0\fs").Append(fontSize).Append(@"\b ");
                    AppendRuns(output, block.Inlines);
                    output.Append(@"\b0\par");
                    break;
                case BlockKind.ListItem:
                    var indent = 240 * (Math.Max(0, block.Level) + 1);
                    output.Append(@"\pard\sa60\f0\fs22\li").Append(indent).Append(@"\fi-240 ");
                    if (block.ListKind == ListKind.Ordered)
                    {
                        output.Append(Math.Max(1, block.Number)).Append(@".\tab ");
                    }
                    else
                    {
                        AppendRtfText(output, "•");
                        output.Append(@"\tab ");
                    }

                    AppendRuns(output, block.Inlines);
                    output.Append(@"\par");
                    break;
                default:
                    output.Append(@"\pard\sa120\f0\fs22 ");
                    AppendRuns(output, block.Inlines);
                    output.Append(@"\par");
                    break;
                }
        }

        output.Append('}');
        return output.ToString();
    }

    private static void AppendRuns(StringBuilder output, IEnumerable<InlineRun> runs)
    {
        foreach (var run in InlineScriptNormalizer.Normalize(runs))
        {
            if (run.Bold)
            {
                output.Append(@"\b ");
            }

            if (run.Italic)
            {
                output.Append(@"\i ");
            }

            if (run.Script == ScriptKind.Superscript)
            {
                output.Append(@"\super ");
            }
            else if (run.Script == ScriptKind.Subscript)
            {
                output.Append(@"\sub ");
            }

            // RichTextBox and Word disagree on some HYPERLINK field forms. Keep the
            // display text rather than risking field instructions appearing in the document.
            AppendRtfText(output, run.Text);

            if (run.Script != ScriptKind.None)
            {
                output.Append(@"\nosupersub ");
            }

            if (run.Italic)
            {
                output.Append(@"\i0 ");
            }

            if (run.Bold)
            {
                output.Append(@"\b0 ");
            }
        }
    }

    internal static void AppendRtfText(StringBuilder output, string text)
    {
        foreach (var character in text)
        {
            switch (character)
            {
                case '\\':
                    output.Append(@"\\");
                    break;
                case '{':
                    output.Append(@"\{");
                    break;
                case '}':
                    output.Append(@"\}");
                    break;
                case '\t':
                    output.Append(@"\tab ");
                    break;
                case '\r':
                    break;
                case '\n':
                    output.Append(@"\line ");
                    break;
                default:
                    if (character < 128)
                    {
                        output.Append(character);
                    }
                    else
                    {
                        output.Append(@"\u").Append(unchecked((short)character)).Append('?');
                    }

                    break;
            }
        }
    }
}
