using System.Buffers.Binary;
using System.Text;
using TH09.Generated;

namespace TH09.Drive;

public sealed class MenuMapException : Exception
{
    public MenuMapException(string message) : base(message) { }
}

public readonly record struct MenuState(
    bool Valid, uint ScreenId, uint Substate, uint Cursor, uint Anim,
    bool Transitioning, uint Chosen, uint Base, string Reason)
{
    public bool IsListReady() =>
        Valid && ScreenId == MenuMapLayout.ScreenReplayList
              && Substate == MenuMapLayout.SubReady && !Transitioning;

    public string Text()
    {
        if (!Valid)
        {
            return "メニューは生きていません（再生中と思われる）: "
                 + Reason + " [base=" + MenuMap.Hex8(Base) + "]";
        }
        return "screen=" + ScreenId + "(" + MenuMap.ScreenName(ScreenId) + ")"
             + " substate=" + Substate + "(" + MenuMap.SubstateName(Substate) + ")"
             + " cursor=" + Cursor + " anim=" + Anim
             + " trans=" + (Transitioning ? 1 : 0) + " chosen=" + Chosen
             + " base=" + MenuMap.Hex8(Base);
    }
}

public readonly record struct DemoCheck(bool Known, bool Demo, uint? State = null, string? Path = null)
{
    public string Text()
    {
        var parts = new List<string>
        {
            "[" + MenuMap.Hex8(GameAddresses.GlobalState) + "]=" + MenuMap.GlobalStateText(State),
        };
        if (!string.IsNullOrEmpty(Path)) parts.Add("path=" + Path);
        string body = string.Join(" ", parts);
        if (!Known) return "タイトルデモか判定できません（" + body + "）";
        return (Demo ? "★タイトルデモ再生中" : "デモではありません") + "（" + body + "）";
    }
}

public static class MenuMap
{
    private const char Backslash = (char)92;

    internal static string Hex8(uint value) => "0x" + value.ToString("X8");

    internal static string Hex4(uint value) => "0x" + value.ToString("X4");

    private static int Mod(int value, int modulus)
    {
        int r = value % modulus;
        return r < 0 ? r + modulus : r;
    }


    public static MenuState InvalidState(string reason, uint basePtr = 0) =>
        new(false, 0, 0, 0, 0, false, 0, basePtr, reason);

    public static bool PointerLooksValid(uint ptr) =>
        MenuMapLayout.PtrMin <= ptr && ptr <= MenuMapLayout.PtrMax;

    public static List<string> StateProblems(uint screenId, uint substate, uint cursor,
                                             uint transitioning,
                                             uint basePtr = MenuMapLayout.PtrMin)
    {
        var bad = new List<string>();
        if (!PointerLooksValid(basePtr))
            bad.Add("ポインタが範囲外 (" + Hex8(basePtr) + ")");
        if (!(MenuMapLayout.ScreenMin <= screenId && screenId <= MenuMapLayout.ScreenMax))
            bad.Add("screen_id=" + screenId + " が " + MenuMapLayout.ScreenMin
                    + ".." + MenuMapLayout.ScreenMax + " の外");
        if (substate > MenuMapLayout.SubMax)
            bad.Add("substate=" + substate + " が 0.." + MenuMapLayout.SubMax + " の外");
        int limit = screenId == MenuMapLayout.ScreenReplayList
            ? MenuMapLayout.CellCount - 1
            : MenuMapLayout.TitleCursorMax;
        if (cursor > limit)
            bad.Add("cursor=" + cursor + " が 0.." + limit + " の外");
        if (transitioning != 0 && transitioning != 1)
            bad.Add("transitioning=" + transitioning + " が 0/1 以外");
        return bad;
    }

    public static MenuState MakeState(uint basePtr, uint cursor, uint substate, uint screenId,
                                      uint anim, uint transitioning, uint chosen)
    {
        var bad = StateProblems(screenId, substate, cursor, transitioning, basePtr);
        return new MenuState(bad.Count == 0, screenId, substate, cursor, anim,
                             transitioning != 0, chosen, basePtr, string.Join(" / ", bad));
    }

