using System.Runtime.Versioning;
using TH09.Record;

namespace TH09.Shell.Data;

internal sealed record ExcludedSaveResult(bool Saved, int Added, int AlreadyThere, string? Reason);

[SupportedOSPlatform("windows")]
internal static class ExcludedReplaysSave
{
    public static ExcludedSaveResult Add(IReadOnlyList<ExcludedReplayEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if ((TestLaunchGuard.Active || LogSource.SuppressFileWrites) && !AppSettingsSource.HasRedirected)
            return new ExcludedSaveResult(false, 0, 0,
                "診断・自己検査・ベンチの走行で、設定の読み先を差し替えていないので、書きませんでした。");
        try
        {
            var path = AppSettingsSource.ConfigPath;
            var current = ConfigStore.Load(path);
            var have = new HashSet<string>(current.ExcludedReplays.Select(e => e.Sha256.ToLowerInvariant()),
                                           StringComparer.Ordinal);
            var next = new List<ExcludedReplayEntry>(current.ExcludedReplays);
            var already = 0;
            foreach (var e in entries)
            {
                if (have.Add(e.Sha256.ToLowerInvariant())) next.Add(e); else already++;
            }
            if (next.Count == current.ExcludedReplays.Count)
                return new ExcludedSaveResult(true, 0, already, null);
            var updated = current with { ExcludedReplays = next };
            var outcome = ConfigStore.Save(path, updated);
            if (!outcome.Written)
                return new ExcludedSaveResult(false, 0, already, outcome.Reason ?? "理由不明");
            AppSettingsSource.Adopt(updated);
            return new ExcludedSaveResult(true, next.Count - current.ExcludedReplays.Count, already, null);
        }
        catch (Exception ex)
        {
            return new ExcludedSaveResult(false, 0, 0, ex.Message);
        }
    }
}
