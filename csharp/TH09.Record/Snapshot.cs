namespace TH09.Record;

public sealed class Snapshot
{
    public required string WallTime { get; init; }

    public required double Monotonic { get; init; }

    public required long Mode { get; init; }
    public required long Difficulty { get; init; }
    public required long StageIndex { get; init; }
    public required long FieldId { get; init; }
    public required long BattleBgmId { get; init; }
    public required long P1Character { get; init; }
    public required long P2Character { get; init; }
    public required long P1Control { get; init; }
    public required long P2Control { get; init; }
    public required long P1CpuLevel { get; init; }
    public required long P2CpuLevel { get; init; }

    public required long RoundFrames { get; init; }
    public required long CompletedRounds { get; init; }
    public required long RoundsRequired { get; init; }
    public required long P1Wins { get; init; }
    public required long P2Wins { get; init; }
    public required long ResultState { get; init; }
    public required long ResultWinner { get; init; }
    public required long PauseUsed { get; init; }

    public required long P1LifeRaw { get; init; }
    public required long P2LifeRaw { get; init; }
    public required double P1Gauge { get; init; }
    public required double P2Gauge { get; init; }
    public required double P1Lives { get; init; }
    public required double P2Lives { get; init; }
    public required long P1Score { get; init; }
    public required long P2Score { get; init; }
    public required long P1MaxCombo { get; init; }
    public required long P2MaxCombo { get; init; }
    public required long P1SpellPoints { get; init; }
    public required long P2SpellPoints { get; init; }
    public required int P1ComboGaugeRaw { get; init; }
    public required int P2ComboGaugeRaw { get; init; }

    public required long P1SpellAttacks { get; init; }
    public required long P2SpellAttacks { get; init; }
    public required long P1BossAttacks { get; init; }
    public required long P2BossAttacks { get; init; }
    public required long P1BossReversals { get; init; }
    public required long P2BossReversals { get; init; }

    public required long ClearLifeBonus { get; init; }
    public required long ClearMaxComboBonus { get; init; }
    public required long ClearSpellBonus { get; init; }
    public required long ClearBossBonus { get; init; }
    public required long ClearReversalBonus { get; init; }
    public required long ClearLivesBonus { get; init; }
    public required long ClearTotal { get; init; }

    public required int P1CpuDodgeMode { get; init; }
    public required int P2CpuDodgeMode { get; init; }
    public required int P1CpuQuickDisableTimer { get; init; }
    public required int P2CpuQuickDisableTimer { get; init; }
    public required int P1CpuStandstillTimer { get; init; }
    public required int P2CpuStandstillTimer { get; init; }

    public required string ExecutionType { get; init; }


    public long LifeRaw(int side) => side == 1 ? P1LifeRaw : P2LifeRaw;

    public long SpellPoints(int side) => side == 1 ? P1SpellPoints : P2SpellPoints;

    public int ComboGaugeRaw(int side) => side == 1 ? P1ComboGaugeRaw : P2ComboGaugeRaw;

    public double Gauge(int side) => side == 1 ? P1Gauge : P2Gauge;

    public long AttackTotal(int side) =>
        side == 1 ? P1SpellAttacks + P1BossAttacks : P2SpellAttacks + P2BossAttacks;

    public int CpuDodgeMode(int side) => side == 1 ? P1CpuDodgeMode : P2CpuDodgeMode;

    public int CpuQuickDisableTimer(int side) => side == 1 ? P1CpuQuickDisableTimer : P2CpuQuickDisableTimer;

    public int CpuStandstillTimer(int side) => side == 1 ? P1CpuStandstillTimer : P2CpuStandstillTimer;
}