    public static MenuState ReadState(IMenuMemory mem, uint baseAddr = MenuMapLayout.MainMenuPtr)
    {
        if (!mem.TryReadUInt32(baseAddr, out uint basePtr, out string error))
            return InvalidState("ポインタを読めません (" + error + ")");
        if (!PointerLooksValid(basePtr))
            return InvalidState("ポインタが範囲外 (" + Hex8(basePtr) + ")", basePtr);
        if (!mem.TryReadUInt32(basePtr + (uint)MenuMapLayout.OffCursor, out uint cursor, out error)
            || !mem.TryReadUInt32(basePtr + (uint)MenuMapLayout.OffSubstate, out uint substate, out error)
            || !mem.TryReadUInt32(basePtr + (uint)MenuMapLayout.OffScreenId, out uint screenId, out error)
            || !mem.TryReadUInt32(basePtr + (uint)MenuMapLayout.OffAnimStep, out uint anim, out error)
            || !mem.TryReadUInt32(basePtr + (uint)MenuMapLayout.OffTransitioning, out uint transitioning, out error)
            || !mem.TryReadUInt32(basePtr + (uint)MenuMapLayout.OffChosenCell, out uint chosen, out error))
        {
            return InvalidState("フィールドを読めません (" + error + ")", basePtr);
        }
        return MakeState(basePtr, cursor, substate, screenId, anim, transitioning, chosen);
    }

    public static bool ReplayPlaying(IMenuMemory mem)
    {
        if (!mem.TryReadUInt32(GameAddresses.ReplayManagerPtr, out uint rm, out _)) return false;
        if (!PointerLooksValid(rm)) return false;
        if (!mem.TryReadUInt32(rm + (uint)MenuMapLayout.OffReplayPlaybackFlag, out uint flag, out _))
            return false;
        return flag == 1;
    }


    public static uint? ReadGlobalState(IMenuMemory mem) =>
        mem.TryReadUInt32(GameAddresses.GlobalState, out uint value, out _) ? value : null;

    public static bool? TitleDemoPlaying(IMenuMemory mem)
    {
        uint? state = ReadGlobalState(mem);
        if (state is null) return null;
        return (state.Value & MenuMapLayout.GsTitleDemo) != 0;
    }

    public static string GlobalStateText(uint? state)
    {
        if (state is null) return "読めません";
        var names = new List<string>();
        foreach (var (bit, name) in MenuMapLayout.GlobalStateBitNames)
            if ((state.Value & bit) != 0) names.Add(name);
        return Hex4(state.Value) + "(" + (names.Count > 0 ? string.Join("+", names) : "-") + ")";
    }

    public static string? ReadReplayPath(IMenuMemory? mem)
    {
        if (mem is null) return null;
        if (!mem.TryReadBytes(MenuMapLayout.AReplayPath, MenuMapLayout.ReplayPathMax,
                              out byte[] raw, out _))
        {
            return null;
        }
        int end = Array.IndexOf(raw, (byte)0);
        if (end < 0) end = raw.Length;
        if (end == 0) return null;
        for (int i = 0; i < end; i++)
            if (raw[i] > 0x7F) return null;
        return Encoding.ASCII.GetString(raw, 0, end);
    }

    public static bool? PathLooksLikeDemo(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        string normalized = path.Replace(Backslash, '/').ToLowerInvariant();
        return normalized.Contains(MenuMapLayout.DemoPathMark, StringComparison.Ordinal);
    }

    public static DemoCheck CheckTitleDemo(IMenuMemory mem, IMenuMemory? pathReader = null)
    {
        uint? state = ReadGlobalState(mem);
        if (state is null) return new DemoCheck(false, false);
        bool demo = (state.Value & MenuMapLayout.GsTitleDemo) != 0;
        string? path = demo ? ReadReplayPath(pathReader) : null;
        return new DemoCheck(true, demo, state, path);
    }


