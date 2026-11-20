using System.ComponentModel;

namespace CopyToMd.Conversion;

public enum CaseTransform
{
    Preserve,
    Lowercase,
    Uppercase,
    TitleCase
}

public enum HeadingMode
{
    Keep,
    ToParagraph
}

public enum ListMode
{
    Keep,
    ToParagraph
}

public enum LinkMode
{
    Keep,
    TextOnly
}

public enum BulletMarker
{
    Dash,
    Asterisk,
    Plus
}

public enum PlainBulletMarker
{
    Bullet,
    Dash,
    Asterisk
}

public enum ParagraphSpacing
{
    BlankLine,
    SingleNewline
}

public enum SoftBreakMode
{
    Join,
    Keep
}

public enum MarkdownScriptMode
{
    LaTeX,
    Unicode,
    Html,
    Plain
}

public enum WordScriptMode
{
    Real,
    Unicode
}

public enum PlainScriptMode
{
    Unicode,
    AsciiFallback
}

public enum SuspectBulletCleaning
{
    Conservative,
    Aggressive,
    Off
}

public sealed class StyleSettings
{
    [Category("1. 字符格式")]
    [DisplayName("保留粗体")]
    [Description("关闭后移除粗体格式，文本内容保留。")]
    public bool KeepBold { get; set; } = true;

    [Category("1. 字符格式")]
    [DisplayName("保留斜体")]
    [Description("关闭后移除斜体格式。")]
    public bool KeepItalic { get; set; } = true;

    [Category("1. 字符格式")]
    [DisplayName("保留下划线")]
    [Description("Markdown 使用 <u>，Word 使用真实下划线；纯文本忽略。")]
    public bool KeepUnderline { get; set; }

    [Category("1. 字符格式")]
    [DisplayName("保留删除线")]
    [Description("Markdown 使用 ~~，Word 使用真实删除线；纯文本忽略。")]
    public bool KeepStrikethrough { get; set; }

    [Category("1. 字符格式")]
    [DisplayName("保留颜色")]
    [Description("Word 使用真实文字颜色；Markdown 可用 HTML span；纯文本忽略。")]
    public bool KeepColor { get; set; }

    [Category("1. 字符格式")]
    [DisplayName("大小写转换")]
    [Description("对文本内容做大小写转换；Preserve 表示不改动。")]
    public CaseTransform CaseTransform { get; set; } = CaseTransform.Preserve;

    [Category("1. 字符格式")]
    [DisplayName("链接处理")]
    [Description("Keep 保留链接；TextOnly 只保留显示文字。Word 当前始终只保留显示文字。")]
    public LinkMode LinkMode { get; set; } = LinkMode.Keep;

    [Category("2. 段落与列表")]
    [DisplayName("标题处理")]
    [Description("Keep 保留标题；ToParagraph 转为普通段落。")]
    public HeadingMode HeadingMode { get; set; } = HeadingMode.Keep;

    [Category("2. 段落与列表")]
    [DisplayName("标题级别偏移")]
    [Description("正数降级，负数升级；最终限制在 1-6。")]
    public int HeadingLevelOffset { get; set; }

    [Category("2. 段落与列表")]
    [DisplayName("最大标题级别")]
    [Description("超过该级别的标题会被压到该级别。")]
    public int MaxHeadingLevel { get; set; } = 6;

    [Category("2. 段落与列表")]
    [DisplayName("列表处理")]
    [Description("Keep 保留列表；ToParagraph 转为普通段落。")]
    public ListMode ListMode { get; set; } = ListMode.Keep;

    [Category("2. 段落与列表")]
    [DisplayName("列表级别位移")]
    [Description("正数加深缩进；负数提升级别，最高级列表可转为纯文本（来源最高为 2 级时 -2 生效）。负数过小时自动收敛，最多一个级别变为纯文本。")]
    public int ListLevelOffset { get; set; }

    [Category("2. 段落与列表")]
    [DisplayName("Markdown 无序符号")]
    [Description("Markdown 无序列表使用的符号。")]
    public BulletMarker BulletMarker { get; set; } = BulletMarker.Dash;

    [Category("2. 段落与列表")]
    [DisplayName("纯文本无序符号")]
    [Description("纯文本无序列表使用的符号。")]
    public PlainBulletMarker PlainBulletMarker { get; set; } = PlainBulletMarker.Bullet;

    [Category("2. 段落与列表")]
    [DisplayName("列表缩进空格数")]
    [Description("Markdown 每级列表缩进空格数；纯文本固定每级 4 空格。")]
    public int ListIndentSpaces { get; set; } = 2;

    [Category("2. 段落与列表")]
    [DisplayName("有序列表重新编号")]
    [Description("开启后忽略来源编号，按 1、2、3 重新编号。")]
    public bool RestartOrderedLists { get; set; }

    [Category("2. 段落与列表")]
    [DisplayName("段落间距")]
    [Description("Markdown 段落之间使用空行或单换行。")]
    public ParagraphSpacing ParagraphSpacing { get; set; } = ParagraphSpacing.BlankLine;

    [Category("2. 段落与列表")]
    [DisplayName("移除空段落")]
    [Description("开启后输出中不包含空段落。")]
    public bool RemoveEmptyBlocks { get; set; }

