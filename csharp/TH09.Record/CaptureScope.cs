using TH09.Record.Generated;
using TH09.TickBus;
using R = TH09.Generated.TickWords.Record;

namespace TH09.Record;

public static class CaptureScope
{
    public const string Net = HitWindowConst.ScopeNet;

    public const string Local = HitWindowConst.ScopeLocal;

    public const string Cpu = HitWindowConst.ScopeCpu;

    public const string Story = HitWindowConst.ScopeStory;

    public const string Extra = HitWindowConst.ScopeExtra;

    public const string Unknown = HitWindowConst.ScopeUnknown;

    public static IReadOnlyList<string> All => HitWindowConst.CaptureScopes;

    public static string Label(string scope) =>
        HitWindowConst.ScopeLabels.TryGetValue(scope, out var s) ? s : scope;

    public static string OfRecord(TickRecord rec, bool adonisLoaded, string? replaySource)
    {
        uint mode = rec.At(R.ModeOffset);
        var (_label, replayActive, _flag) = SnapshotMapper.ExecutionType(rec, adonisLoaded);
        if (mode == HitWindowConst.ModeStory) return Story;
        if (mode == HitWindowConst.ModeExtra) return Extra;
        if (rec.At(R.P1ControlOffset) == HitWindowConst.ControlCpu
            || rec.At(R.P2ControlOffset) == HitWindowConst.ControlCpu) return Cpu;
        if (replayActive != 0)
        {
            if (string.Equals(replaySource, HitWindowConst.ReplaySourceAdonis, StringComparison.Ordinal))
                return Net;
            if (string.Equals(replaySource, HitWindowConst.ReplaySourceGame, StringComparison.Ordinal))
                return Local;
            return Unknown;
        }
        if (adonisLoaded) return Net;
        return Local;
    }

    public static Dictionary<string, bool> Normalize(IReadOnlyDictionary<string, bool>? raw)
    {
        var outMap = new Dictionary<string, bool>(All.Count, StringComparer.Ordinal);
        foreach (var s in All)
            outMap[s] = raw is null || !raw.TryGetValue(s, out var v) || v;
        return outMap;
    }

    public static bool IsEnabled(string scope, IReadOnlyDictionary<string, bool>? scopes)
    {
        if (scopes is null) return true;
        if (string.Equals(scope, Unknown, StringComparison.Ordinal))
            return HitWindowConst.ScopeUnknownMembers.Any(s => !scopes.TryGetValue(s, out var v) || v);
        return !scopes.TryGetValue(scope, out var got) || got;
    }

    public static string Note(IReadOnlyDictionary<string, bool>? scopes)
    {
        if (scopes is null) return "全部取ります";
        var on = All.Where(s => !scopes.TryGetValue(s, out var v) || v).Select(Label).ToList();
        var off = All.Where(s => scopes.TryGetValue(s, out var v) && !v).Select(Label).ToList();
        if (off.Count == 0) return "全部取ります";
        if (on.Count == 0) return "どれも取りません";
        return "取る: " + string.Join(" ", on) + " ／ 取らない: " + string.Join(" ", off);
    }
}
