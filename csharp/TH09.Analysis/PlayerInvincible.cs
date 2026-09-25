namespace TH09.Analysis;

public static class PlayerInvincible
{
    public static string LimitNote => AnalysisTables.InvincibleLimitNote;

    private static readonly Dictionary<string, string> ReasonJa = BuildReasonJa();

    private static Dictionary<string, string> BuildReasonJa()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in Packed.Lines(AnalysisTables.InvincibleReasonJaPacked))
        {
            var f = line.Split('\t');
            map[f[0]] = f[1];
        }
        return map;
    }

    public const string ReasonState = "state";
    public const string ReasonTimer = "timer";
    public const string ReasonBoth = "both";

    public static string ReasonLabel(string reason) =>
        ReasonJa.TryGetValue(reason, out var ja) ? ja : reason;

    public static bool HasWords(Window w) =>
        w.Main.Has("p1_player_state") && w.Main.Has("p1_invincible_timer")
        && w.Main.Has("p2_player_state") && w.Main.Has("p2_invincible_timer");

    public static bool?[] Flags(Window w, int side)
    {
        var st = w.Series("p" + side + "_player_state");
        var iv = w.Series("p" + side + "_invincible_timer");
        var outv = new bool?[w.TickCount];
        if (st is null || iv is null) return outv;
        for (int i = 0; i < outv.Length; i++)
        {
            var got = CanBeHit(i < st.Length ? st[i] : null, i < iv.Length ? iv[i] : null);
            outv[i] = got is null ? null : !got.Value;
        }
        return outv;
    }

    public static bool? CanBeHit(double? playerState, double? invincibleTimer)
    {
        if (playerState is null || invincibleTimer is null) return null;
        return (int)playerState.Value == AnalysisTables.PlayerStateNormal
               && (int)invincibleTimer.Value <= 0;
    }

    public static List<InvincibleSpan> Spans(Window w, int side)
    {
        var st = w.Series("p" + side + "_player_state");
        var iv = w.Series("p" + side + "_invincible_timer");
        var outv = new List<InvincibleSpan>();
        if (st is null || iv is null) return outv;
        var flags = Flags(w, side);

        string? ReasonAt(int i)
        {
            if (flags[i] != true) return null;
            var a = i < st.Length ? st[i] : null;
            var b = i < iv.Length ? iv[i] : null;
            bool byState = a is not null && (int)a.Value != AnalysisTables.PlayerStateNormal;
            bool byTimer = b is not null && (int)b.Value > 0;
            if (byState && byTimer) return ReasonBoth;
            return byState ? ReasonState : ReasonTimer;
        }

        int start = -1; string? cur = null;
        for (int i = 0; i < flags.Length; i++)
        {
            var v = ReasonAt(i);
            if (v is null) { Flush(i); cur = null; continue; }
            if (cur is not null && string.Equals(cur, v, StringComparison.Ordinal)) continue;
            Flush(i);
            cur = v; start = i;
        }
        Flush(flags.Length);
        return outv;

        void Flush(int end)
        {
            if (cur is null || start < 0) return;
            outv.Add(new InvincibleSpan(start, end - start, cur, ReasonLabel(cur)));
            start = -1;
        }
    }


    public static IEnumerable<(string Label, Func<string?> Body)> SelfTestCases()
    {
        yield return ("無敵の理由の表が空でない（母数）", () =>
            ReasonJa.Count > 0 ? null : "0 件。生成物を引けていない");
        yield return ("理由の鍵が 3 つとも表にある", () =>
            ReasonJa.ContainsKey(ReasonState) && ReasonJa.ContainsKey(ReasonTimer)
            && ReasonJa.ContainsKey(ReasonBoth) ? null : "鍵が足りない: " + string.Join(",", ReasonJa.Keys));
        yield return ("限界の断り書きが空でない", () =>
            !string.IsNullOrEmpty(LimitNote) ? null : "空。凡例で断れない");

        yield return ("player_state 0 かつ timer 0 は被弾しうる", () =>
            CanBeHit(0, 0) == true ? null : "被弾しうるにならない");
        yield return ("timer が正なら被弾しない", () =>
            CanBeHit(0, 1) == false ? null : "無敵にならない");
        yield return ("★timer が負でも被弾しうる（<= 0 で書く理由）", () =>
            CanBeHit(0, -5) == true ? null : "負の timer を無敵に倒している");
        yield return ("player_state が 0 でなければ被弾しない", () =>
            CanBeHit(2, 0) == false ? null : "無敵にならない");
        yield return ("★片方でも読めなければ null（false に倒さない）", () =>
            CanBeHit(null, 0) is null && CanBeHit(0, null) is null ? null : "null にならない");

        yield return ("★否定: 旧（timer だけ見る）なら『やられ中』を無敵と言えない", () =>
        {
            bool LegacyInvincible(double state, double timer) => timer > 0;
            bool oldSays = LegacyInvincible(2, 0);
            bool newSays = CanBeHit(2, 0) == false;
            if (oldSays) return "旧が落ちない（写しが間違っている。否定テストの意味が無い）";
            return newSays ? null : "新も『やられ中』を無敵と言えていない";
        });
        yield return ("★否定: 旧（timer >= 0）なら timer=0 が無敵に化ける", () =>
        {
            bool LegacyInvincible(double timer) => timer >= 0;
            if (!LegacyInvincible(0)) return "旧が落ちない（写しが間違っている）";
            return CanBeHit(0, 0) == true ? null : "新でも timer=0 が無敵になっている";
        });
    }
}

public readonly record struct InvincibleSpan(int Index, int Frames, string Reason, string Label);
