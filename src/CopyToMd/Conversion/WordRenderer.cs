using System.Text;

namespace CopyToMd.Conversion;

public sealed class WordRenderer
{
    private readonly StyleSettings style;

    public WordRenderer(StyleSettings? style = null)
    {
        this.style = style ?? new StyleSettings();
        this.style.Normalize();
    }

    public string RenderRtf(RichDocument document)
    {
        var colors = new List<string>();
        if (style.KeepColor)
        {
            colors = document.Blocks.SelectMany(BlockRuns)
                .Select(run => run.Color)
                .Where(color => color is not null && TryParseColor(color, out _, out _, out _))
                .Distinct()
                .ToList()!;
        }

        var output = new StringBuilder();
        output.Append(@"{\rtf1\ansi\deff0{\fonttbl{\f0\fnil\fcharset0 Calibri;}}");
        if (colors.Count > 0)
        {
            output.Append(@"{\colortbl ;");
            foreach (var color in colors)
            {
                TryParseColor(color, out var red, out var green, out var blue);
                output.Append(@"\red").Append(red).Append(@"\green").Append(green).Append(@"\blue").Append(blue).Append(';');
            }

            output.Append('}');
        }

        output.Append(@"\viewkind4\uc1");

        foreach (var block in document.Blocks)
        {
            switch (block.Kind)
            {
                case BlockKind.Heading:
                    var level = Math.Clamp(block.HeadingLevel, 1, 6);
                    var fontSize = Math.Max(28, 48 - ((level - 1) * 4));
                    output.Append(@"\pard\sb240\sa120\f0\fs").Append(fontSize).Append(@"\b ");
                    AppendRuns(output, block.Inlines, colors);
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

                    AppendRuns(output, block.Inlines, colors);
                    output.Append(@"\par");
                    break;
                case BlockKind.Table when block.Table is { Count: > 0 }:
                    foreach (var row in block.Table)
                    {
                        output.Append(@"\pard\sa60\f0\fs22 ");
                        for (var column = 0; column < row.Cells.Count; column++)
                        {
                            if (column > 0)
                            {
                                output.Append(@"\tab ");
                            }

                            AppendRuns(output, row.Cells[column].Inlines, colors);
                        }

                        output.Append(@"\par");
                    }

                    break;
                default:
                    output.Append(@"\pard\sa120\f0\fs22 ");
                    AppendRuns(output, block.Inlines, colors);
                    output.Append(@"\par");
                    break;
            }
        }

        output.Append('}');
        return output.ToString();
    }

    private static IEnumerable<InlineRun> BlockRuns(DocumentBlock block) =>
        block.Kind == BlockKind.Table && block.Table is not null
            ? block.Table.SelectMany(row => row.Cells.SelectMany(cell => cell.Inlines))
            : block.Inlines;

    private void AppendRuns(StringBuilder output, IEnumerable<InlineRun> runs, List<string> colors)
    {
        foreach (var rawRun in InlineScriptNormalizer.Normalize(runs))
        {
            var run = rawRun;
            var text = run.Text;
            var script = run.Script;
            if (script != ScriptKind.None && style.WordScriptMode == WordScriptMode.Unicode)
            {
                text = InlineScriptNormalizer.ToUnicode(text, script);
                script = ScriptKind.None;
            }

            var bold = style.KeepBold && run.Bold;
            var italic = style.KeepItalic && run.Italic;
            var underline = style.KeepUnderline && run.Underline;
            var strike = style.KeepStrikethrough && run.Strikethrough;
            var colorIndex = style.KeepColor && run.Color is not null ? colors.IndexOf(run.Color) + 1 : 0;

            if (colorIndex > 0)
            {
                output.Append(@"\cf").Append(colorIndex).Append(' ');
            }

            if (bold)
            {
                output.Append(@"\b ");
            }

            if (italic)
            {
                output.Append(@"\i ");
            }

            if (underline)
            {
                output.Append(@"\ul ");
            }

            if (strike)
            {
                output.Append(@"\strike ");
            }

            if (script == ScriptKind.Superscript)
            {
                output.Append(@"\super ");
            }
            else if (script == ScriptKind.Subscript)
            {
                output.Append(@"\sub ");
            }

            // RichTextBox and Word disagree on some HYPERLINK field forms. Keep the
            // display text rather than risking field instructions appearing in the document.
            AppendRtfText(output, text);

            if (script != ScriptKind.None)
            {
                output.Append(@"\nosupersub ");
            }

            if (strike)
            {
                output.Append(@"\strike0 ");
            }

            if (underline)
            {
                output.Append(@"\ulnone ");
            }

            if (italic)
            {
                output.Append(@"\i0 ");
            }

            if (bold)
            {
                output.Append(@"\b0 ");
            }

            if (colorIndex > 0)
            {
                output.Append(@"\cf0 ");
            }
        }
    }

    private static bool TryParseColor(string? value, out int red, out int green, out int blue)
    {
        red = green = blue = 0;
        if (value is null || value.Length != 7 || value[0] != '#')
        {
            return false;
        }

        try
        {
            red = Convert.ToInt32(value[1..3], 16);
            green = Convert.ToInt32(value[3..5], 16);
            blue = Convert.ToInt32(value[5..7], 16);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            return false;
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
