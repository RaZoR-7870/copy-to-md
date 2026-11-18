using System.Text.RegularExpressions;

namespace CopyToMd.Target;

public sealed class TargetResolver
{
    private readonly IReadOnlyList<TargetRule> rules;

    public TargetResolver(IEnumerable<TargetRule>? userRules = null)
    {
        rules = (userRules ?? []).Where(rule => rule is not null).Concat(DefaultRules()).ToList();
    }

    public TargetResolution Resolve(TargetInfo target, OutputMode overrideMode = OutputMode.Auto)
    {
        if (overrideMode != OutputMode.Auto)
        {
            return new TargetResolution(overrideMode, "手动模式");
        }

        foreach (var rule in rules)
        {
            if (Matches(rule, target))
            {
                return new TargetResolution(rule.Mode, string.IsNullOrWhiteSpace(rule.Name) ? rule.ProcessName : rule.Name);
            }
        }

        return new TargetResolution(OutputMode.PlainText, "未知应用回退");
    }

    private static bool Matches(TargetRule rule, TargetInfo target)
    {
        if (!string.IsNullOrWhiteSpace(rule.ProcessName) &&
            !target.ProcessName.Equals(NormalizeProcessName(rule.ProcessName), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.TitlePattern))
        {
            try
            {
                if (!Regex.IsMatch(target.WindowTitle, rule.TitlePattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
                {
                    return false;
                }
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        var extensions = rule.Extensions ?? [];
        return extensions.Length == 0 || extensions.Any(extension => HasExtension(target.WindowTitle, extension));
    }

    private static bool HasExtension(string title, string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        var suffix = extension.TrimStart('.');
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return false;
        }

        var pattern = $@"\.{Regex.Escape(suffix)}(?=$|[\s\]\)\}}—–-])";
        return Regex.IsMatch(title, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
    }

    private static string NormalizeProcessName(string value) =>
        value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? value[..^4] : value;

    private static IEnumerable<TargetRule> DefaultRules()
    {
        yield return new TargetRule { Name = "Obsidian", ProcessName = "obsidian", Mode = OutputMode.Markdown };
        yield return new TargetRule { Name = "Typora", ProcessName = "typora", Mode = OutputMode.Markdown };
        yield return new TargetRule { Name = "VS Code Markdown", ProcessName = "code", Extensions = ["md", "markdown", "mdx"], Mode = OutputMode.Markdown };
        yield return new TargetRule { Name = "Microsoft Word", ProcessName = "winword", Mode = OutputMode.Word };
        yield return new TargetRule { Name = "记事本", ProcessName = "notepad", Mode = OutputMode.PlainText };
        yield return new TargetRule { Name = "VS Code 非 Markdown", ProcessName = "code", Mode = OutputMode.PlainText };
    }
}
