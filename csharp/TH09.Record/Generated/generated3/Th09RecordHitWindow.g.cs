#nullable enable

namespace TH09.Record.Generated;

using System.Collections.Frozen;

internal static class HitWindowConst
{
    public const uint HitValid = 2147483648u;
    public const uint HitlistValid = 2147483648u;
    public const uint FlagPlayersValid = 1u;
    public const int HitDelayTicks = 1;
    public const uint InputReplayBomb = 2u;

    public const string TriggerLifeRaw = "life_raw";
    public const string TriggerHitKind = "hit_kind";
    public const string TriggerQuick = "quick";
    public static readonly string[] DefaultTriggers = ["life_raw", "hit_kind", "quick"];

    public const int WindowBeforeTicks = 300;
    public const int WindowAfterTicks = 120;
    public const int WindowBeforeMin = 60;
    public const int WindowBeforeMax = 600;
    public const int WindowAfterMin = 61;
    public const int WindowAfterMax = 300;

    public const int HeadRawFactor = 3;
    public const int TailRawSlack = 300;
    public const int MaxWindowTicks = 1440;
    public const int MaxWindowRawCap = 2400;
    public const int MaxPendingWindows = 64;

    public const string QuantRawF32 = "rawf32-v1";
    public const string CoordFlagsColumn = "coord_flags";
    public const int ColumnNameCount = 11977;
    public const string ColumnNamesSha256 = "0b700530537e1dbbf622b9a22cdf7dc14c1ba000b4829c2457ebec5054e08cb4";

    public const string ScopeNet = "net";
    public const string ScopeLocal = "local";
    public const string ScopeCpu = "cpu";
    public const string ScopeStory = "story";
    public const string ScopeExtra = "extra";
    public const string ScopeUnknown = "unknown";
    public static readonly string[] CaptureScopes = ["net", "local", "cpu", "story", "extra"];
    public static readonly string[] ScopeUnknownMembers = ["net", "local"];

    public static readonly System.Collections.Frozen.FrozenDictionary<string, string> ScopeLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["net"] = "ネット対戦",
            ["local"] = "ローカル2P",
            ["cpu"] = "Match（対CPU）",
            ["story"] = "Story",
            ["extra"] = "Extra",
            ["unknown"] = "対人かローカルか不明",
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public const uint ModeStory = 0u;
    public const uint ModeExtra = 1u;
    public const uint ModeMatch = 2u;
    public const uint ControlCpu = 1u;

    public const string ReplaySourceAdonis = "Adonis";
    public const string ReplaySourceGame = "Game";
}