    [Category("2. 段落与列表")]
    [DisplayName("软换行处理")]
    [Description("Join 把段落内的自动折行/软换行（PPT 的 br、RTF 的 \\v）合并为一个空格，恢复完整句子；Keep 保留换行。")]
    public SoftBreakMode SoftBreakMode { get; set; } = SoftBreakMode.Join;

    [Category("3. 上下标")]
    [DisplayName("Markdown 上下标")]
    [Description("LaTeX 输出 $x^{2}$；Unicode 输出 x²；Html 输出 <sup>；Plain 尽量 Unicode。")]
    public MarkdownScriptMode MarkdownScriptMode { get; set; } = MarkdownScriptMode.LaTeX;

    [Category("3. 上下标")]
    [DisplayName("Word 上下标")]
    [Description("Real 使用真实 RTF 上下标；Unicode 使用 Unicode 字符。")]
    public WordScriptMode WordScriptMode { get; set; } = WordScriptMode.Real;

    [Category("3. 上下标")]
    [DisplayName("纯文本上下标")]
    [Description("Unicode 优先 Unicode 字符；AsciiFallback 对无法映射字符使用 ^()/_()。")]
    public PlainScriptMode PlainScriptMode { get; set; } = PlainScriptMode.Unicode;

    [Category("4. 空白与清理")]
    [DisplayName("修剪首尾空白")]
    [Description("修剪每个段落首尾空白。")]
    public bool TrimWhitespace { get; set; }

    [Category("4. 空白与清理")]
    [DisplayName("合并连续空格")]
    [Description("把连续空格/Tab 合并为一个空格，不影响换行。")]
    public bool CollapseSpaces { get; set; }

    [Category("4. 空白与清理")]
    [DisplayName("伪项目符号清理")]
    [Description("Conservative 至少连续多行才清理；Aggressive 单行也清理；Off 不清理 l/m/n。")]
    public SuspectBulletCleaning SuspectBulletCleaning { get; set; } = SuspectBulletCleaning.Conservative;

    [Category("4. 空白与清理")]
    [DisplayName("最少可疑行数")]
    [Description("Conservative 模式下连续可疑行达到该数量才认定为列表。")]
    public int MinSuspectLines { get; set; } = 3;

    [Category("5. 高级输出")]
    [DisplayName("Markdown 允许内联 HTML")]
    [Description("允许用 <u>、<span> 等表达下划线/颜色；关闭后这些格式在 Markdown 中移除。")]
    public bool MarkdownAllowHtml { get; set; } = true;

    public StyleSettings Clone() => (StyleSettings)MemberwiseClone();

    public void Normalize()
    {
        HeadingLevelOffset = Math.Clamp(HeadingLevelOffset, -5, 5);
        MaxHeadingLevel = Math.Clamp(MaxHeadingLevel, 1, 6);
        ListLevelOffset = Math.Clamp(ListLevelOffset, -6, 6);
        ListIndentSpaces = Math.Clamp(ListIndentSpaces, 0, 8);
        MinSuspectLines = Math.Clamp(MinSuspectLines, 1, 20);
    }

    public static StyleSettings PreserveAll() => new()
    {
        KeepBold = true,
        KeepItalic = true,
        KeepUnderline = true,
        KeepStrikethrough = true,
        KeepColor = true,
        LinkMode = LinkMode.Keep,
        MarkdownAllowHtml = true,
        SoftBreakMode = SoftBreakMode.Keep
    };

    public static StyleSettings Minimal() => new()
    {
        KeepBold = false,
        KeepItalic = false,
        KeepUnderline = false,
        KeepStrikethrough = false,
        KeepColor = false,
        CaseTransform = CaseTransform.Preserve,
        LinkMode = LinkMode.TextOnly,
        HeadingMode = HeadingMode.Keep,
        ListMode = ListMode.Keep,
        ParagraphSpacing = ParagraphSpacing.SingleNewline,
        MarkdownScriptMode = MarkdownScriptMode.Unicode,
        WordScriptMode = WordScriptMode.Unicode,
        PlainScriptMode = PlainScriptMode.Unicode,
        TrimWhitespace = true,
        CollapseSpaces = true
    };

    public static StyleSettings AcademicMarkdown() => new()
    {
        KeepBold = true,
        KeepItalic = false,
        KeepUnderline = false,
        KeepStrikethrough = false,
        KeepColor = false,
        LinkMode = LinkMode.Keep,
        HeadingMode = HeadingMode.Keep,
        ListMode = ListMode.Keep,
        BulletMarker = BulletMarker.Dash,
        ListIndentSpaces = 2,
        ParagraphSpacing = ParagraphSpacing.BlankLine,
        MarkdownScriptMode = MarkdownScriptMode.LaTeX,
        TrimWhitespace = true
    };

    public static StyleSettings SimpleWord() => new()
    {
        KeepBold = true,
        KeepItalic = false,
        KeepUnderline = false,
        KeepStrikethrough = false,
        KeepColor = false,
        LinkMode = LinkMode.TextOnly,
        HeadingMode = HeadingMode.Keep,
        ListMode = ListMode.Keep,
        WordScriptMode = WordScriptMode.Real,
        TrimWhitespace = true
    };
}
