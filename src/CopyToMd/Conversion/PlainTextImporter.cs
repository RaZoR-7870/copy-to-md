using System.Text.RegularExpressions;

namespace CopyToMd.Conversion;

public sealed partial class PlainTextImporter
{
    private sealed class ParsedLine
    {
        public required string Raw { get; init; }
        public string Indent { get; private set; } = string.Empty;
        public string Body { get; private set; } = string.Empty;
        public int Number { get; private set; } = 1;
        public bool IsBlank => string.IsNullOrWhiteSpace(Raw);
        public bool IsFence { get; init; }
        public bool IsSuspect { get; private set; }
        public char Marker { get; private set; }
        public bool IsStandardBullet { get; private set; }
        public bool IsOrdered { get; private set; }
        public bool IsConfirmedList { get; set; }
        public int IndentWidth => Indent.Sum(character => character == '\t' ? 4 : 1);

        public void Parse(bool inFence)
        {
            if (IsBlank || IsFence || inFence)
            {
                Body = Raw;
                return;
            }

            var match = SuspectBulletRegex().Match(Raw);
            if (match.Success)
            {
                IsSuspect = true;
                ReadListMatch(match);
                return;
            }

            match = StandardBulletRegex().Match(Raw);
            if (match.Success)
            {
                IsStandardBullet = true;
                IsConfirmedList = true;
                ReadListMatch(match);
                return;
            }

            match = OrderedListRegex().Match(Raw);
            if (match.Success)
            {
                IsOrdered = true;
                IsConfirmedList = true;
                ReadListMatch(match);
                Number = int.Parse(match.Groups["number"].Value);
                return;
            }

            Body = Raw;
        }

        private void ReadListMatch(Match match)
        {
            Indent = match.Groups["indent"].Value;
            Body = match.Groups["body"].Value;
            if (match.Groups["marker"].Success)
            {
                Marker = match.Groups["marker"].Value[0];
            }
        }
    }

    public RichDocument Import(string? text)
    {
        var document = new RichDocument
        {
            Source = DocumentSource.PlainText,
            NewLine = text?.Contains("\r\n", StringComparison.Ordinal) == true ? "\r\n" : "\n"
        };

        if (string.IsNullOrEmpty(text))
        {
            return document;
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var rawLines = normalized.Split('\n');
        var lines = new List<ParsedLine>(rawLines.Length);
        var inFence = false;

        foreach (var raw in rawLines)
        {
            var trimmed = raw.TrimStart();
            var isFence = trimmed.StartsWith("```", StringComparison.Ordinal) ||
                          trimmed.StartsWith("~~~", StringComparison.Ordinal);
            var line = new ParsedLine { Raw = raw, IsFence = isFence };
            line.Parse(inFence);
            lines.Add(line);

            if (isFence)
            {
                inFence = !inFence;
            }
        }

        ConfirmSuspectLists(lines);
        var indentLevels = lines
            .Where(line => line.IsConfirmedList)
            .Select(line => line.IndentWidth)
            .Distinct()
            .OrderBy(width => width)
            .ToList();

        foreach (var line in lines)
        {
            if (line.IsBlank)
            {
                document.Blocks.Add(DocumentBlock.FromText(string.Empty));
                continue;
            }

            if (!line.IsConfirmedList)
            {
                document.Blocks.Add(DocumentBlock.FromText(line.Raw));
                continue;
            }

            var block = DocumentBlock.Create(BlockKind.ListItem, line.Body);
            block.ListKind = line.IsOrdered ? ListKind.Ordered : ListKind.Bullet;
            block.Number = line.Number;
            block.Level = Math.Max(0, indentLevels.IndexOf(line.IndentWidth));
            document.Blocks.Add(block);
        }

        return document;
    }

    private static void ConfirmSuspectLists(IReadOnlyList<ParsedLine> lines)
    {
        var start = 0;
        while (start < lines.Count)
        {
            while (start < lines.Count && (lines[start].IsBlank || lines[start].IsFence))
            {
                start++;
            }

            var end = start;
            while (end < lines.Count && !lines[end].IsBlank && !lines[end].IsFence)
            {
                end++;
            }

            var index = start;
            while (index < end)
            {
                if (!lines[index].IsSuspect)
                {
                    index++;
                    continue;
                }

                var groupStart = index;
                while (index < end && lines[index].IsSuspect)
                {
                    index++;
                }

                var count = index - groupStart;
                if (count < 3)
                {
                    // One or two leading letters can be ordinary prose or variable
                    // definitions. Without rich-font evidence, preserve them.
                    continue;
                }

                for (var line = groupStart; line < index; line++)
                {
                    lines[line].IsConfirmedList = true;
                }
            }

            start = end + 1;
        }
    }

    [GeneratedRegex(@"^(?<indent>[ \t]*)(?<marker>[lmn])(?:\t| +)(?<body>(?![=]).*?\S)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex SuspectBulletRegex();

    [GeneratedRegex(@"^(?<indent>[ \t]*)(?:[•◦▪·∙]|[-*+])(?:\t| +)(?<body>.*?\S)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex StandardBulletRegex();

    [GeneratedRegex(@"^(?<indent>[ \t]*)(?<number>\d{1,9})[.)](?:\t| +)(?<body>.*?\S)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex OrderedListRegex();
}
