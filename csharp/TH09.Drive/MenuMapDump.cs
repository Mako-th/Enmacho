using System.Text;
using TH09.Generated;

namespace TH09.Drive;

internal sealed class SyntheticMemory : IMenuMemory
{
    public Dictionary<uint, uint> Words { get; } = [];

    public Dictionary<uint, byte[]> Blobs { get; } = [];

    public static string FailText(uint address) => "読めません @" + MenuMap.Hex8(address);

    public bool TryReadUInt32(uint address, out uint value, out string error)
    {
        if (Words.TryGetValue(address, out value))
        {
            error = "";
            return true;
        }
        value = 0;
        error = FailText(address);
        return false;
    }

    public bool TryReadBytes(uint address, int size, out byte[] value, out string error)
    {
        if (Blobs.TryGetValue(address, out byte[]? blob))
        {
            int take = Math.Min(size, blob.Length);
            value = blob[..take];
            error = "";
            return true;
        }
        value = [];
        error = FailText(address);
        return false;
    }

    public SyntheticMemory Word(uint address, uint value)
    {
        Words[address] = value;
        return this;
    }

    public SyntheticMemory Blob(uint address, byte[] value)
    {
        Blobs[address] = value;
        return this;
    }
}

internal static class MenuMapDump
{
    private static readonly string Tab = ((char)9).ToString();

    private static void Row(TextWriter w, params string[] fields) => w.WriteLine(string.Join(Tab, fields));

    private static string S(uint v) => v.ToString();

    private static string S(int v) => v.ToString();

    private static string B(bool v) => v ? "1" : "0";

    private static string Join(IEnumerable<uint> values) => string.Join(",", values);


