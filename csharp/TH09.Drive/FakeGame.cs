using TH09.Generated;

namespace TH09.Drive;

public sealed class FakeGame : IMenuView, IInputSink
{
    private readonly MtRandom _rng;
    private MenuState? _frozen;
    private uint _lastKey;
    private int _lastKeyFrame = -10000;
    private int _savedCursor;
    private int _playbackLeft;

    public Dictionary<int, string> Files { get; }

    public Dictionary<int, string> Cells { get; private set; } = [];

    public int AnimFrames { get; }

    public int PlaybackFrames { get; }

    public int GapFrames { get; }

    public bool HonorGap { get; }

    public double DropRate { get; }

    public int TitleItems { get; }

    public uint BaseAddr { get; }

    public bool StaleBlock { get; }

    public bool ClearSlots { get; }

    public int Frame { get; private set; }

    public int Screen { get; private set; }

    public int Substate { get; private set; }

    public int Cursor { get; private set; }

    public int Anim { get; private set; }

    public uint Transitioning { get; private set; }

    public int Chosen { get; private set; }

    public uint Hold { get; private set; }

    public uint HoldFields { get; private set; }

    public int DemoFramesLeft { get; private set; }

    public bool? DemoKnown { get; private set; } = true;


    public int Taps { get; private set; }

    public int TapsWhileInvalid { get; private set; }

    public int TapsWhileEntering { get; private set; }

    public int CancelsOnTitle { get; private set; }

    public int DroppedEntering { get; private set; }

    public int DroppedGap { get; private set; }

    public int DroppedRandom { get; private set; }

    public int DroppedForced { get; private set; }

    public int DroppedDemo { get; private set; }

    public int DemoOkTaps { get; private set; }

    public int DropNext { get; set; }

    public int Playbacks { get; private set; }

    public int RejectedOpens { get; private set; }

    public List<uint> Applied { get; } = [];

    public FakeGame(int screen = MenuMapLayout.ScreenTitle,
                    IDictionary<int, string>? cells = null,
                    int animFrames = 8, int playbackFrames = 20,
                    int gapFrames = 2, bool honorGap = true,
                    double dropRate = 0.0, int seed = 12345,
                    int titleItems = 8, uint baseAddr = 0x0AB00000u,
                    bool entering = false, int cursor = 0,
                    bool staleBlock = false, bool clearSlots = true)
    {
        _ = entering;
        _ = cursor;
        Files = cells is null ? [] : new Dictionary<int, string>(cells);
        AnimFrames = animFrames;
        PlaybackFrames = playbackFrames;
        GapFrames = gapFrames;
        HonorGap = honorGap;
        DropRate = dropRate;
        TitleItems = titleItems;
        BaseAddr = baseAddr;
        _rng = new MtRandom(seed);
        StaleBlock = staleBlock;
        ClearSlots = clearSlots;

        Screen = screen;
        Substate = MenuMapLayout.SubReady;
        Cursor = 0;

        if (screen == MenuMapLayout.ScreenReplayList) Enumerate();
        EnterScreen(screen, Cursor, animate: false);
    }


    public bool Valid => _playbackLeft <= 0 && DemoFramesLeft <= 0;

    public void StartDemo(int frames = 1000000000, bool? known = true)
    {
        DemoFramesLeft = frames;
        DemoKnown = known;
    }

    public string? CellLabel(int cell) => Cells.TryGetValue(cell, out string? name) ? name : null;

    private void Enumerate() => Cells = new Dictionary<int, string>(Files);

    private void EnterScreen(int screen, int cursor = 0, bool animate = true)
    {
        Screen = screen;
        Cursor = cursor;
        Chosen = 0;
        if (screen == MenuMapLayout.ScreenReplayList) Enumerate();
        if (animate)
        {
            Substate = MenuMapLayout.SubEntering;
            Anim = AnimFrames;
            Transitioning = 1u;
        }
        else
        {
            Substate = MenuMapLayout.SubReady;
            Anim = 0;
            Transitioning = 0u;
        }
    }

    private void Tick()
    {
        Frame++;
        if (DemoFramesLeft > 0)
        {
            DemoFramesLeft--;
            if (DemoFramesLeft == 0)
            {
                EnterScreen(MenuMapLayout.ScreenTitle, cursor: 0);
            }
            return;
        }
        if (_playbackLeft > 0)
        {
            _playbackLeft--;
            if (_playbackLeft == 0)
            {
                EnterScreen(MenuMapLayout.ScreenReplayList, cursor: 0);
            }
            return;
        }
        if (Substate == MenuMapLayout.SubEntering)
        {
            if (Anim > 0) Anim--;
            if (Anim == 0)
            {
                Substate = MenuMapLayout.SubReady;
                Transitioning = 0u;
            }
        }
    }


    public MenuState Read()
    {
        Tick();
        if (!Valid)
        {
            if (StaleBlock && _frozen is not null)
            {
                return _frozen.Value;
            }
            int garbageCursor = _rng.Choice([0, 3, 61234]);
            int garbageSubstate = _rng.Choice([0, 1, 7]);
            int garbageScreen = _rng.Choice([4, 5, 8223]);
            return MenuMap.MakeState(BaseAddr, (uint)garbageCursor, (uint)garbageSubstate,
                                     (uint)garbageScreen, 0u, 0x3F800000u, 0u);
        }
        return MenuMap.MakeState(BaseAddr, (uint)Cursor, (uint)Substate, (uint)Screen,
                                 (uint)Anim, Transitioning, (uint)Chosen);
    }

