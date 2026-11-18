using System.Text;

namespace CopyToMd.Conversion;

internal static class InlineScriptNormalizer
{
    private const string SuperscriptAscii = "0123456789+-=()ni";
    private const string SuperscriptUnicode = "⁰¹²³⁴⁵⁶⁷⁸⁹⁺⁻⁼⁽⁾ⁿⁱ";
    private const string SubscriptAscii = "0123456789+-=()aehijklmnoprstuvx";
    private const string SubscriptUnicode = "₀₁₂₃₄₅₆₇₈₉₊₋₌₍₎ₐₑₕᵢⱼₖₗₘₙₒₚᵣₛₜᵤᵥₓ";

    private static readonly Dictionary<char, char> SuperscriptToAscii = BuildMap(SuperscriptUnicode, SuperscriptAscii);
    private static readonly Dictionary<char, char> SubscriptToAscii = BuildMap(SubscriptUnicode, SubscriptAscii);
    private static readonly Dictionary<char, char> AsciiToSuperscript = BuildMap(SuperscriptAscii, SuperscriptUnicode);
    private static readonly Dictionary<char, char> AsciiToSubscript = BuildMap(SubscriptAscii, SubscriptUnicode);

    public static List<InlineRun> Normalize(IEnumerable<InlineRun> runs)
    {
        var result = new List<InlineRun>();
        foreach (var run in runs)
        {
            if (run.Script != ScriptKind.None)
            {
                var decoded = new StringBuilder(run.Text.Length);
                foreach (var character in run.Text)
                {
                    decoded.Append(TryReadUnicodeScript(character, out var ascii, out _) ? ascii : character);
                }

                result.Add(run with { Text = decoded.ToString() });
                continue;
            }

            var normal = new StringBuilder();
            var script = new StringBuilder();
            var scriptKind = ScriptKind.None;

            foreach (var character in run.Text)
            {
                if (TryReadUnicodeScript(character, out var ascii, out var kind))
                {
                    FlushNormal(run, normal);
                    if (scriptKind != kind)
                    {
                        FlushScript(run, script, scriptKind);
                        scriptKind = kind;
                    }

                    script.Append(ascii);
                    continue;
                }

                FlushScript(run, script, scriptKind);
                scriptKind = ScriptKind.None;
                normal.Append(character);
            }

            FlushScript(run, script, scriptKind);
            FlushNormal(run, normal);
        }

        return result;

        void FlushNormal(InlineRun source, StringBuilder text)
        {
            if (text.Length > 0)
            {
                result.Add(source with { Text = text.ToString() });
                text.Clear();
            }
        }

        void FlushScript(InlineRun source, StringBuilder text, ScriptKind kind)
        {
            if (text.Length > 0 && kind != ScriptKind.None)
            {
                result.Add(source with { Text = text.ToString(), Script = kind });
                text.Clear();
            }
        }
    }

    public static string ToUnicode(string text, ScriptKind kind)
    {
        if (kind == ScriptKind.None || text.Length == 0)
        {
            return text;
        }

        var map = kind == ScriptKind.Superscript ? AsciiToSuperscript : AsciiToSubscript;
        var output = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (!map.TryGetValue(character, out var mapped))
            {
                return text.Length == 1
                    ? (kind == ScriptKind.Superscript ? "^" : "_") + text
                    : (kind == ScriptKind.Superscript ? "^(" : "_(") + text + ")";
            }

            output.Append(mapped);
        }

        return output.ToString();
    }

    private static bool TryReadUnicodeScript(char character, out char ascii, out ScriptKind kind)
    {
        if (SuperscriptToAscii.TryGetValue(character, out ascii))
        {
            kind = ScriptKind.Superscript;
            return true;
        }

        if (SubscriptToAscii.TryGetValue(character, out ascii))
        {
            kind = ScriptKind.Subscript;
            return true;
        }

        kind = ScriptKind.None;
        return false;
    }

    private static Dictionary<char, char> BuildMap(string keys, string values)
    {
        if (keys.Length != values.Length)
        {
            throw new InvalidOperationException("上下标字符映射长度不一致。");
        }

        return keys.Zip(values, (key, value) => (key, value)).ToDictionary(pair => pair.key, pair => pair.value);
    }
}
