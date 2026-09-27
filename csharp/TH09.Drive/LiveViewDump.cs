using System.Globalization;
using TH09.Generated;

namespace TH09.Drive;

internal static class LiveViewDump
{
    public const string Flag = "--dump-live-view";

    private const uint Base1 = 0x0234_5678;
    private const uint Base2 = 0x0256_ABC0;
    private const uint SlotPtr = 0x0031_0000;
    private const uint DeadPtr = 0x0000_0123;

    public static int Run(TextWriter writer)
    {
        var mem = new TableMemory(writer);
        long clock = 0;
        var view = new LiveMenuView(mem, () => clock);

        void Clock(long ms)
        {
            clock = ms;
            writer.Write("clock\t" + ms.ToString(CultureInfo.InvariantCulture) + "\n");
        }

        void Call(string name, Func<string> body)
        {
            writer.Write("call\t" + name + "\n");
            string answer;
            try
            {
                answer = body();
            }
            catch (ArgumentException exc)
            {
                answer = "!" + exc.Message;
            }
            writer.Write("out\t" + answer + "\n");
        }

        string ReadText() => view.Read().Text();
        string Offsets(int cell)
        {
            var got = view.StageOffsets(cell);
            return got is null ? "-" : string.Join(",", got.Select(Hex));
        }
        string Occupancy()
        {
            var got = view.Occupancy();
            return got is null ? "-" : string.Concat(got.Select(x => x ? '1' : '0'));
        }
        static string Hex(uint value) => "0x" + value.ToString("X8", CultureInfo.InvariantCulture);
        static string Bit(bool value) => value ? "1" : "0";
        static string Tri(bool? value) => value is null ? "-" : (value.Value ? "1" : "0");

        Clock(1000);
        Call("read/no-pointer", ReadText);

        mem.Put(MenuMapLayout.MainMenuPtr, DeadPtr);
        Clock(1010);
        Call("read/bad-pointer", ReadText);

        mem.Put(MenuMapLayout.MainMenuPtr, Base1);
        Clock(1020);
        Call("read/no-fields", ReadText);

        WriteMenu(mem, Base1, cursor: 7, substate: MenuMapLayout.SubReady,
                  screen: MenuMapLayout.ScreenReplayList, anim: 100,
                  transitioning: 0, chosen: 3);
        Clock(1030);
        Call("read/alive", ReadText);

        for (int i = 1; i <= 3; i++)
        {
            mem.Put(Base1 + (uint)MenuMapLayout.OffAnimStep, 100u + (uint)i);
            Clock(1030 + 20 * i);
            Call("read/anim+" + i.ToString(CultureInfo.InvariantCulture), ReadText);
        }

        for (int i = 1; i <= LiveMenuView.StaleReads + 1; i++)
        {
            Clock(1100 + 100L * i);
            Call("read/frozen-not-playing#" + i.ToString(CultureInfo.InvariantCulture), ReadText);
        }

        WriteReplayManager(mem, playing: true);
        mem.Put(Base1 + (uint)MenuMapLayout.OffAnimStep, 200);
        long at = 5000;
        for (int i = 1; i <= LiveMenuView.StaleReads + 1; i++)
        {
            Clock(at);
            Call("read/frozen-fast#" + i.ToString(CultureInfo.InvariantCulture), ReadText);
        }

        mem.Put(Base1 + (uint)MenuMapLayout.OffAnimStep, 201);
        Clock(9000);
        Call("read/frozen-slow#1", ReadText);
        Clock(9000 + LiveMenuView.StaleMilliseconds * 4);
        Call("read/frozen-slow#2", ReadText);

        mem.Put(Base1 + (uint)MenuMapLayout.OffAnimStep, 202);
        for (int i = 1; i <= LiveMenuView.StaleReads + 2; i++)
        {
            Clock(20000 + 100L * i);
            Call("read/frozen#" + i.ToString(CultureInfo.InvariantCulture), ReadText);
        }

        mem.Put(MenuMapLayout.MainMenuPtr, Base2);
        WriteMenu(mem, Base2, cursor: 11, substate: MenuMapLayout.SubReady,
                  screen: MenuMapLayout.ScreenReplayList, anim: 5,
                  transitioning: 1, chosen: 0);
        Clock(30000);
        Call("read/rebased", ReadText);

        for (int cell = 0; cell < MenuMapLayout.CellCount; cell++)
        {
            mem.Put(Base2 + (uint)MenuMapLayout.OffSlotPtrs + 4u * (uint)cell,
                    cell % 3 == 0 ? SlotPtr + (uint)cell : DeadPtr);
        }
        Clock(30010);
        Call("occupied/0", () => Bit(view.CellOccupied(0)));
        Call("occupied/1", () => Bit(view.CellOccupied(1)));
        Call("occupied/49", () => Bit(view.CellOccupied(MenuMapLayout.CellCount - 1)));
        Call("occupied/50", () => Bit(view.CellOccupied(MenuMapLayout.CellCount)));
        Call("occupied/-1", () => Bit(view.CellOccupied(-1)));
        Call("occupancy", Occupancy);

        for (int i = 0; i < MenuMapLayout.StageSlots; i++)
        {
            uint addr = Base2 + (uint)(MenuMapLayout.OffSlotHeader
                                       + MenuMapLayout.SlotHeaderStride * 4 + MenuMapLayout.OffStageOffsets)
                        + 4u * (uint)i;
            mem.Put(addr, i < 2 ? 0u : 0x1000u + (uint)i);
        }
        Clock(30020);
        Call("stage/4", () => Offsets(4));
        Call("stage/5", () => Offsets(5));

        Clock(30030);
        Call("replay/on", () => Bit(view.ReplayPlaying()));
        Call("demo/unknown", () => Tri(view.TitleDemo()));
        mem.Put(GameAddresses.GlobalState, 0);
        Call("demo/off", () => Tri(view.TitleDemo()));
        mem.Put(GameAddresses.GlobalState, MenuMapLayout.GsTitleDemo);
        Call("demo/on", () => Tri(view.TitleDemo()));
        WriteReplayManager(mem, playing: false);
        Call("replay/off", () => Bit(view.ReplayPlaying()));

        writer.Write("stale_hits\t" + view.StaleHits.ToString(CultureInfo.InvariantCulture) + "\n");
        writer.Write("last_base\t" + Hex(view.LastBase) + "\n");
        writer.Write("reads\t" + mem.Reads.ToString(CultureInfo.InvariantCulture) + "\n");
        return 0;
    }

