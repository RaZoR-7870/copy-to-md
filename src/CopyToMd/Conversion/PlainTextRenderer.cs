using System.Text;

namespace CopyToMd.Conversion;

public sealed class PlainTextRenderer
{
    private readonly StyleSettings style;

    public PlainTextRenderer(StyleSettings? style = null)
    {
        this.style = style ?? new StyleSettings();
        this.style.Normalize();
    }

    public string Render(RichDocument document)
    {
        var output = new StringBuilder();
        for (var index = 0; index < document.Blocks.Count; index++)
        {
            if (index > 0)
            {
                output.Append(document.NewLine);
            }

            var block = document.Blocks[index];
            if (block.Kind == BlockKind.Table && block.Table is { Count: > 0 })
            {
                output.Append(string.Join(document.NewLine, block.Table.Select(row =>
                    string.Join('\t', row.Cells.Select(cell =>
                        string.Concat(InlineScriptNormalizer.Normalize(cell.Inlines).Select(RenderRun)))))));
                continue;
            }

            if (block.Kind == BlockKind.ListItem)
            {
                output.Append(' ', Math.Max(0, block.Level) * 4);
                if (block.ListKind == ListKind.Ordered)
                {
                    output.Append(Math.Max(1, block.Number)).Append(". ");
                }
                else
                {
                    output.Append(style.PlainBulletMarker switch
                    {
                        PlainBulletMarker.Dash => "- ",
                        PlainBulletMarker.Asterisk => "* ",
                        _ => "• "
                    });
                }
            }

            output.Append(string.Concat(InlineScriptNormalizer.Normalize(block.Inlines)
                .Select(RenderRun)));
        }

        return output.ToString();
    }

    private string RenderRun(InlineRun run)
    {
        if (run.Script == ScriptKind.None)
        {
            return run.Text;
        }

        if (style.PlainScriptMode == PlainScriptMode.AsciiFallback)
        {
            var prefix = run.Script == ScriptKind.Superscript ? "^" : "_";
            return run.Text.Length == 1 ? prefix + run.Text : prefix + "(" + run.Text + ")";
        }

        return InlineScriptNormalizer.ToUnicode(run.Text, run.Script);
    }
}