    public static uint[]? StageOffsets(IMenuMemory mem, uint basePtr, int cell)
    {
        if (cell < 0 || cell >= MenuMapLayout.CellCount)
            throw new ArgumentException("セル番号は 0.." + (MenuMapLayout.CellCount - 1) + " です: " + cell);
        uint addr = basePtr + (uint)(MenuMapLayout.OffSlotHeader
                                     + MenuMapLayout.SlotHeaderStride * cell
                                     + MenuMapLayout.OffStageOffsets);
        int want = 4 * MenuMapLayout.StageSlots;
        if (!mem.TryReadBytes(addr, want, out byte[] raw, out _)) return null;
        if (raw.Length != want) return null;
        var offsets = new uint[MenuMapLayout.StageSlots];
        for (int i = 0; i < offsets.Length; i++)
            offsets[i] = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(4 * i, 4));
        return offsets;
    }

    public static int FirstPlayableStage(uint[]? offsets)
    {
        if (offsets is null || offsets.Length == 0) return 0;
        for (int i = 0; i < offsets.Length; i++)
            if (offsets[i] != 0) return i;
        return 0;
    }

    public static bool[]? CellOccupancy(IMenuMemory mem, uint basePtr)
    {
        int want = 4 * MenuMapLayout.CellCount;
        if (!mem.TryReadBytes(basePtr + (uint)MenuMapLayout.OffSlotPtrs, want,
                              out byte[] raw, out _))
        {
            return null;
        }
        if (raw.Length != want) return null;
        var occupied = new bool[MenuMapLayout.CellCount];
        for (int i = 0; i < occupied.Length; i++)
            occupied[i] = PointerLooksValid(BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(4 * i, 4)));
        return occupied;
    }


    public static int CellOfSlot(int slot)
    {
        if (slot < 1 || slot > MenuMapLayout.NumberedCells)
            throw new ArgumentException("スロット番号は 1.." + MenuMapLayout.NumberedCells + " です: " + slot);
        return slot - 1;
    }

    public static int? SlotOfCell(int cell)
    {
        if (cell < 0 || cell >= MenuMapLayout.CellCount)
            throw new ArgumentException("セル番号は 0.." + (MenuMapLayout.CellCount - 1) + " です: " + cell);
        if (cell >= MenuMapLayout.NumberedCells) return null;
        return cell + 1;
    }

    public static bool IsNumberedCell(int cell) =>
        0 <= cell && cell < MenuMapLayout.NumberedCells;


    private static uint[] Vertical(int diff)
    {
        diff = Mod(diff, MenuMapLayout.CellCount);
        if (diff == 0) return [];
        if (diff <= MenuMapLayout.CellCount - diff)
            return Repeat(MenuMapLayout.BitDown, diff);
        return Repeat(MenuMapLayout.BitUp, MenuMapLayout.CellCount - diff);
    }

    private static uint[] Repeat(uint bit, int count)
    {
        var keys = new uint[count];
        Array.Fill(keys, bit);
        return keys;
    }

    public static uint[] CursorPath(int src, int dst)
    {
        if (src < 0 || src >= MenuMapLayout.CellCount)
            throw new ArgumentException("セル番号は 0.." + (MenuMapLayout.CellCount - 1) + " です: " + src);
        if (dst < 0 || dst >= MenuMapLayout.CellCount)
            throw new ArgumentException("セル番号は 0.." + (MenuMapLayout.CellCount - 1) + " です: " + dst);
        int diff = Mod(dst - src, MenuMapLayout.CellCount);
        uint[] verticalOnly = Vertical(diff);
        uint[] tail = Vertical(diff - MenuMapLayout.LateralStep);
        var withLateral = new uint[tail.Length + 1];
        withLateral[0] = MenuMapLayout.BitRight;
        Array.Copy(tail, 0, withLateral, 1, tail.Length);
        return verticalOnly.Length <= withLateral.Length ? verticalOnly : withLateral;
    }

    public static int ApplyKey(int cursor, uint bit)
    {
        if (bit == MenuMapLayout.BitUp) return Mod(cursor - 1, MenuMapLayout.CellCount);
        if (bit == MenuMapLayout.BitDown) return Mod(cursor + 1, MenuMapLayout.CellCount);
        if (bit == MenuMapLayout.BitLeft || bit == MenuMapLayout.BitRight)
            return Mod(cursor + MenuMapLayout.LateralStep, MenuMapLayout.CellCount);
        return cursor;
    }


    public static string ScreenName(uint screenId)
    {
        foreach (var (id, name) in MenuMapLayout.ScreenNames)
            if (screenId == id) return name;
        return "画面" + screenId;
    }

    public static string SubstateName(uint substate)
    {
        foreach (var (id, name) in MenuMapLayout.SubstateNames)
            if (substate == id) return name;
        return "?(" + substate + ")";
    }

    public static string KeyName(uint bit)
    {
        foreach (var (value, name) in MenuMapLayout.BitNames)
            if (bit == value) return name;
        return Hex4(bit);
    }

    public static string KeysText(IReadOnlyList<uint>? bits)
    {
        if (bits is null || bits.Count == 0) return "（移動なし）";
        var names = new string[bits.Count];
        for (int i = 0; i < bits.Count; i++) names[i] = KeyName(bits[i]);
        return string.Join("+", names);
    }


    public static void SelfCheck()
    {
        int prevOff = -1;
        int prevEnd = 0;
        string? prevName = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (off, name, size) in MenuMapLayout.Regions)
        {
            if (off % 4 != 0)
                throw new MenuMapException(name + " のオフセット 0x" + off.ToString("X") + " が4の倍数ではありません");
            if (!seen.Add(name))
                throw new MenuMapException("フィールド名が重複しています: " + name);
            if (off <= prevOff)
                throw new MenuMapException(name + " のオフセットが昇順ではありません（0x" + off.ToString("X5") + "）");
            if (off < prevEnd)
                throw new MenuMapException(name + " (0x" + off.ToString("X5") + ") が " + prevName
                                           + " の領域 (…0x" + prevEnd.ToString("X5") + ") と重なっています");
            prevOff = off;
            prevEnd = off + size;
            prevName = name;
        }

        if (MenuMapLayout.OffSlotPtrs + 4 * MenuMapLayout.CellCount > MenuMapLayout.OffSlotHeader)
            throw new MenuMapException("slot_ptrs[50] が slot_header に食い込んでいます");
        if (MenuMapLayout.OffSlotHeader + MenuMapLayout.SlotHeaderStride * MenuMapLayout.CellCount
            > MenuMapLayout.OffChosenCell)
        {
            throw new MenuMapException("slot_header[50] が chosen_cell に食い込んでいます");
        }

        for (int slot = 1; slot <= MenuMapLayout.NumberedCells; slot++)
            if (SlotOfCell(CellOfSlot(slot)) != slot)
                throw new MenuMapException("cell_of_slot/slot_of_cell の往復が一致しません: " + slot);
        for (int cell = 0; cell < MenuMapLayout.CellCount; cell++)
        {
            int? slot = SlotOfCell(cell);
            if (slot is null)
            {
                if (cell < MenuMapLayout.NumberedCells)
                    throw new MenuMapException("番号枠のセル " + cell + " が None になりました");
            }
            else if (CellOfSlot(slot.Value) != cell)
            {
                throw new MenuMapException("slot_of_cell/cell_of_slot の往復が一致しません: " + cell);
            }
        }

        var bits = new HashSet<uint>();
        foreach (var (bit, _) in MenuMapLayout.BitNames)
            if (!bits.Add(bit)) throw new MenuMapException("入力ビットが重複しています");

        var seenBits = new HashSet<uint>();
        foreach (var (bit, label) in MenuMapLayout.GlobalStateBitNames)
        {
            if (bit == 0 || (bit & (bit - 1)) != 0)
                throw new MenuMapException(label + " のビット 0x" + bit.ToString("X") + " が単一ビットではありません");
            if (!seenBits.Add(bit))
                throw new MenuMapException("グローバル状態語のビットが重複しています: " + label);
        }
        if (MenuMapLayout.GsTitleDemo == MenuMapLayout.GsReplayPlaying)
            throw new MenuMapException("デモ判定と再生中フラグが同じビットになっています");

        foreach (int src in new[] { 0, 1, 24, 25, 49 })
        {
            foreach (int dst in new[] { 0, 7, 24, 25, 37, 49 })
            {
                int pos = src;
                foreach (uint bit in CursorPath(src, dst)) pos = ApplyKey(pos, bit);
                if (pos != dst)
                    throw new MenuMapException("cursor_path(" + src + ", " + dst + ") が " + pos + " に着きます");
            }
        }
    }
}
