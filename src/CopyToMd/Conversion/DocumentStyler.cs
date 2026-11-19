using System.Globalization;
using System.Text.RegularExpressions;

namespace CopyToMd.Conversion;

public static partial class DocumentStyler
{
    public static RichDocument Apply(RichDocument document, StyleSettings style)
    {
        style.Normalize();
        var orderedCounter = 0;
        var keptBlocks = new List<DocumentBlock>();

        foreach (var block in document.Blocks)
        {
            if (style.RemoveEmptyBlocks && string.IsNullOrWhiteSpace(block.Text))
            {
                continue;
            }

            if (block.Kind == BlockKind.Heading)
            {
                if (style.HeadingMode == HeadingMode.ToParagraph)
                {
                    block.Kind = BlockKind.Paragraph;
                }
                else
                {
                    block.HeadingLevel = Math.Clamp(block.HeadingLevel + style.HeadingLevelOffset, 1, style.MaxHeadingLevel);
                }
            }

            if (block.Kind == BlockKind.ListItem)
            {
                if (style.ListMode == ListMode.ToParagraph)
                {
                    block.Kind = BlockKind.Paragraph;
                    block.ListKind = ListKind.None;
                    orderedCounter = 0;
                }
                else if (style.RestartOrderedLists && block.ListKind == ListKind.Ordered)
                {
                    block.Number = ++orderedCounter;
                }
            }
            else
            {
                orderedCounter = 0;
            }

            var runs = new List<InlineRun>();
            foreach (var run in block.Inlines)
            {
                var text = TransformCase(run.Text, style.CaseTransform);
                if (style.CollapseSpaces)
                {
                    text = HorizontalWhitespaceRegex().Replace(text, " ");
                }

                runs.Add(run with
                {
                    Text = text,
                    Bold = style.KeepBold && run.Bold,
                    Italic = style.KeepItalic && run.Italic,
                    Underline = style.KeepUnderline && run.Underline,
                    Strikethrough = style.KeepStrikethrough && run.Strikethrough,
                    Color = style.KeepColor ? run.Color : null,
                    Link = style.LinkMode == LinkMode.Keep ? run.Link : null
                });
            }

            if (style.TrimWhitespace && runs.Count > 0)
            {
                runs[0] = runs[0] with { Text = runs[0].Text.TrimStart() };
                runs[^1] = runs[^1] with { Text = runs[^1].Text.TrimEnd() };
            }

            block.Inlines.Clear();
            block.Inlines.AddRange(runs.Where(run => run.Text.Length > 0));
            keptBlocks.Add(block);
        }

        document.Blocks.Clear();
        document.Blocks.AddRange(keptBlocks);
        return document;
    }

    private static string TransformCase(string text, CaseTransform transform) => transform switch
    {
        CaseTransform.Lowercase => text.ToLowerInvariant(),
        CaseTransform.Uppercase => text.ToUpperInvariant(),
        CaseTransform.TitleCase => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLowerInvariant()),
        _ => text
    };

    [GeneratedRegex(@"[ \t]+", RegexOptions.CultureInvariant)]
    private static partial Regex HorizontalWhitespaceRegex();
}
