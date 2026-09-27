using TH09.Generated;

namespace TH09.Drive;

public class MenuDriver
{
    public const uint SkipHoldFields = TickWords.CmdFieldBits.MirrorP1;

    public uint SkipFields { get; set; } = SkipHoldFields;

    public static readonly Action<string> Quiet = _ => { };

    public static void DefaultLog(string message) =>
        Console.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message);

    protected IMenuView View { get; }

    protected IInputSink Sink { get; }

    protected Action<string> Log { get; }

    protected Func<double> Clock { get; }

    protected Action<double> Sleep { get; }

    protected double Poll { get; }

    protected int TapTicks { get; }

    public double ConfirmSec { get; set; } = 1.0;

    public MenuState? Last { get; private set; }

    public int Taps { get; protected set; }

    public int Resends { get; private set; }

    private bool _leftMenu;
    private int _listStable;

    public MenuDriver(IMenuView view, IInputSink sink, Action<string>? log = null,
                      Func<double>? clock = null, Action<double>? sleep = null,
                      double poll = 1 / 120.0, int tapTicks = 6)
    {
        View = view;
        Sink = sink;
        Log = log ?? DefaultLog;
        Clock = clock ?? (() => (double)Environment.TickCount64 / 1000.0);
        Sleep = sleep ?? (sec => Thread.Sleep((int)(sec * 1000.0)));
        Poll = poll;
        TapTicks = tapTicks;
    }


    public MenuState Observe()
    {
        Last = View.Read();
        return Last.Value;
    }

    public MenuState WaitFor(Func<MenuState, bool> pred, double timeout, double? poll = null)
    {
        double interval = poll ?? Poll;
        double deadline = Clock() + Math.Max(0.0, timeout);
        while (true)
        {
            MenuState state = Observe();
            if (pred(state)) return state;
            if (Clock() >= deadline)
                throw new MenuTimeout("期限内に状態が変わりませんでした（最後の状態: " + state.Text() + "）");
            Sleep(interval);
        }
    }

    public MenuState WaitReady(int screen, double timeout)
    {
        try
        {
            return WaitFor(s => s.Valid && s.ScreenId == (uint)screen
                                && s.Substate == MenuMapLayout.SubReady && !s.Transitioning,
                           timeout);
        }
        catch (MenuTimeout exc)
        {
            throw new MenuTimeout("画面 " + screen + "(" + MenuMap.ScreenName((uint)screen)
                                  + ") が操作可能になりません: " + exc.Message);
        }
    }

    protected static bool Sendable(MenuState state) =>
        state.Valid && !state.Transitioning && state.Substate != MenuMapLayout.SubEntering;


    public MenuState Commit(uint mask, Func<MenuState, bool> afterPred, double timeout, int retries = 3)
    {
        double deadline = Clock() + Math.Max(0.0, timeout);
        int maxTaps = Math.Max(1, retries + 1);
        int sent = 0;
        while (sent < maxTaps && Clock() < deadline)
        {
            try
            {
                WaitFor(Sendable, Math.Max(0.0, deadline - Clock()));
            }
            catch (MenuTimeout)
            {
                break;
            }
            Sink.Tap(mask, TapTicks);
            Taps++;
            sent++;
            try
            {
                return WaitFor(afterPred, Math.Min(ConfirmSec, Math.Max(0.0, deadline - Clock())));
            }
            catch (MenuTimeout)
            {
                Resends++;
                Log("入力 " + MenuMap.KeyName(mask) + " が効いていません。再送します（"
                    + sent + "/" + maxTaps + "）");
            }
        }
        throw new MenuTimeout("入力 " + MenuMap.KeyName(mask) + " を " + sent
                              + " 回送っても状態が変わりませんでした（最後の状態: "
                              + (Last is null ? "未観測" : Last.Value.Text()) + "）");
    }


    public const double AbortTimeout = 180.0;

    public const int AbortMaxHolds = 8;

    public const double AbortHoldSec = 25.0;

    public MenuState EndReplayPlayback(string reason, double? timeout = null, int? maxHolds = null)
    {
        if (ViewTitleDemo() == true)
        {
            Log("タイトルデモなので早送りしません（ensure_title に任せます）。");
            return Observe();
        }

        MenuState state = Observe();
        if (state.Valid && !ViewReplayPlaying())
        {
            Log("再生していないので早送りしません（" + state.Text() + "）");
            return state;
        }

        double limit = timeout ?? AbortTimeout;
        int holdLimit = maxHolds ?? AbortMaxHolds;
        Log("★走査が残したリプレイを早送りで終わらせます: " + reason);
        double deadline = Clock() + Math.Max(0.0, limit);
        int holds = 0;
        int stable = 0;
        try
        {
            while (true)
            {
                if (holds >= holdLimit)
                {
                    throw new PlaybackAbortFailed(
                        "skip の保持を " + holds + " 回送っても再生が終わりません（最後の状態: "
                        + (Last is null ? "未観測" : Last.Value.Text()) + "）。"
                        + "手でタイトルへ戻してください。**ゲームは終了させないこと。**");
                }
                if (!Sink.SetHold(MenuMapLayout.BitSkip, SkipFields))
                {
                    Log("警告: skip の保持が ack されませんでした" + HoldFailureReason()
                        + " 送り直します（" + (holds + 1) + "/" + holdLimit + "）");
                }
                holds++;
                double until = Math.Min(Clock() + AbortHoldSec, deadline);
                while (true)
                {
                    MenuState st = Observe();
                    if (st.Valid && !ViewReplayPlaying())
                    {
                        stable++;
                        if (stable >= StableReads)
                        {
                            Log("再生が終わってメニューへ戻りました（" + st.Text() + "）");
                            return st;
                        }
                    }
                    else
                    {
                        stable = 0;
                    }
                    double now = Clock();
                    if (now >= deadline)
                    {
                        throw new PlaybackAbortFailed(
                            "走査が残したリプレイが " + FormatG(limit) + " 秒（skip 保持 " + holds
                            + " 回）で終わりません（最後の状態: " + st.Text() + "）。"
                            + "手でタイトルへ戻してください。**ゲームは終了させないこと。**");
                    }
                    if (now >= until) break;
                    Sleep(Poll);
                }
            }
        }
        finally
        {
            ReleaseSkip();
        }
    }


    public virtual MenuState EnsureTitle(int maxCancels = 8, double timeout = 60.0)
    {
        double deadline = Clock() + Math.Max(0.0, timeout);
        int cancels = 0;
        int demoTaps = 0;
        while (true)
        {
            MenuState state = Observe();
            if (state.Valid && state.ScreenId == MenuMapLayout.ScreenTitle)
            {
                if (state.Substate == MenuMapLayout.SubReady && !state.Transitioning) return state;
            }
            else if (Sendable(state))
            {
                if (cancels >= maxCancels)
                {
                    throw new MenuLost("X を " + cancels + " 回送ってもタイトルへ戻れません（最後の状態: "
                                       + state.Text() + "）。手で操作してタイトルへ戻してください。");
                }
                Log("タイトルへ戻ります: X を送ります（" + (cancels + 1) + "/" + maxCancels
                    + "、現在 " + state.Text() + "）");
                Sink.Tap(MenuMapLayout.BitCancel, TapTicks);
                Taps++;
                cancels++;
                uint before = state.ScreenId;
                try
                {
                    WaitFor(s => !s.Valid || s.ScreenId != before,
                            Math.Min(2.0, Math.Max(0.0, deadline - Clock())));
                }
                catch (MenuTimeout) { }
                continue;
            }
            else if (!state.Valid && ViewTitleDemo() == true)
            {
                if (demoTaps >= maxCancels)
                {
                    throw new MenuLost("タイトルデモに OK を " + demoTaps
                                       + " 回送っても抜けられません（最後の状態: " + state.Text()
                                       + "）。手で操作してタイトルへ戻してください。");
                }
                Log("タイトルデモを検出しました。OK(Z) を送って抜けます（" + (demoTaps + 1)
                    + "/" + maxCancels + "）");
                Sink.Tap(MenuMapLayout.BitOk, TapTicks);
                Taps++;
                demoTaps++;
                try
                {
                    WaitFor(s => s.Valid, Math.Min(2.0, Math.Max(0.0, deadline - Clock())));
                }
                catch (MenuTimeout) { }
                continue;
            }
            if (Clock() >= deadline)
            {
                throw new MenuLost("タイトルへ戻れないまま " + FormatG(timeout)
                                   + " 秒が過ぎました（最後の状態: " + state.Text() + "）");
            }
            Sleep(Poll);
        }
    }

    public MenuState EnterReplayList(double timeout = 30.0)
    {
        MenuState state = EnsureTitle();
        int guard = MenuMapLayout.TitleCursorMax + 2;
        while (state.Cursor != MenuMapLayout.TitleItemReplay)
        {
            if (guard <= 0)
                throw new MenuTimeout("タイトルのカーソルを REPLAY(" + MenuMapLayout.TitleItemReplay + ") へ寄せられません");
            guard--;
            bool up = state.Cursor > MenuMapLayout.TitleItemReplay;
            uint key = up ? MenuMapLayout.BitUp : MenuMapLayout.BitDown;
            uint want = up ? state.Cursor - 1 : state.Cursor + 1;
            state = Commit(key, s => s.Valid && s.ScreenId == MenuMapLayout.ScreenTitle && s.Cursor == want,
                           timeout: 5.0);
        }
        Commit(MenuMapLayout.BitOk, s => s.Valid && s.ScreenId == MenuMapLayout.ScreenReplayList, timeout);
        state = WaitReady(MenuMapLayout.ScreenReplayList, timeout);
        Log("リプレイ選択画面に入りました（cursor=" + state.Cursor + "）");
        return state;
    }

    public MenuState LeaveReplayList(double timeout = 30.0)
    {
        MenuState state = Observe();
        if (!state.Valid) state = WaitFor(s => s.Valid, timeout);
        if (state.ScreenId != MenuMapLayout.ScreenReplayList)
            throw new UnexpectedScreen("リプレイ選択画面に居ません: " + state.Text());
        if (state.Substate == MenuMapLayout.SubDetail)
            Commit(MenuMapLayout.BitCancel, s => s.Valid && s.Substate != MenuMapLayout.SubDetail, timeout: 10.0);
        Commit(MenuMapLayout.BitCancel, s => s.Valid && s.ScreenId == MenuMapLayout.ScreenTitle, timeout);
        state = WaitReady(MenuMapLayout.ScreenTitle, timeout);
        if (state.Cursor != MenuMapLayout.TitleItemReplay)
            Log("注意: タイトルの cursor が " + state.Cursor + " です（3 に戻るはず）");
        return state;
    }


    public MenuState MoveCursorTo(int cell, int maxSteps = 60)
    {
        if (cell < 0 || cell >= MenuMapLayout.CellCount)
            throw new ArgumentException("セル番号は 0.." + (MenuMapLayout.CellCount - 1) + " です: " + cell);
        MenuState state = WaitReady(MenuMapLayout.ScreenReplayList, 10.0);
        if (state.Substate != MenuMapLayout.SubReady)
            throw new UnexpectedScreen("一覧が操作可能ではありません: " + state.Text());
        int steps = 0;
        while (state.Cursor != (uint)cell)
        {
            if (steps >= maxSteps)
                throw new MenuTimeout("カーソルを " + cell + " へ動かせません（" + steps
                                      + " 手で打ち切り、現在 " + state.Cursor + "）");
            uint key = MenuMap.CursorPath((int)state.Cursor, cell)[0];
            uint want = (uint)MenuMap.ApplyKey((int)state.Cursor, key);
            state = Commit(key, s => s.Valid && s.ScreenId == MenuMapLayout.ScreenReplayList
                                     && s.Substate == MenuMapLayout.SubReady && s.Cursor == want,
                           timeout: 5.0);
            steps++;
        }
        return state;
    }

    public MenuState OpenCell(int cell, double timeout = 10.0)
    {
        if (!View.CellOccupied(cell))
        {
            int? slot = MenuMap.SlotOfCell(cell);
            string where = slot is null ? "ud 枠" : "th9_" + slot.Value.ToString("D2") + ".rpy";
            throw new EmptyCell("セル " + cell + "（" + where + "）は空です（slot_ptrs が NULL）。開きません。");
        }
        MoveCursorTo(cell);
        MenuState state = Commit(MenuMapLayout.BitOk,
                                 s => s.Valid && s.ScreenId == MenuMapLayout.ScreenReplayList
                                      && s.Substate == MenuMapLayout.SubDetail, timeout);
        if (state.Chosen != (uint)cell)
            Log("注意: chosen_cell=" + state.Chosen + " が指定セル " + cell + " と違います");
        return state;
    }

    public MenuState SelectStartStage(int target, double timeout = 10.0)
    {
        MenuState state = Observe();
        if (!(state.Valid && state.Substate == MenuMapLayout.SubDetail))
            throw new UnexpectedScreen("概要画面に居ません: " + state.Text());
        int guard = MenuMapLayout.StageSlots + 2;
        while (state.Cursor != (uint)target)
        {
            if (guard <= 0)
                throw new MenuTimeout("開始面カーソルを " + target + " へ寄せられません（現在 " + state.Cursor + "）");
            guard--;
            bool up = state.Cursor > (uint)target;
            uint key = up ? MenuMapLayout.BitUp : MenuMapLayout.BitDown;
            uint want = up ? state.Cursor - 1 : state.Cursor + 1;
            state = Commit(key, s => s.Valid && s.Substate == MenuMapLayout.SubDetail && s.Cursor == want,
                           timeout);
        }
        Log("開始面カーソルを " + target + " に合わせました。");
        return state;
    }

    private bool ViewReplayPlaying()
    {
        try
        {
            return View.ReplayPlaying();
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool? ViewTitleDemo()
    {
        try
        {
            return View.TitleDemo();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private bool SlotReleased(uint cell)
    {
        if (cell >= MenuMapLayout.CellCount) return false;
        try
        {
            return !View.CellOccupied((int)cell);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private string PlaybackStarted(MenuState s, uint cell)
    {
        if (!s.Valid) return "メニューが読めなくなった";
        if (s.Substate != MenuMapLayout.SubDetail) return "概要画面から抜けた";
        if (SlotReleased(cell)) return "slot_ptrs[" + cell + "] が NULL 化された";
        if (ViewReplayPlaying()) return "ReplayManager が再生中";
        return "";
    }

    public virtual void StartPlayback(double timeout = 10.0)
    {
        MenuState state = Observe();
        if (!(state.Valid && state.Substate == MenuMapLayout.SubDetail))
            throw new UnexpectedScreen("概要画面に居ません: " + state.Text());
        uint cell = state.Chosen;
        var why = new List<string>();

        bool Started(MenuState s)
        {
            string reason = PlaybackStarted(s, cell);
            if (reason.Length != 0) why.Add(reason);
            return reason.Length != 0;
        }

        Commit(MenuMapLayout.BitOk, Started, timeout);
        Log("再生を開始しました（" + (why.Count > 0 ? why[0] : "根拠不明")
            + "。以降の観測は Tick Bus 側で行うこと）");
    }

    public const double RenewSec = 10.0;

    public const int StableReads = 5;

    public void ResetListReturn()
    {
        _leftMenu = false;
        _listStable = 0;
    }

    public bool ListReturned(MenuState? state = null)
    {
        MenuState st = state ?? Observe();
        if (!st.Valid)
        {
            _leftMenu = true;
            _listStable = 0;
            return false;
        }
        if (!_leftMenu) return false;
        if (!(st.ScreenId == MenuMapLayout.ScreenReplayList && st.Substate == MenuMapLayout.SubReady
              && !st.Transitioning))
        {
            _listStable = 0;
            return false;
        }
        _listStable++;
        return _listStable >= StableReads;
    }

    public virtual MenuState WaitListAgain(double timeout = 900.0)
    {
        double deadline = Clock() + Math.Max(0.0, timeout);
        double nextRenew = Clock() + RenewSec;
        ResetListReturn();
        while (Clock() < deadline)
        {
            if (ListReturned()) return Last!.Value;
            double now = Clock();
            if (now >= nextRenew)
            {
                if (!Sink.Renew())
                {
                    Log("警告: skip 保持の再送(renew)が ack されませんでした" + HoldFailureReason()
                        + " 次の周回で再送します。");
                }
                nextRenew = now + RenewSec;
            }
            Sleep(Poll);
        }
        throw new MenuTimeout("再生が " + timeout.ToString("F0") + " 秒で終わりませんでした（最後の状態: "
                              + (Last is null ? "未観測" : Last.Value.Text()) + "）");
    }


    private string HoldFailureReason()
    {
        uint? state;
        try
        {
            state = Sink.HookState();
        }
        catch (Exception)
        {
            return "";
        }
        if (state is null) return "";
        string text = TickWords.HookStates.Text(state.Value);
        if (state.Value == TickWords.HookStates.Idle || state.Value == TickWords.HookStates.Armed)
        {
            return "（hook_state=" + text + ": フックが刺さっていません。"
                   + "th09_inject.exe --run で RUNNING にしてください）";
        }
        return "（hook_state=" + text + "）";
    }

    public void HoldSkip()
    {
        bool acked = Sink.SetHold(MenuMapLayout.BitSkip, SkipFields);
        if (!acked)
            throw new SkipHoldFailed("skip の保持が ack されませんでした" + HoldFailureReason());
    }

    public void ReleaseSkip()
    {
        if (!Sink.SetHold(0))
            Log("警告: skip 保持の解除が ack されませんでした" + HoldFailureReason());
    }

    private static string FormatG(double value) => value.ToString("G6");
}
