namespace CopyToMd.Conversion;

public enum DocumentSource
{
    PlainText,
    Html,
    Rtf
}

public enum BlockKind
{
    Paragraph,
    Heading,
    ListItem,
    Table
}

public enum ListKind
{
    None,
    Bullet,
    Ordered
}

public sealed record InlineRun(
    string Text,
    bool Bold = false,
    bool Italic = false,
    string? Link = null,
    ScriptKind Script = ScriptKind.None,
    bool Underline = false,
    bool Strikethrough = false,
    string? Color = null);

public sealed class TableCell
{
    public List<InlineRun> Inlines { get; } = [];
    public string Text => string.Concat(Inlines.Select(run => run.Text));
}

public sealed class TableRow
{
    public List<TableCell> Cells { get; } = [];
}

public sealed class DocumentBlock
{
    public BlockKind Kind { get; set; } = BlockKind.Paragraph;
    public int HeadingLevel { get; set; } = 1;
    public ListKind ListKind { get; set; } = ListKind.None;
    public int Level { get; set; }
    public int Number { get; set; } = 1;
    public double? IndentHint { get; set; }
    public double? FontSizeHint { get; set; }
    public List<TableRow>? Table { get; set; }
    public List<InlineRun> Inlines { get; } = [];

    public string Text => Kind == BlockKind.Table && Table is not null
        ? string.Join("\n", Table.Select(row => string.Join("\t", row.Cells.Select(cell => cell.Text))))
        : string.Concat(Inlines.Select(run => run.Text));

    public static DocumentBlock FromText(string text) =>
        Create(BlockKind.Paragraph, text);

    public static DocumentBlock Create(BlockKind kind, string text, bool bold = false, bool italic = false)
    {
        var block = new DocumentBlock { Kind = kind };
        if (text.Length > 0)
        {
            block.Inlines.Add(new InlineRun(text, bold, italic));
        }

        return block;
    }
}

public sealed class RichDocument
{
    public List<DocumentBlock> Blocks { get; } = [];
    public List<string> Warnings { get; } = [];
    public DocumentSource Source { get; set; } = DocumentSource.PlainText;
    public bool UsedFallback { get; set; }
    public string NewLine { get; set; } = "\n";

    public bool HasSemanticFormatting => Blocks.Any(block =>
        block.Kind is BlockKind.Heading or BlockKind.ListItem or BlockKind.Table ||
        block.Inlines.Any(run => run.Bold || run.Italic || run.Link is not null || run.Script != ScriptKind.None ||
                                 run.Underline || run.Strikethrough || run.Color is not null));

    public string PlainText => string.Join(Environment.NewLine, Blocks.Select(block => block.Text));
}
