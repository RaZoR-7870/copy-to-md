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

            if (block.Kind == BlockKind.Table && block.Table is not null)
            {
                orderedCounter = 0;
                foreach (var row in block.Table)
                {
                    foreach (var cell in row.Cells)
                    {
                        var source = style.SoftBreakMode == SoftBreakMode.Join ? JoinSoftBreaks(cell.Inlines) : cell.Inlines;
                        var styled = StyleRuns(source, style);
                        cell.Inlines.Clear();
                        cell.Inlines.AddRange(styled);
                    }
                }

                keptBlocks.Add(block);
                continue;
            }

            if (style.SoftBreakMode == SoftBreakMode.Join)
            {
                var joined = JoinSoftBreaks(block.Inlines);
                block.Inlines.Clear();
                block.Inlines.AddRange(joined);
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
                else
                {
                    var shiftedLevel = block.Level + style.ListLevelOffset;
                    if (shiftedLevel < 0)
                    {
                        block.Kind = BlockKind.Paragraph;
                        block.ListKind = ListKind.None;
                        orderedCounter = 0;
                    }
                    else
                    {
                        block.Level = shiftedLevel;
                        if (style.RestartOrderedLists && block.ListKind == ListKind.Ordered)
                        {
                            block.Number = ++orderedCounter;
                        }
                    }
                }
            }
            else
            {
                orderedCounter = 0;
            }

            var runs = StyleRuns(block.Inlines, style);
            block.Inlines.Clear();
            block.Inlines.AddRange(runs);
            keptBlocks.Add(block);
        }

        document.Blocks.Clear();
        document.Blocks.AddRange(keptBlocks);
        return document;
    }

    private static List<InlineRun> StyleRuns(List<InlineRun> source, StyleSettings style)
    {
        var runs = new List<InlineRun>();
        foreach (var run in source)
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

        return runs.Where(run => run.Text.Length > 0).ToList();
    }

    private static string TransformCase(string text, CaseTransform transform) => transform switch
    {
        CaseTransform.Lowercase => text.ToLowerInvariant(),
        CaseTransform.Uppercase => text.ToUpperInvariant(),
        CaseTransform.TitleCase => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLowerInvariant()),
        _ => text
    };

    private static List<InlineRun> JoinSoftBreaks(List<InlineRun> runs)
    {
        var result = new List<InlineRun>();
        var pendingSpace = false;
        foreach (var run in runs)
        {
            var segments = run.Text.Replace('\v', '\n').Split('\n');
            for (var index = 0; index < segments.Length; index++)
            {
                if (index > 0)
                {
                    pendingSpace = true;
                }

                var text = segments[index];
                if (text.Length == 0)
                {
                    continue;
                }

                if (pendingSpace)
                {
                    text = text.TrimStart();
                    if (text.Length == 0)
                    {
                        continue;
                    }

                    if (result.Count > 0)
                    {
                        result[^1] = result[^1] with { Text = result[^1].Text.TrimEnd() + " " };
                    }
                    else
                    {
                        result.Add(run with { Text = " " });
                    }

                    pendingSpace = false;
                }

                if (result.Count > 0 && SameStyle(result[^1], run))
                {
                    result[^1] = result[^1] with { Text = result[^1].Text + text };
                }
                else
                {
                    result.Add(run with { Text = text });
                }
            }
        }

        return result;
    }

    private static bool SameStyle(InlineRun left, InlineRun right) =>
        left.Bold == right.Bold && left.Italic == right.Italic && left.Link == right.Link &&
        left.Script == right.Script && left.Underline == right.Underline &&
        left.Strikethrough == right.Strikethrough && left.Color == right.Color;

    [GeneratedRegex(@"[ \t]+", RegexOptions.CultureInvariant)]
    private static partial Regex HorizontalWhitespaceRegex();
}
