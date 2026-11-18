using System.Text;

namespace CopyToMd.Conversion;

public sealed class PlainTextRenderer
{
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
            if (block.Kind == BlockKind.ListItem)
            {
                output.Append(' ', Math.Max(0, block.Level) * 4);
                output.Append(block.ListKind == ListKind.Ordered
                    ? $"{Math.Max(1, block.Number)}. "
                    : "• ");
            }

            output.Append(string.Concat(InlineScriptNormalizer.Normalize(block.Inlines)
                .Select(run => InlineScriptNormalizer.ToUnicode(run.Text, run.Script))));
        }

        return output.ToString();
    }
}
