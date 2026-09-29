using TH09.Record;

namespace TH09.Shell.Data;

internal static class ReplayOwnLabels
{
    public const string Menu = "所有を手で決める";

    public const string Auto = "自動に戻す";

    public const string Foreign = "自分のものでない";

    public const string OwnP1 = "1P にする";

    public const string OwnP2 = "2P にする";

    public const string Category = "リプレイ";

    public const string Mark = "●";

    public const string Seat = "　";

    public static string Item(string label, bool active) => (active ? Mark : Seat) + " " + label;


    public const string MarkOwn = "自分のものにする";

    public const string Delete = "記録の削除";

    public const string CheckAllTip = "表示中を全部チェック";

    public const string ClearChecks = "チェックを外す";

    public const string DeleteAndExclude = "削除後、以後の一括登録でも登録しない";

    public const string DeleteOnly = "記録だけ削除する";

    public const string DeleteCancel = "やめる";

    public const string DeleteHeader = "リプレイの記録を削除";

    public const string NoTarget = "対象の行がありません";

    public const string NoDbDelete = "本体 DB の場所が分からないので、記録を削除できません。";

    public const string NoLayer0Delete =
        "Layer 0 の場所が未設定です。走査で作った記録の生 tick を消せないので、記録の削除はしません。";

    public static string AllMissing(int n)
        => "対象の " + n.ToString(System.Globalization.CultureInfo.InvariantCulture)
           + " 件は、本体 DB にもう行がありません（削除するものがありません）。";

    public static string ExcludeSaveFailed(string? reason)
        => "設定（config.json）へ書けなかったので、記録は削除しませんでした: " + (reason ?? "理由不明");

    public static string DeleteFailed(string reason, bool excludeSaved)
        => "記録を削除できませんでした: " + reason
           + (excludeSaved ? "（登録しない設定は保存済みです。設定画面の除外の一覧から外せます。）" : "");

    public static string Deleted(int total, int deleted, int missing, int scanSessions, int detached,
                                 int? excludedAdded, int excludedAlready)
    {
        static string N(int v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var s = "記録を削除しました: " + N(total) + " 件のうち削除 " + N(deleted) + " 件・行が無かった " + N(missing)
                + " 件 ／ 走査のセッション " + N(scanSessions) + " 本を消し、紐付きだけ外したセッション "
                + N(detached) + " 本";
        if (excludedAdded is int added)
            s += " ／ 以後登録しない: " + N(added) + " 件を足した（もう在ったもの " + N(excludedAlready) + " 件）";
        return s + "。";
    }

    public static string CheckedCount(int n)
        => "チェック " + n.ToString(System.Globalization.CultureInfo.InvariantCulture) + " 件";

    public static string TargetLine(int n, bool byCheck)
        => "対象 " + n.ToString(System.Globalization.CultureInfo.InvariantCulture) + " 件（"
           + (byCheck ? "チェックした行" : "右クリックした行") + "）";

    public static string DeleteMenu(int n)
        => Delete + "（" + n.ToString(System.Globalization.CultureInfo.InvariantCulture) + " 件）";

    public static string DeleteConfirmText(int n)
        => n.ToString(System.Globalization.CultureInfo.InvariantCulture)
           + " 件のリプレイの記録を削除します。走査で作った記録は消えます。"
           + "監視で記録したプレイは残り、リプレイとの結び付きだけ外れます。"
           + ".rpy ファイルは消しません。";

    public static string DeleteConfirmText(int n, string line) => DeleteConfirmText(n) + "\n" + line;

    public const string OwnNameExplain =
        "登録した名前はそれぞれ別に判定し、どれか 1 つがリプレイの名前の一部に含まれていれば"
        + "自分のものとします（部分一致。ワイルドカードはありません）。"
        + "「mako」と登録すると「mako@th」「mako_Win」にも当たります。"
        + "短すぎる名前は他人の名前にも当たるので、名前の本体を登録してください。";

    public const string CaseSensitive = "大文字と小文字を区別する";

    public const string OwnDirsHeader = "自分のフォルダ（複数可）";

    public const string OwnDirsNote =
        "このフォルダの配下のリプレイは、名前に当たらなくても自分のものとして登録します。"
        + "設定を外したら、既存Replay一括登録をやり直すと元に戻ります。";

    public const string ExcludedHeader = "登録しないリプレイ（除外）";

    public const string ExcludedSaveNote = "「外す」は［保存］を押して初めて効きます。";

    public const string ExcludedNote =
        "外すと次の一括登録で登録し直します。走査の記録は戻らないので走査し直してください。";

    public const string ExcludedWhere = "この一覧はこの設定ファイル（config.json）にだけあります。";

    public const string NoDb = "本体 DB の場所が分からないので、所有を覆せません。";

    public static string Applied(long replayId, string label, bool changed)
        => "replay_id " + replayId.ToString(System.Globalization.CultureInfo.InvariantCulture)
           + " に「" + label + "」を"
           + (changed ? "しました。" : "していました（変わっていません）。");

    public static string AppliedMany(int total, string label, int changed, int unchanged, int missing,
                                     int withoutSide)
        => "「" + label + "」: " + OwnResultLine(total, changed, unchanged, missing, withoutSide) + "。";

    public static string OwnResultLine(int total, int changed, int unchanged, int missing, int withoutSide)
    {
        static string N(int v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var s = "対象 " + N(total) + " 件のうち 変わった " + N(changed) + " 件・すでに同じ " + N(unchanged)
                + " 件・行が無かった " + N(missing) + " 件";
        if (withoutSide > 0)
            s += "（うち 側が分からず「自分・側不明」になったもの " + N(withoutSide) + " 件。統計の集計には入りません）";
        return s;
    }

}
