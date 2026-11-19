using CopyToMd.Target;

namespace CopyToMd.Conversion;

public sealed class ConversionService
{
    private readonly HtmlImporter htmlImporter = new();
    private readonly RtfImporter rtfImporter = new();
    private readonly PlainTextImporter plainTextImporter = new();
    private readonly StyleSettings style;
    private readonly MarkdownRenderer markdownRenderer;
    private readonly PlainTextRenderer plainTextRenderer;
    private readonly WordRenderer wordRenderer;

    public ConversionService(StyleSettings? style = null)
    {
        this.style = style ?? new StyleSettings();
        markdownRenderer = new MarkdownRenderer(this.style);
        plainTextRenderer = new PlainTextRenderer(this.style);
        wordRenderer = new WordRenderer(this.style);
    }

    public ConversionResult Convert(string? html, string? rtf, string? unicodeText, OutputMode mode)
    {
        if (mode == OutputMode.Auto)
        {
            throw new ArgumentException("转换前必须确定输出模式。", nameof(mode));
        }

        style.Normalize();
        var plainDocument = plainTextImporter.Import(unicodeText, style);
        htmlImporter.TryImport(html, out var htmlDocument);

        RichDocument? document = htmlDocument?.HasSemanticFormatting == true ? htmlDocument : null;
        if (document is null && rtfImporter.TryImport(rtf, unicodeText, out var rtfDocument))
        {
            document = rtfDocument;
        }

        document ??= htmlDocument;
        if (document is null)
        {
            document = plainDocument;
            document.UsedFallback = html is not null || rtf is not null;
            if (document.UsedFallback)
            {
                document.Warnings.Add("富文本不可用或不可信，已使用保守纯文本清理。");
            }
        }

        DocumentStyler.Apply(document, style);

        return mode switch
        {
            OutputMode.Markdown => new ConversionResult(mode, document, markdownRenderer.Render(document)),
            OutputMode.Word => CreateWordResult(document),
            _ => new ConversionResult(mode, document, plainTextRenderer.Render(document))
        };
    }

    private ConversionResult CreateWordResult(RichDocument document)
    {
        var plainText = plainTextRenderer.Render(document);
        var rtf = wordRenderer.RenderRtf(document);
        if (!RtfRoundTripValidator.IsValid(rtf, plainText))
        {
            document.Warnings.Add("生成的 Word RTF 未通过往返校验，已回退为纯文本。");
            return new ConversionResult(OutputMode.Word, document, plainText);
        }

        return new ConversionResult(OutputMode.Word, document, plainText, rtf);
    }
}