    private static readonly uint[] ScreenGrid =
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 8223, 0xFFFFFFFFu];

    private static readonly uint[] SubstateGrid = [0, 1, 2, 3, 8223];

    private static readonly uint[] CursorGrid = [0, 1, 15, 16, 24, 25, 49, 50, 8223];

    private static readonly uint[] TransitionGrid = [0, 1, 2, 0xFFFFFFFFu];

    private static readonly uint[] MakeScreens = [0, 1, 4, 5, 11, 12, 16, 17, 8223];

    private static readonly uint[] MakeCursors = [0, 3, 15, 16, 49, 50];

    private static readonly uint[] MakeTransitions = [0, 1, 2];

    private static uint[] BaseGrid() =>
    [
        0,
        MenuMapLayout.PtrMin - 1,
        MenuMapLayout.PtrMin,
        MenuMapLayout.PtrMax,
        MenuMapLayout.PtrMax + 1,
    ];

    private static uint LiveBase => MenuMapLayout.PtrMin + 0x0F113400u;

    public static int Run(TextWriter w)
    {
        DumpConstants(w);
        DumpTables(w);
        DumpPointer(w);
        DumpCursorPath(w);
        DumpApplyKey(w);
        DumpCells(w);
        DumpStateProblems(w);
        DumpMakeState(w);
        DumpNames(w);
        DumpFirstStage(w);
        DumpDemoPath(w);
        DumpMemoryCases(w);
        return 0;
    }


    private static void DumpConstants(TextWriter w)
    {
        Row(w, "failtext", S(MenuMapLayout.MainMenuPtr), SyntheticMemory.FailText(MenuMapLayout.MainMenuPtr));

        Row(w, "const", "MAIN_MENU_PTR", S(MenuMapLayout.MainMenuPtr));
        Row(w, "const", "A_REPLAY_PATH", S(MenuMapLayout.AReplayPath));
        Row(w, "const", "REPLAY_MANAGER_PTR", S(GameAddresses.ReplayManagerPtr));
        Row(w, "const", "A_GLOBAL_STATE", S(GameAddresses.GlobalState));
        Row(w, "const", "CELL_COUNT", S(MenuMapLayout.CellCount));
        Row(w, "const", "NUMBERED_CELLS", S(MenuMapLayout.NumberedCells));
        Row(w, "const", "SLOT_HEADER_STRIDE", S(MenuMapLayout.SlotHeaderStride));
        Row(w, "const", "LATERAL_STEP", S(MenuMapLayout.LateralStep));
        Row(w, "const", "REPLAY_PATH_MAX", S(MenuMapLayout.ReplayPathMax));
        Row(w, "const", "STAGE_SLOTS", S(MenuMapLayout.StageSlots));
        Row(w, "const", "PTR_MIN", S(MenuMapLayout.PtrMin));
        Row(w, "const", "PTR_MAX", S(MenuMapLayout.PtrMax));
        Row(w, "const", "OFF_CURSOR", S(MenuMapLayout.OffCursor));
        Row(w, "const", "OFF_SUBSTATE", S(MenuMapLayout.OffSubstate));
        Row(w, "const", "OFF_PREV_SCREEN", S(MenuMapLayout.OffPrevScreen));
        Row(w, "const", "OFF_SLOT_PTRS", S(MenuMapLayout.OffSlotPtrs));
        Row(w, "const", "OFF_SLOT_HEADER", S(MenuMapLayout.OffSlotHeader));
        Row(w, "const", "OFF_CHOSEN_CELL", S(MenuMapLayout.OffChosenCell));
        Row(w, "const", "OFF_SCREEN_ID", S(MenuMapLayout.OffScreenId));
        Row(w, "const", "OFF_ANIM_STEP", S(MenuMapLayout.OffAnimStep));
        Row(w, "const", "OFF_TRANSITIONING", S(MenuMapLayout.OffTransitioning));
        Row(w, "const", "OFF_REPLAY_PLAYBACK_FLAG", S(MenuMapLayout.OffReplayPlaybackFlag));
        Row(w, "const", "OFF_STAGE_OFFSETS", S(MenuMapLayout.OffStageOffsets));
        Row(w, "const", "SCREEN_TITLE", S(MenuMapLayout.ScreenTitle));
        Row(w, "const", "SCREEN_REPLAY_LIST", S(MenuMapLayout.ScreenReplayList));
        Row(w, "const", "SCREEN_MUSIC_ROOM", S(MenuMapLayout.ScreenMusicRoom));
        Row(w, "const", "SCREEN_CHAR_SELECT", S(MenuMapLayout.ScreenCharSelect));
        Row(w, "const", "SCREEN_MIN", S(MenuMapLayout.ScreenMin));
        Row(w, "const", "SCREEN_MAX", S(MenuMapLayout.ScreenMax));
        Row(w, "const", "TITLE_ITEM_REPLAY", S(MenuMapLayout.TitleItemReplay));
        Row(w, "const", "TITLE_CURSOR_MAX", S(MenuMapLayout.TitleCursorMax));
        Row(w, "const", "SUB_ENTERING", S(MenuMapLayout.SubEntering));
        Row(w, "const", "SUB_READY", S(MenuMapLayout.SubReady));
        Row(w, "const", "SUB_DETAIL", S(MenuMapLayout.SubDetail));
        Row(w, "const", "SUB_MAX", S(MenuMapLayout.SubMax));
        Row(w, "const", "BIT_OK", S(MenuMapLayout.BitOk));
        Row(w, "const", "BIT_CANCEL", S(MenuMapLayout.BitCancel));
        Row(w, "const", "BIT_UP", S(MenuMapLayout.BitUp));
        Row(w, "const", "BIT_DOWN", S(MenuMapLayout.BitDown));
        Row(w, "const", "BIT_LEFT", S(MenuMapLayout.BitLeft));
        Row(w, "const", "BIT_RIGHT", S(MenuMapLayout.BitRight));
        Row(w, "const", "BIT_SKIP", S(MenuMapLayout.BitSkip));
        Row(w, "const", "GS_TITLE_DEMO", S(MenuMapLayout.GsTitleDemo));
        Row(w, "const", "GS_IN_GAME_STICKY", S(MenuMapLayout.GsInGameSticky));
        Row(w, "const", "GS_REPLAY_PLAYING", S(MenuMapLayout.GsReplayPlaying));
        Row(w, "const", "GS_FREEZE_P1", S(MenuMapLayout.GsFreezeP1));
        Row(w, "const", "GS_FREEZE_P2", S(MenuMapLayout.GsFreezeP2));
        Row(w, "const", "GS_CONTINUED", S(MenuMapLayout.GsContinued));
        Row(w, "const", "GS_CONTINUED_LEVEL_FREEZE", S(MenuMapLayout.GsContinuedLevelFreeze));
        Row(w, "const", "GF_TIME_STOP", S(MenuMapLayout.GfTimeStop));
        Row(w, "conststr", "DEMO_PATH_MARK", MenuMapLayout.DemoPathMark);
    }

    private static void DumpTables(TextWriter w)
    {
        for (int i = 0; i < MenuMapLayout.Regions.Length; i++)
        {
            var (off, name, size) = MenuMapLayout.Regions[i];
            Row(w, "region", S(i), S(off), name, S(size));
        }
        for (int i = 0; i < MenuMapLayout.ScreenNames.Length; i++)
        {
            var (id, name) = MenuMapLayout.ScreenNames[i];
            Row(w, "table", "SCREEN_NAMES", S(i), S(id), name);
        }
        for (int i = 0; i < MenuMapLayout.SubstateNames.Length; i++)
        {
            var (id, name) = MenuMapLayout.SubstateNames[i];
            Row(w, "table", "SUBSTATE_NAMES", S(i), S(id), name);
        }
        for (int i = 0; i < MenuMapLayout.BitNames.Length; i++)
        {
            var (bit, name) = MenuMapLayout.BitNames[i];
            Row(w, "table", "BIT_NAMES", S(i), S(bit), name);
        }
        for (int i = 0; i < MenuMapLayout.GlobalStateBitNames.Length; i++)
        {
            var (bit, name) = MenuMapLayout.GlobalStateBitNames[i];
            Row(w, "table", "GLOBAL_STATE_BIT_NAMES", S(i), S(bit), name);
        }
        for (int i = 0; i < MenuMapLayout.GameFlagsBitNames.Length; i++)
        {
            var (bit, name) = MenuMapLayout.GameFlagsBitNames[i];
            Row(w, "table", "GAME_FLAGS_BIT_NAMES", S(i), S(bit), name);
        }
    }


    private static void DumpPointer(TextWriter w)
    {
        uint[] probes =
        [
            0, 1, MenuMapLayout.PtrMin - 1, MenuMapLayout.PtrMin, MenuMapLayout.PtrMin + 1,
            0x00400000u, LiveBase, MenuMapLayout.PtrMax - 1, MenuMapLayout.PtrMax,
            MenuMapLayout.PtrMax + 1, 0x80000000u, 0xFFFFFFFFu,
        ];
        foreach (uint p in probes)
            Row(w, "ptr_valid", S(p), B(MenuMap.PointerLooksValid(p)));
    }

    private static void DumpCursorPath(TextWriter w)
    {
        for (int src = 0; src < MenuMapLayout.CellCount; src++)
            for (int dst = 0; dst < MenuMapLayout.CellCount; dst++)
                Row(w, "cursor_path", S(src), S(dst), Join(MenuMap.CursorPath(src, dst)));

        foreach (int bad in new[] { -1, MenuMapLayout.CellCount, MenuMapLayout.CellCount + 1 })
        {
            Row(w, "cursor_path_err", S(bad), S(0), Message(() => MenuMap.CursorPath(bad, 0)));
            Row(w, "cursor_path_err", S(0), S(bad), Message(() => MenuMap.CursorPath(0, bad)));
        }
    }

    private static void DumpApplyKey(TextWriter w)
    {
        var bits = new List<uint>();
        foreach (var (bit, _) in MenuMapLayout.BitNames) bits.Add(bit);
        bits.Add(0);
        bits.Add(0x0200u);
        bits.Add(MenuMapLayout.BitUp | MenuMapLayout.BitDown);
        for (int cursor = 0; cursor < MenuMapLayout.CellCount; cursor++)
            foreach (uint bit in bits)
                Row(w, "apply_key", S(cursor), S(bit), S(MenuMap.ApplyKey(cursor, bit)));
    }

    private static void DumpCells(TextWriter w)
    {
        for (int slot = -2; slot <= MenuMapLayout.NumberedCells + 2; slot++)
        {
            try
            {
                Row(w, "cell_of_slot", S(slot), "ok", S(MenuMap.CellOfSlot(slot)));
            }
            catch (ArgumentException exc)
            {
                Row(w, "cell_of_slot", S(slot), "err", exc.Message);
            }
        }
        for (int cell = -2; cell <= MenuMapLayout.CellCount + 2; cell++)
        {
            try
            {
                int? slot = MenuMap.SlotOfCell(cell);
                Row(w, "slot_of_cell", S(cell), slot is null ? "none" : "ok",
                    slot is null ? "" : S(slot.Value));
            }
            catch (ArgumentException exc)
            {
                Row(w, "slot_of_cell", S(cell), "err", exc.Message);
            }
            Row(w, "is_numbered", S(cell), B(MenuMap.IsNumberedCell(cell)));
        }
    }

    private static void DumpStateProblems(TextWriter w)
    {
        foreach (uint screen in ScreenGrid)
            foreach (uint substate in SubstateGrid)
                foreach (uint cursor in CursorGrid)
                    foreach (uint trans in TransitionGrid)
                        foreach (uint basePtr in BaseGrid())
                        {
                            var bad = MenuMap.StateProblems(screen, substate, cursor, trans, basePtr);
                            Row(w, "state_problems", S(screen), S(substate), S(cursor), S(trans),
                                S(basePtr), string.Join(" / ", bad));
                        }

        foreach (uint screen in MakeScreens)
        {
            var bad = MenuMap.StateProblems(screen, MenuMapLayout.SubReady, 0, 0);
            Row(w, "state_problems_default", S(screen), string.Join(" / ", bad));
        }
    }

    private static void DumpMakeState(TextWriter w)
    {
        int i = 0;
        foreach (uint screen in MakeScreens)
            foreach (uint substate in SubstateGrid)
                foreach (uint cursor in MakeCursors)
                    foreach (uint trans in MakeTransitions)
                        foreach (uint basePtr in BaseGrid())
                        {
                            i++;
                            uint anim = (uint)(i * 7919 % 65537);
                            uint chosen = (uint)(i * 104729 % 4099);
                            var st = MenuMap.MakeState(basePtr, cursor, substate, screen,
                                                       anim, trans, chosen);
                            Row(w, "make_state", S(basePtr), S(cursor), S(substate), S(screen),
                                S(anim), S(trans), S(chosen),
                                B(st.Valid), B(st.Transitioning), B(st.IsListReady()),
                                st.Reason, st.Text());
                        }

        Row(w, "invalid_state", "", S(0), MenuMap.InvalidState("").Text());
        Row(w, "invalid_state", "理由の字", S(LiveBase),
            MenuMap.InvalidState("理由の字", LiveBase).Text());
    }

    private static void DumpNames(TextWriter w)
    {
        uint[] screenProbes = [0, 1, 2, 10, 11, 12, 13, 14, 16, 17, 8223, 0xFFFFFFFFu];
        foreach (uint id in screenProbes) Row(w, "screen_name", S(id), MenuMap.ScreenName(id));

        uint[] subProbes = [0, 1, 2, 3, 5, 8223, 0xFFFFFFFFu];
        foreach (uint id in subProbes) Row(w, "substate_name", S(id), MenuMap.SubstateName(id));

        var keyProbes = new List<uint> { 0, 0x0002u, 0x0004u, 0x0200u, 0xFFFFu, 0x12345u };
        foreach (var (bit, _) in MenuMapLayout.BitNames) keyProbes.Add(bit);
        foreach (uint bit in keyProbes) Row(w, "key_name", S(bit), MenuMap.KeyName(bit));

        uint[][] keyLists =
        [
            [],
            [MenuMapLayout.BitOk],
            [MenuMapLayout.BitDown, MenuMapLayout.BitDown, MenuMapLayout.BitRight],
            [MenuMapLayout.BitUp, 0x0200u, MenuMapLayout.BitSkip],
            MenuMap.CursorPath(3, 41),
            MenuMap.CursorPath(49, 0),
        ];
        foreach (uint[] keys in keyLists) Row(w, "keys_text", Join(keys), MenuMap.KeysText(keys));
        Row(w, "keys_text_null", "", MenuMap.KeysText(null));
    }

    private static void DumpFirstStage(TextWriter w)
    {
        uint[]?[] samples =
        [
            null,
            [],
            [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            [17, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            [0, 0, 0, 4423, 0, 0, 0, 0, 0, 0],
            [0, 0, 0, 0, 0, 0, 0, 0, 0, 1],
            [0, 0, 0, 0, 0, 0, 0, 0, 0, 0xFFFFFFFFu],
            [0, 5],
        ];
        foreach (uint[]? offsets in samples)
            Row(w, "first_stage", offsets is null ? "none" : "ok",
                offsets is null ? "" : Join(offsets), S(MenuMap.FirstPlayableStage(offsets)));
    }

    private static void DumpDemoPath(TextWriter w)
    {
        string bs = ((char)92).ToString();
        string?[] paths =
        [
            null,
            "",
            "demo/demorpy0.rpy",
            "demo/demorpy2.rpy",
            "./replay/th9_07.rpy",
            "." + bs + "replay" + bs + "th9_07.rpy",
            "." + bs + "DEMO" + bs + "demorpy1.rpy",
            "DEMO/demorpy1.rpy",
            "demo",
            "xdemo/y",
            "replay/demo",
            "/demo/",
        ];
        foreach (string? path in paths)
        {
            bool? demo = MenuMap.PathLooksLikeDemo(path);
            Row(w, "demo_path", path is null ? "none" : "ok", path ?? "",
                demo is null ? "none" : B(demo.Value));
        }

        uint?[] states = [null, 0, 2, 8, 14, 0x0004u, 0x4000u, 0x7FFEu, 0xFFFFFFFFu];
        foreach (uint? state in states)
            Row(w, "global_state_text", state is null ? "none" : S(state.Value),
                MenuMap.GlobalStateText(state));
    }


    private static void DumpMemoryCases(TextWriter w)
    {
        var cases = new List<(string Name, SyntheticMemory Mem)>();

        SyntheticMemory Add(string name)
        {
            var mem = new SyntheticMemory();
            cases.Add((name, mem));
            return mem;
        }

        uint live = LiveBase;
        uint ptr = MenuMapLayout.MainMenuPtr;
        uint altPtr = MenuMapLayout.MainMenuPtr + 4;

        var rsCases = new List<(string Name, uint BaseAddr)>();

        void Fields(SyntheticMemory mem, uint basePtr, uint cursor, uint substate, uint screen,
                    uint anim, uint trans, uint chosen, string? skip)
        {
            if (skip != "cursor") mem.Word(basePtr + (uint)MenuMapLayout.OffCursor, cursor);
            if (skip != "substate") mem.Word(basePtr + (uint)MenuMapLayout.OffSubstate, substate);
            if (skip != "screen_id") mem.Word(basePtr + (uint)MenuMapLayout.OffScreenId, screen);
            if (skip != "anim") mem.Word(basePtr + (uint)MenuMapLayout.OffAnimStep, anim);
            if (skip != "transitioning") mem.Word(basePtr + (uint)MenuMapLayout.OffTransitioning, trans);
            if (skip != "chosen") mem.Word(basePtr + (uint)MenuMapLayout.OffChosenCell, chosen);
        }

        Add("rs_ptr_unreadable");
        rsCases.Add(("rs_ptr_unreadable", ptr));
        Add("rs_ptr_zero").Word(ptr, 0);
        rsCases.Add(("rs_ptr_zero", ptr));
        Add("rs_ptr_low").Word(ptr, MenuMapLayout.PtrMin - 1);
        rsCases.Add(("rs_ptr_low", ptr));
        Add("rs_ptr_high").Word(ptr, MenuMapLayout.PtrMax + 1);
        rsCases.Add(("rs_ptr_high", ptr));
        Add("rs_fields_none").Word(ptr, live);
        rsCases.Add(("rs_fields_none", ptr));
        foreach (string skip in new[] { "cursor", "substate", "screen_id", "anim", "transitioning", "chosen" })
        {
            var mem = Add("rs_miss_" + skip).Word(ptr, live);
            Fields(mem, live, 7, MenuMapLayout.SubReady, MenuMapLayout.ScreenReplayList, 3, 0, 42, skip);
            rsCases.Add(("rs_miss_" + skip, ptr));
        }
        var good = Add("rs_good").Word(ptr, live);
        Fields(good, live, 7, MenuMapLayout.SubReady, MenuMapLayout.ScreenReplayList, 3, 0, 42, null);
        rsCases.Add(("rs_good", ptr));
        var junk = Add("rs_garbage").Word(ptr, live);
        Fields(junk, live, 60, 5, 8223, 0xFFFFFFFFu, 77, 0, null);
        rsCases.Add(("rs_garbage", ptr));
        var entering = Add("rs_entering").Word(ptr, live);
        Fields(entering, live, 24, MenuMapLayout.SubEntering, MenuMapLayout.ScreenReplayList, 1, 1, 0, null);
        rsCases.Add(("rs_entering", ptr));
        var alt = Add("rs_alt_base").Word(altPtr, live);
        Fields(alt, live, 12, MenuMapLayout.SubDetail, MenuMapLayout.ScreenTitle, 9, 0, 5, null);
        rsCases.Add(("rs_alt_base", altPtr));
        rsCases.Add(("rs_alt_base", ptr));

        uint rmPtr = GameAddresses.ReplayManagerPtr;
        uint rmBase = MenuMapLayout.PtrMin + 0x00223300u;
        uint flagAt = rmBase + (uint)MenuMapLayout.OffReplayPlaybackFlag;
        Add("rp_unreadable");
        Add("rp_zero").Word(rmPtr, 0);
        Add("rp_out_of_range").Word(rmPtr, MenuMapLayout.PtrMax + 1);
        Add("rp_flag_unreadable").Word(rmPtr, rmBase);
        Add("rp_flag_zero").Word(rmPtr, rmBase).Word(flagAt, 0);
        Add("rp_flag_one").Word(rmPtr, rmBase).Word(flagAt, 1);
        Add("rp_flag_two").Word(rmPtr, rmBase).Word(flagAt, 2);
        string[] rpCases =
        [
            "rp_unreadable", "rp_zero", "rp_out_of_range", "rp_flag_unreadable",
            "rp_flag_zero", "rp_flag_one", "rp_flag_two",
        ];

        uint gs = GameAddresses.GlobalState;
        uint pathAt = MenuMapLayout.AReplayPath;
        byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);
        byte[] Zero(string text, int total)
        {
            var buf = new byte[total];
            byte[] src = Ascii(text);
            Array.Copy(src, buf, Math.Min(src.Length, total));
            return buf;
        }

        Add("gs_unreadable");
        Add("gs_zero").Word(gs, 0);
        Add("gs_demo_no_path").Word(gs, 0x000Eu);
        Add("gs_demo_with_path").Word(gs, 0x000Eu).Blob(pathAt, Zero("demo/demorpy0.rpy", 64));
        Add("gs_demo_replay_path").Word(gs, 0x0002u).Blob(pathAt, Zero("./replay/th9_07.rpy", 64));
        Add("gs_replay_only").Word(gs, MenuMapLayout.GsReplayPlaying)
                             .Blob(pathAt, Zero("./replay/th9_25.rpy", 64));
        Add("gs_all").Word(gs, 0xFFFFFFFFu).Blob(pathAt, Zero("demo/demorpy2.rpy", 64));
        Add("gs_demo_empty_path").Word(gs, 0x000Eu).Blob(pathAt, new byte[64]);
        Add("gs_demo_nonascii").Word(gs, 0x000Eu).Blob(pathAt, [0x64, 0x65, 0x83, 0x40, 0x00]);
        Add("gs_demo_no_nul").Word(gs, 0x000Eu).Blob(pathAt, Ascii(new string('d', 64)));
        Add("gs_demo_short").Word(gs, 0x000Eu).Blob(pathAt, Ascii("demo/"));
        string[] gsCases =
        [
            "gs_unreadable", "gs_zero", "gs_demo_no_path", "gs_demo_with_path",
            "gs_demo_replay_path", "gs_replay_only", "gs_all", "gs_demo_empty_path",
            "gs_demo_nonascii", "gs_demo_no_nul", "gs_demo_short",
        ];

        int wantStage = 4 * MenuMapLayout.StageSlots;
        int wantPtrs = 4 * MenuMapLayout.CellCount;
        uint StageAddr(uint basePtr, int cell) =>
            basePtr + (uint)(MenuMapLayout.OffSlotHeader
                             + MenuMapLayout.SlotHeaderStride * cell
                             + MenuMapLayout.OffStageOffsets);

        byte[] Words32(params uint[] values)
        {
            var buf = new byte[4 * values.Length];
            for (int k = 0; k < values.Length; k++)
            {
                buf[4 * k] = (byte)(values[k] & 0xFF);
                buf[4 * k + 1] = (byte)((values[k] >> 8) & 0xFF);
                buf[4 * k + 2] = (byte)((values[k] >> 16) & 0xFF);
                buf[4 * k + 3] = (byte)((values[k] >> 24) & 0xFF);
            }
            return buf;
        }

        var so = Add("so_ok");
        so.Blob(StageAddr(live, 0), Words32(0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        so.Blob(StageAddr(live, 3), Words32(0, 0, 4423, 91, 0, 5, 0, 0, 0, 7));
        so.Blob(StageAddr(live, 24), Words32(19, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        so.Blob(StageAddr(live, 49), Words32(0, 0, 0, 0, 0, 0, 0, 0, 0, 0xFFFFFFFFu));
        var soShort = Add("so_short");
        soShort.Blob(StageAddr(live, 3), Words32(1, 2, 3, 4, 5));
        Add("so_missing");

        var co = Add("co_mixed");
        var ptrs = new uint[MenuMapLayout.CellCount];
        for (int cell = 0; cell < ptrs.Length; cell++)
        {
            ptrs[cell] = (cell % 3) switch
            {
                0 => 0,
                1 => (uint)(cell * 7 + 3),
                _ => MenuMapLayout.PtrMin + (uint)(cell * 0x1000),
            };
        }
        co.Blob(live + (uint)MenuMapLayout.OffSlotPtrs, Words32(ptrs));
        var coShort = Add("co_short");
        coShort.Blob(live + (uint)MenuMapLayout.OffSlotPtrs, new byte[wantPtrs - 4]);
        Add("co_missing");

        foreach (var (name, mem) in cases)
        {
            Row(w, "memcase", name, S(mem.Words.Count), S(mem.Blobs.Count));
            foreach (uint addr in mem.Words.Keys.Order())
                Row(w, "memu32", name, S(addr), S(mem.Words[addr]));
            foreach (uint addr in mem.Blobs.Keys.Order())
                Row(w, "membytes", name, S(addr), Convert.ToHexString(mem.Blobs[addr]));
        }

        var byName = new Dictionary<string, SyntheticMemory>(StringComparer.Ordinal);
        foreach (var (name, mem) in cases) byName[name] = mem;

        foreach (var (name, baseAddr) in rsCases)
        {
            var st = MenuMap.ReadState(byName[name], baseAddr);
            Row(w, "read_state", name, S(baseAddr), B(st.Valid), S(st.ScreenId), S(st.Substate),
                S(st.Cursor), S(st.Anim), B(st.Transitioning), S(st.Chosen), S(st.Base),
                B(st.IsListReady()), st.Reason, st.Text());
        }

        foreach (string name in rpCases)
            Row(w, "replay_playing", name, B(MenuMap.ReplayPlaying(byName[name])));

        foreach (string name in gsCases)
        {
            var mem = byName[name];
            uint? state = MenuMap.ReadGlobalState(mem);
            Row(w, "read_global_state", name, state is null ? "none" : S(state.Value));
            bool? demo = MenuMap.TitleDemoPlaying(mem);
            Row(w, "title_demo", name, demo is null ? "none" : B(demo.Value));

            foreach (bool withPath in new[] { true, false })
            {
                string? path = MenuMap.ReadReplayPath(withPath ? mem : null);
                Row(w, "read_path", name, B(withPath), path is null ? "none" : "ok", path ?? "");
                var check = MenuMap.CheckTitleDemo(mem, withPath ? mem : null);
                Row(w, "check_demo", name, B(withPath), B(check.Known), B(check.Demo),
                    check.State is null ? "none" : S(check.State.Value),
                    check.Path is null ? "none" : "ok", check.Path ?? "", check.Text());
            }
        }

        foreach (string name in new[] { "so_ok", "so_short", "so_missing" })
        {
            foreach (int cell in new[] { 0, 3, 24, 49, 1 })
            {
                uint[]? offsets = MenuMap.StageOffsets(byName[name], live, cell);
                Row(w, "stage_offsets", name, S(live), S(cell),
                    offsets is null ? "none" : "ok", offsets is null ? "" : Join(offsets),
                    S(MenuMap.FirstPlayableStage(offsets)));
            }
        }
        foreach (int bad in new[] { -1, MenuMapLayout.CellCount, MenuMapLayout.CellCount + 5 })
            Row(w, "stage_offsets_err", "so_ok", S(live), S(bad),
                Message(() => MenuMap.StageOffsets(byName["so_ok"], live, bad)));

        foreach (string name in new[] { "co_mixed", "co_short", "co_missing" })
        {
            bool[]? occupied = MenuMap.CellOccupancy(byName[name], live);
            Row(w, "cell_occupancy", name, S(live), occupied is null ? "none" : "ok",
                occupied is null ? "" : string.Concat(occupied.Select(o => o ? "1" : "0")),
                S(occupied is null ? -1 : occupied.Count(o => o)));
        }

        Row(w, "counts", "mem_cases", S(cases.Count));
        Row(w, "counts", "stage_bytes", S(wantStage));
        Row(w, "counts", "ptr_bytes", S(wantPtrs));
    }

    private static string Message(Func<object?> body)
    {
        try
        {
            body();
        }
        catch (ArgumentException exc)
        {
            return exc.Message;
        }
        return "（例外が出ませんでした）";
    }
}
