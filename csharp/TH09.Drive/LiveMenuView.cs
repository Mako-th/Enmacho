using System.Globalization;
using TH09.Generated;

namespace TH09.Drive;

public sealed class LiveMenuView : IMenuView
{
    public const int StaleReads = 20;

    public const int StaleMilliseconds = 250;

    public const int AliveStreak = 3;

    private readonly IMenuMemory _mem;
    private readonly Func<long> _clock;
    private (uint Base, uint Anim)? _staleKey;
    private int _staleCount;
    private long _staleSince;
    private (uint Base, uint ScreenId, uint Substate)? _aliveWhere;
    private uint _aliveAnim;
    private int _aliveStreak;

    public LiveMenuView(IMenuMemory mem, Func<long>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(mem);
        _mem = mem;
        _clock = clock ?? (() => Environment.TickCount64);
    }

    public uint LastBase { get; private set; }

    public int StaleHits { get; private set; }

    public int AliveHits { get; private set; }

    public bool ReplayPlaying() => MenuMap.ReplayPlaying(_mem);

    public bool? TitleDemo()
    {
        var check = MenuMap.CheckTitleDemo(_mem, _mem);
        return check.Known ? check.Demo : null;
    }

    public MenuState Read()
    {
        var state = MenuMap.ReadState(_mem);
        if (state.Base != 0) LastBase = state.Base;
        if (!state.Valid)
        {
            _staleKey = null;
            _staleCount = 0;
            return state;
        }
        var key = (state.Base, state.Anim);
        long now = _clock();
        if (_staleKey != key)
        {
            _staleKey = key;
            _staleCount = 1;
            _staleSince = now;
            return state;
        }
        _staleCount++;
        bool frozen = _staleCount >= StaleReads && now - _staleSince >= StaleMilliseconds;
        if (frozen && ReplayPlaying())
        {
            StaleHits++;
            var seconds = ((now - _staleSince) / 1000.0).ToString("F2", CultureInfo.InvariantCulture);
            return MenuMap.InvalidState(
                "再生中に MainMenu が " + _staleCount.ToString(CultureInfo.InvariantCulture)
                + " 回・" + seconds + " 秒 tick していません（anim="
                + state.Anim.ToString(CultureInfo.InvariantCulture)
                + " のまま。解放済みブロックの残骸）", state.Base);
        }
        return state;
    }

    public bool AtTitleOrMenu()
    {
        var state = Read();
        if (!state.Valid || state.Transitioning)
        {
            _aliveWhere = null;
            _aliveStreak = 0;
            return false;
        }
        var where = (state.Base, state.ScreenId, state.Substate);
        if (_aliveWhere is { } was && was == where && state.Anim > _aliveAnim) _aliveStreak++;
        else _aliveStreak = 0;
        _aliveWhere = where;
        _aliveAnim = state.Anim;
        if (_aliveStreak < AliveStreak) return false;
        AliveHits++;
        return true;
    }

    public uint[]? StageOffsets(int cell)
    {
        var state = Read();
        return state.Valid ? MenuMap.StageOffsets(_mem, state.Base, cell) : null;
    }

    public bool CellOccupied(int cell)
    {
        if (cell < 0 || cell >= MenuMapLayout.CellCount)
        {
            throw new ArgumentException(
                "セル番号は 0.." + (MenuMapLayout.CellCount - 1).ToString(CultureInfo.InvariantCulture)
                + " です: " + cell.ToString(CultureInfo.InvariantCulture));
        }
        var state = Read();
        if (!state.Valid) return false;
        if (!_mem.TryReadUInt32(state.Base + (uint)MenuMapLayout.OffSlotPtrs + 4u * (uint)cell,
                                out uint ptr, out _))
        {
            return false;
        }
        return MenuMap.PointerLooksValid(ptr);
    }

    public bool[]? Occupancy()
    {
        var state = Read();
        return state.Valid ? MenuMap.CellOccupancy(_mem, state.Base) : null;
    }
}
