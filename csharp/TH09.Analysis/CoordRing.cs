namespace TH09.Analysis;

public static class CoordRing
{
    public const string SuffixX = "_x";
    public const string SuffixY = "_y";
    public const string SuffixKind = "_kind";
    public const string SuffixState = "_state";

    public static readonly string[] LaserSuffixes =
        ["_angle", "_tail", "_head", "_width", "_timer", "_gate0", "_gate2", "_phase"];

    private static readonly string[] Bases = Packed.Lines(AnalysisTables.SlotBasesPacked);

    public static int SlotCount => Bases.Length;

    public static string BaseName(int slot) => Bases[slot];

    public static string ColX(int slot) => Bases[slot] + SuffixX;
    public static string ColY(int slot) => Bases[slot] + SuffixY;
    public static string ColKind(int slot) => Bases[slot] + SuffixKind;
    public static string ColState(int slot) => Bases[slot] + SuffixState;

    public static string LaserCol(int slot, int which)
    {
        if (!IsLaserSlot(slot)) throw new ArgumentException($"slot {slot} はレーザー枠ではありません");
        return Bases[slot] + LaserSuffixes[which];
    }

    public static bool IsBulletSlot(int slot) =>
        (slot >= AnalysisTables.CoordBaseP1Bullet && slot < AnalysisTables.CoordBaseP1Bullet + AnalysisTables.CoordBulletSlots)
        || (slot >= AnalysisTables.CoordBaseP2Bullet && slot < AnalysisTables.CoordBaseP2Bullet + AnalysisTables.CoordBulletSlots);

    public static bool IsEnemySlot(int slot) =>
        (slot >= AnalysisTables.CoordBaseP1Enemy && slot < AnalysisTables.CoordBaseP1Enemy + AnalysisTables.CoordEnemySlots)
        || (slot >= AnalysisTables.CoordBaseP2Enemy && slot < AnalysisTables.CoordBaseP2Enemy + AnalysisTables.CoordEnemySlots);

    public static bool IsLaserSlot(int slot) =>
        slot >= AnalysisTables.CoordBaseP1Laser && slot < AnalysisTables.CoordBaseP1Laser + AnalysisTables.CoordLaserTotal;

    public static bool IsExSlot(int slot) =>
        slot >= AnalysisTables.CoordBaseEx && slot < AnalysisTables.CoordBaseEx + AnalysisTables.CoordExSlots;

    public static bool IsShotSlot(int slot) =>
        slot >= AnalysisTables.CoordBaseP1Shot && slot < AnalysisTables.CoordBaseP1Shot + AnalysisTables.CoordShotTotal;

    public static bool SlotCountHasEx(int slotCount) => slotCount > AnalysisTables.CoordBaseEx;

    public static bool SlotIsAlive(int slot, uint state)
    {
        if (IsBulletSlot(slot))
        {
            var masked = state & AnalysisTables.CoordStateAliveMask;
            return masked != AnalysisTables.CoordBulletStateFree
                && masked != AnalysisTables.CoordBulletStateTerm;
        }
        if (IsLaserSlot(slot)) return state != AnalysisTables.CoordLaserStateFree;
        if (IsExSlot(slot)) return state != AnalysisTables.CoordExStateFree;
        if (IsShotSlot(slot)) return state != AnalysisTables.CoordShotStateFree;
        return (state & 1) != 0;
    }

    public static bool SlotIsVanishing(int slot, uint state)
    {
        if (IsShotSlot(slot)) return state == AnalysisTables.CoordShotStateVanish;
        if (IsBulletSlot(slot))
            return (state & AnalysisTables.CoordStateAliveMask) == AnalysisTables.CoordBulletStateVanish;
        return false;
    }

    public static uint LaserPhase(uint phaseWord) => phaseWord & AnalysisTables.CoordLaserPhaseMask;

    public static bool LaserIsLethal(uint phaseWord, uint timer, uint gate0, uint gate2)
    {
        var phase = LaserPhase(phaseWord);
        if (phase == AnalysisTables.CoordLaserPhase1) return true;
        if (phase == AnalysisTables.CoordLaserPhase0) return S32(timer) >= S32(gate0);
        if (phase == AnalysisTables.CoordLaserPhase2) return S32(timer) < S32(gate2);
        return false;
    }

    private static int S32(uint v) => unchecked((int)v);

    public static string EnemyClass(uint kind)
    {
        if ((kind & AnalysisTables.CoordEnemyLilyBit) != 0) return AnalysisTables.EnemyClassLily;
        if ((kind & AnalysisTables.CoordEnemyGhostMask) != 0) return AnalysisTables.EnemyClassGhost;
        if ((kind & AnalysisTables.CoordEnemyBossMask) != 0)
            return (kind & AnalysisTables.CoordEnemyBossMask) == AnalysisTables.CoordEnemyBossMask
                ? AnalysisTables.EnemyClassBoss : AnalysisTables.EnemyClassC2C3;
        if ((kind & AnalysisTables.CoordEnemyFairyZeroMask) == 0) return AnalysisTables.EnemyClassFairy;
        return AnalysisTables.EnemyClassOther;
    }

    private const uint EnemyGhostActiveBit = 0x1000u;

    public static bool EnemyGhostActivated(uint kind) => (kind & EnemyGhostActiveBit) != 0;

    public static uint EnemyCatWord(uint kind) => kind & AnalysisTables.CoordEnemyKindWordMask;

    public static uint EnemyCat(uint kind) =>
        (EnemyCatWord(kind) >> AnalysisTables.CoordEnemyCatShift) & AnalysisTables.CoordEnemyCatMask;

    public static double UnpackF32(uint bits) => BitConverter.UInt32BitsToSingle(bits);
}