    public bool ReplayPlaying() => _playbackLeft > 0;

    public bool? TitleDemo()
    {
        if (DemoFramesLeft <= 0) return false;
        return DemoKnown;
    }

    public bool CellOccupied(int cell)
    {
        if (cell < 0 || cell >= MenuMapLayout.CellCount)
            throw new ArgumentException("セル番号は 0.." + (MenuMapLayout.CellCount - 1) + " です: " + cell);
        if (!Valid)
        {
            return ClearSlots ? false : Cells.ContainsKey(cell);
        }
        return Cells.ContainsKey(cell);
    }


    public bool Tap(uint mask, int ticks = 0)
    {
        _ = ticks;
        Taps++;
        if (!Valid) TapsWhileInvalid++;
        if (Screen == MenuMapLayout.ScreenTitle && (mask & MenuMapLayout.BitCancel) != 0) CancelsOnTitle++;
        if (Substate == MenuMapLayout.SubEntering || Transitioning != 0) TapsWhileEntering++;

        string? dropped = DropReason(mask);
        if (dropped is null) Apply(mask);
        _lastKey = mask;
        _lastKeyFrame = Frame;
        if (HonorGap)
        {
            for (int i = 0; i < GapFrames; i++) Tick();
        }
        return dropped is null;
    }

    public bool SetHold(uint mask, uint fields = 0)
    {
        Hold = mask;
        HoldFields = mask != 0 ? fields : 0u;
        return true;
    }

    public bool ReleaseAll()
    {
        Hold = 0u;
        HoldFields = 0u;
        LockInput = false;
        _lastKey = 0u;
        _lastKeyFrame = -10000;
        return true;
    }

    public bool LockInput { get; set; }

    private string? DropReason(uint mask)
    {
        if (DropNext > 0)
        {
            DropNext--;
            DroppedForced++;
            return "forced";
        }
        if (DemoFramesLeft > 0)
        {
            if ((mask & MenuMapLayout.BitOk) != 0) return null;
            DroppedDemo++;
            return "demo";
        }
        if (!Valid) return "playback";
        if (Substate == MenuMapLayout.SubEntering || Transitioning != 0)
        {
            DroppedEntering++;
            return "entering";
        }
        if (mask == _lastKey && Frame - _lastKeyFrame < GapFrames)
        {
            DroppedGap++;
            return "gap";
        }
        if (DropRate > 0.0 && _rng.NextDouble() < DropRate)
        {
            DroppedRandom++;
            return "random";
        }
        return null;
    }

    private void Apply(uint mask)
    {
        Applied.Add(mask);
        if (DemoFramesLeft > 0 && (mask & MenuMapLayout.BitOk) != 0)
        {
            DemoOkTaps++;
            DemoFramesLeft = 0;
            EnterScreen(MenuMapLayout.ScreenTitle, cursor: 0);
            return;
        }
        if (Screen == MenuMapLayout.ScreenReplayList) ApplyList(mask);
        else if (Screen == MenuMapLayout.ScreenTitle) ApplyTitle(mask);
        else
        {
            if ((mask & MenuMapLayout.BitCancel) != 0) EnterScreen(MenuMapLayout.ScreenTitle, cursor: 0);
        }
    }

    private void ApplyTitle(uint mask)
    {
        if ((mask & MenuMapLayout.BitUp) != 0) Cursor = Math.Max(0, Cursor - 1);
        else if ((mask & MenuMapLayout.BitDown) != 0) Cursor = Math.Min(TitleItems - 1, Cursor + 1);
        else if ((mask & MenuMapLayout.BitOk) != 0)
        {
            if (Cursor == MenuMapLayout.TitleItemReplay)
                EnterScreen(MenuMapLayout.ScreenReplayList, cursor: 0);
        }
    }

    private void ApplyList(uint mask)
    {
        if (Substate == MenuMapLayout.SubDetail)
        {
            if ((mask & MenuMapLayout.BitCancel) != 0)
            {
                Substate = MenuMapLayout.SubReady;
                Cursor = _savedCursor;
                Chosen = 0;
            }
            else if ((mask & MenuMapLayout.BitOk) != 0)
            {
                Playbacks++;
                _playbackLeft = PlaybackFrames;
                _frozen = MenuMap.MakeState(BaseAddr, (uint)Cursor, (uint)Substate, (uint)Screen,
                                            (uint)Anim, Transitioning, (uint)Chosen);
            }
            return;
        }
        if ((mask & MenuMapLayout.BitCancel) != 0)
        {
            EnterScreen(MenuMapLayout.ScreenTitle, cursor: MenuMapLayout.TitleItemReplay);
            return;
        }
        if ((mask & MenuMapLayout.BitOk) != 0)
        {
            if (Cells.ContainsKey(Cursor))
            {
                _savedCursor = Cursor;
                Chosen = Cursor;
                Substate = MenuMapLayout.SubDetail;
            }
            else
            {
                RejectedOpens++;
            }
            return;
        }
        Cursor = MenuMap.ApplyKey(Cursor, mask);
    }
}
