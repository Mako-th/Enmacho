using TH09.Record;

namespace TH09.Shell.Data;

internal static class ReplayOwnLabels
{
    public const string Menu = "所有を手で決める";

    public const string Auto = "自動で判定する";

    public const string Foreign = "他人のリプレイ";

    public const string OwnP1 = "自分のリプレイ（1P）";

    public const string OwnP2 = "自分のリプレイ（2P）";

    public const string Category = "リプレイ";

    public const string Mark = "●";

    public const string Seat = "　";

    public static string Item(string label, bool active) => (active ? Mark : Seat) + " " + label;

    public const string NoDb = "本体 DB の場所が分からないので、所有を覆せません。";

    public static string Applied(long replayId, string label, bool changed)
        => "replay_id " + replayId.ToString(System.Globalization.CultureInfo.InvariantCulture)
           + " の所有を「" + label + "」に"
           + (changed ? "しました。" : "していました（変わっていません）。")
           + EffectNote;

    public const string EffectNote =
        "（一覧と「自分のみ」には今すぐ効きます。自己ベストの母数へ効くのは"
        + "「既存Replay一括登録」を回してからです。）";
}
