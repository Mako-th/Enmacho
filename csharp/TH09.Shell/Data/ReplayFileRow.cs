using System.Globalization;

namespace TH09.Shell.Data;

internal enum ReplayFileField
{
    Id,
    Path,
    Source,
    Current,
    Confidence,
    Decode,
}

internal sealed record ReplayFileColumn(string Label, ReplayFileField Field, double Width,
                                        bool RightAligned = false,
                                        CellStyle Style = CellStyle.Normal)
{
    public bool IsMuted => Style == CellStyle.Muted;
}

internal static class ReplayFileColumns
{
    public static readonly ReplayFileColumn[] All =
    [
        new("ID", ReplayFileField.Id, 56, RightAligned: true),
        new("Replayパス", ReplayFileField.Path, 560),
        new("保存元", ReplayFileField.Source, 90),
        new("現在", ReplayFileField.Current, 60),
        new("信頼度", ReplayFileField.Confidence, 80, RightAligned: true),
        new("解析", ReplayFileField.Decode, 90, Style: CellStyle.Muted),
    ];
}

internal sealed record ReplayFileCell(string Text, double Width, bool RightAligned, CellStyle Style)
{
    public bool IsMuted => Style == CellStyle.Muted;
}

internal sealed class ReplayFileRow
{
    public required long ReplayId { get; init; }

    public required string? FullPath { get; init; }

    public string ShownPath => DisplayPath(FullPath) ?? ReplayFormat.Missing;

    public required string? Source { get; init; }

    public string SourceText => Source ?? ReplayFormat.Missing;

    public required int? IsCurrent { get; init; }

    public string CurrentText => IsCurrent is int v && v != 0 ? "有" : "旧";

    public required double? Confidence { get; init; }

    public string ConfidenceText => Confidence is double d ? PythonFloat(d) : ReplayFormat.Missing;

    public required string? DecodeStatus { get; init; }

    public string DecodeText => string.IsNullOrEmpty(DecodeStatus) ? ReplayFormat.Missing : DecodeStatus;

    private ReplayFileCell[]? _cells;

    public IReadOnlyList<ReplayFileCell> Cells
    {
        get
        {
            if (_cells is not null) return _cells;
            var cols = ReplayFileColumns.All;
            var cells = new ReplayFileCell[cols.Length];
            for (var i = 0; i < cols.Length; i++)
                cells[i] = new ReplayFileCell(Text(cols[i].Field), cols[i].Width,
                                              cols[i].RightAligned, cols[i].Style);
            return _cells = cells;
        }
    }

    public string Text(ReplayFileField field) => field switch
    {
        ReplayFileField.Id => ReplayId.ToString(CultureInfo.InvariantCulture),
        ReplayFileField.Path => ShownPath,
        ReplayFileField.Source => SourceText,
        ReplayFileField.Current => CurrentText,
        ReplayFileField.Confidence => ConfidenceText,
        ReplayFileField.Decode => DecodeText,
        _ => throw new InvalidOperationException(
                 "ReplayFileField." + field + " の升目の字が書かれていない"),
    };

    public static readonly string[] Markers = ["replayautosavetest", "replayautosave", "replay"];

    public static string? DisplayPath(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return null;
        var parts = fullPath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;
        foreach (var marker in Markers)
            for (var i = 0; i < parts.Length; i++)
                if (string.Equals(parts[i], marker, StringComparison.OrdinalIgnoreCase))
                    return string.Join('\\', parts[i..]);
        return parts[^1];
    }

    private static string PythonFloat(double d)
    {
        var s = d.ToString("R", CultureInfo.InvariantCulture);
        return s.AsSpan().IndexOfAny('.', 'E', 'e') >= 0 || !char.IsAsciiDigit(s[^1])
            ? s : s + ".0";
    }
}

internal readonly record struct RevealTarget(string? Directory, string? Note);

internal static class ReplayFileReveal
{
    public static RevealTarget For(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return new RevealTarget(null, ReplayFileLabels.NoPathRecorded);
        if (!File.Exists(fullPath))
            return new RevealTarget(null, ReplayFileLabels.NotFound + "\n" + fullPath);
        var dir = Path.GetDirectoryName(fullPath);
        return string.IsNullOrEmpty(dir)
            ? new RevealTarget(null, ReplayFileLabels.NotFound + "\n" + fullPath)
            : new RevealTarget(dir, null);
    }

    public static RevealTarget For(ReplayFileRow? row) => For(row?.FullPath);

    public readonly record struct RevealCandidate(string Label, string Directory);

    public static IReadOnlyList<RevealCandidate> Candidates(IEnumerable<ReplayFileRow> rows)
    {
        var hits = new List<(string FullPath, string Directory)>();
        foreach (var row in rows)
        {
            var target = For(row.FullPath);
            if (target.Directory is string dir) hits.Add((row.FullPath!, dir));
        }
        if (hits.Count == 0) return [];

        var labels = new string[hits.Count];
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < hits.Count; i++)
        {
            labels[i] = ReplayFileRow.DisplayPath(hits[i].FullPath) ?? hits[i].FullPath;
            counts[labels[i]] = counts.GetValueOrDefault(labels[i]) + 1;
        }

        var result = new RevealCandidate[hits.Count];
        for (var i = 0; i < hits.Count; i++)
            result[i] = new RevealCandidate(
                counts[labels[i]] > 1 ? hits[i].FullPath : labels[i],
                hits[i].Directory);
        return result;
    }
}