    private static void WriteMenu(TableMemory mem, uint basePtr, uint cursor, uint substate,
                                  uint screen, uint anim, uint transitioning, uint chosen)
    {
        mem.Put(basePtr + (uint)MenuMapLayout.OffCursor, cursor);
        mem.Put(basePtr + (uint)MenuMapLayout.OffSubstate, substate);
        mem.Put(basePtr + (uint)MenuMapLayout.OffScreenId, screen);
        mem.Put(basePtr + (uint)MenuMapLayout.OffAnimStep, anim);
        mem.Put(basePtr + (uint)MenuMapLayout.OffTransitioning, transitioning);
        mem.Put(basePtr + (uint)MenuMapLayout.OffChosenCell, chosen);
    }

    private static void WriteReplayManager(TableMemory mem, bool playing)
    {
        const uint manager = 0x0027_9000;
        mem.Put(GameAddresses.ReplayManagerPtr, manager);
        mem.Put(manager + (uint)MenuMapLayout.OffReplayPlaybackFlag, playing ? 1u : 0u);
    }

    private sealed class TableMemory(TextWriter writer) : IMenuMemory
    {
        private readonly Dictionary<uint, uint> _cells = [];

        public int Reads { get; private set; }

        public void Put(uint address, uint value)
        {
            _cells[address] = value;
            writer.Write("mem\t0x" + address.ToString("X8", CultureInfo.InvariantCulture)
                         + "\t0x" + value.ToString("X8", CultureInfo.InvariantCulture) + "\n");
        }

        public bool TryReadUInt32(uint address, out uint value, out string error)
        {
            Reads++;
            if (_cells.TryGetValue(address, out value))
            {
                error = "";
                return true;
            }
            value = 0;
            error = Missing(address);
            return false;
        }

        public bool TryReadBytes(uint address, int size, out byte[] value, out string error)
        {
            Reads++;
            value = [];
            if (size <= 0 || size % 4 != 0)
            {
                error = Missing(address);
                return false;
            }
            var buffer = new byte[size];
            for (int at = 0; at < size; at += 4)
            {
                if (!_cells.TryGetValue(address + (uint)at, out uint word))
                {
                    error = Missing(address + (uint)at);
                    return false;
                }
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
                    buffer.AsSpan(at, 4), word);
            }
            value = buffer;
            error = "";
            return true;
        }

        private static string Missing(uint address)
            => "no cell at " + MenuMap.Hex8(address);
    }
}
