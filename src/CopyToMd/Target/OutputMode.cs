using System.Text.Json.Serialization;

namespace CopyToMd.Target;

[JsonConverter(typeof(JsonStringEnumConverter<OutputMode>))]
public enum OutputMode
{
    Auto,
    Markdown,
    Word,
    PlainText
}
