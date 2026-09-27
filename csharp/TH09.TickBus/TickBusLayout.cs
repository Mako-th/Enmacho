using W = TH09.Generated.TickWords;

namespace TH09.TickBus;

public sealed class TickBusLayoutException(string message) : Exception(message);

public static class TickBusLayout
{
    public const uint Magic = W.Magic;
    public const uint Version = W.Version;
    public const int HeaderSize = W.HeaderSize;
    public const int RecordSize = W.RecordSize;
    public const int Capacity = W.Capacity;

    public const uint IndexMask = Capacity - 1u;

    public const long TotalSize = HeaderSize + (long)RecordSize * Capacity;

    public const uint SeqBuilding = 0xFFFFFFFFu;

    public const int HeaderWordCount = HeaderSize / 4;

    public const int RecordWordCount = RecordSize / 4;

    public static readonly (string Name, int Offset)[] HeaderFields =
    [
        (W.Header.Magic, W.Header.MagicOffset),
        (W.Header.Version, W.Header.VersionOffset),
        (W.Header.RecordSize, W.Header.RecordSizeOffset),
        (W.Header.Capacity, W.Header.CapacityOffset),
        (W.Header.WriteIndex, W.Header.WriteIndexOffset),
        (W.Header.HookState, W.Header.HookStateOffset),
        (W.Header.LastError, W.Header.LastErrorOffset),
        (W.Header.GamePid, W.Header.GamePidOffset),
        (W.Header.HookAddr, W.Header.HookAddrOffset),
        (W.Header.WriterTid, W.Header.WriterTidOffset),
        (W.Header.Dropped, W.Header.DroppedOffset),
        (W.Header.ArmedTicks, W.Header.ArmedTicksOffset),
        (W.Header.CmdFields, W.Header.CmdFieldsOffset),
        (W.Header.InputHookAddr, W.Header.InputHookAddrOffset),
        (W.Header.InputTicks, W.Header.InputTicksOffset),
        (W.Header.InputError, W.Header.InputErrorOffset),
        (W.Header.CmdSeq, W.Header.CmdSeqOffset),
        (W.Header.CmdMask, W.Header.CmdMaskOffset),
        (W.Header.CmdTicks, W.Header.CmdTicksOffset),
        (W.Header.CmdAck, W.Header.CmdAckOffset),
        (W.Header.InputHits, W.Header.InputHitsOffset),
        (W.Header.InputLastEcx, W.Header.InputLastEcxOffset),
        (W.Header.InputPollTicks, W.Header.InputPollTicksOffset),
        (W.Header.HitHookAddr, W.Header.HitHookAddrOffset),
        (W.Header.HitError, W.Header.HitErrorOffset),
        (W.Header.HitTicks, W.Header.HitTicksOffset),
        (W.Header.SpeedHookAddr, W.Header.SpeedHookAddrOffset),
        (W.Header.SpeedError, W.Header.SpeedErrorOffset),
        (W.Header.SpeedTicks, W.Header.SpeedTicksOffset),
    ];

    public static readonly (string Name, int Offset)[] RecordFields =
    [
        (W.Record.SeqBegin, W.Record.SeqBeginOffset),
        (W.Record.Flags, W.Record.FlagsOffset),
        (W.Record.P1EnemyClassCounts, W.Record.P1EnemyClassCountsOffset),
        (W.Record.P2EnemyClassCounts, W.Record.P2EnemyClassCountsOffset),
        (W.Record.Mode, W.Record.ModeOffset),
        (W.Record.Difficulty, W.Record.DifficultyOffset),
        (W.Record.StageIndex, W.Record.StageIndexOffset),
        (W.Record.FieldId, W.Record.FieldIdOffset),
        (W.Record.BattleBgmId, W.Record.BattleBgmIdOffset),
        (W.Record.RoundFrames, W.Record.RoundFramesOffset),
        (W.Record.CompletedRounds, W.Record.CompletedRoundsOffset),
        (W.Record.RoundsRequired, W.Record.RoundsRequiredOffset),
        (W.Record.PauseUsed, W.Record.PauseUsedOffset),
        (W.Record.P1Wins, W.Record.P1WinsOffset),
        (W.Record.P2Wins, W.Record.P2WinsOffset),
        (W.Record.ResultState, W.Record.ResultStateOffset),
        (W.Record.ResultWinner, W.Record.ResultWinnerOffset),
        (W.Record.InputMask, W.Record.InputMaskOffset),
        (W.Record.ReplayFlag, W.Record.ReplayFlagOffset),
        (W.Record.P1Character, W.Record.P1CharacterOffset),
        (W.Record.P2Character, W.Record.P2CharacterOffset),
        (W.Record.P1Control, W.Record.P1ControlOffset),
        (W.Record.P2Control, W.Record.P2ControlOffset),
        (W.Record.P1CpuLevel, W.Record.P1CpuLevelOffset),
        (W.Record.P2CpuLevel, W.Record.P2CpuLevelOffset),
        (W.Record.P1LifeRaw, W.Record.P1LifeRawOffset),
        (W.Record.P2LifeRaw, W.Record.P2LifeRawOffset),
        (W.Record.P1Lives, W.Record.P1LivesOffset),
        (W.Record.P2Lives, W.Record.P2LivesOffset),
        (W.Record.P1ScoreRaw, W.Record.P1ScoreRawOffset),
        (W.Record.P2ScoreRaw, W.Record.P2ScoreRawOffset),
        (W.Record.P1ScoreMirror, W.Record.P1ScoreMirrorOffset),
        (W.Record.P2ScoreMirror, W.Record.P2ScoreMirrorOffset),
        (W.Record.P1CurrentCombo, W.Record.P1CurrentComboOffset),
        (W.Record.P2CurrentCombo, W.Record.P2CurrentComboOffset),
        (W.Record.P1MaxCombo, W.Record.P1MaxComboOffset),
        (W.Record.P2MaxCombo, W.Record.P2MaxComboOffset),
        (W.Record.P1SpellAttacks, W.Record.P1SpellAttacksOffset),
        (W.Record.P2SpellAttacks, W.Record.P2SpellAttacksOffset),
        (W.Record.P1BossAttacks, W.Record.P1BossAttacksOffset),
        (W.Record.P2BossAttacks, W.Record.P2BossAttacksOffset),
        (W.Record.P1BossReversals, W.Record.P1BossReversalsOffset),
        (W.Record.P2BossReversals, W.Record.P2BossReversalsOffset),
        (W.Record.P1SpellPoints, W.Record.P1SpellPointsOffset),
        (W.Record.P2SpellPoints, W.Record.P2SpellPointsOffset),
        (W.Record.P1SpellPointsMirror, W.Record.P1SpellPointsMirrorOffset),
        (W.Record.P2SpellPointsMirror, W.Record.P2SpellPointsMirrorOffset),
        (W.Record.P1Gauge, W.Record.P1GaugeOffset),
        (W.Record.P2Gauge, W.Record.P2GaugeOffset),
        (W.Record.ClearLifeBonus, W.Record.ClearLifeBonusOffset),
        (W.Record.ClearMaxComboBonus, W.Record.ClearMaxComboBonusOffset),
        (W.Record.ClearSpellBonus, W.Record.ClearSpellBonusOffset),
        (W.Record.ClearBossBonus, W.Record.ClearBossBonusOffset),
        (W.Record.ClearReversalBonus, W.Record.ClearReversalBonusOffset),
        (W.Record.ClearLivesBonus, W.Record.ClearLivesBonusOffset),
        (W.Record.ClearTotal, W.Record.ClearTotalOffset),
        (W.Record.P1CpuDodgeMode, W.Record.P1CpuDodgeModeOffset),
        (W.Record.P2CpuDodgeMode, W.Record.P2CpuDodgeModeOffset),
        (W.Record.P1CpuQuickTimer, W.Record.P1CpuQuickTimerOffset),
        (W.Record.P2CpuQuickTimer, W.Record.P2CpuQuickTimerOffset),
        (W.Record.P1CpuStandTimer, W.Record.P1CpuStandTimerOffset),
        (W.Record.P2CpuStandTimer, W.Record.P2CpuStandTimerOffset),
        (W.Record.P1ZeroHitTimer, W.Record.P1ZeroHitTimerOffset),
        (W.Record.P2ZeroHitTimer, W.Record.P2ZeroHitTimerOffset),
        (W.Record.P1ComboGaugeRaw, W.Record.P1ComboGaugeRawOffset),
        (W.Record.P2ComboGaugeRaw, W.Record.P2ComboGaugeRawOffset),
        (W.Record.P1EnemyTotal, W.Record.P1EnemyTotalOffset),
        (W.Record.P2EnemyTotal, W.Record.P2EnemyTotalOffset),
        (W.Record.P1EnemyFairy, W.Record.P1EnemyFairyOffset),
        (W.Record.P2EnemyFairy, W.Record.P2EnemyFairyOffset),
        (W.Record.P1EnemyBoss, W.Record.P1EnemyBossOffset),
        (W.Record.P2EnemyBoss, W.Record.P2EnemyBossOffset),
        (W.Record.P1EnemyCharge, W.Record.P1EnemyChargeOffset),
        (W.Record.P2EnemyCharge, W.Record.P2EnemyChargeOffset),
        (W.Record.P1BulletFairy, W.Record.P1BulletFairyOffset),
        (W.Record.P2BulletFairy, W.Record.P2BulletFairyOffset),
        (W.Record.P1BulletRival, W.Record.P1BulletRivalOffset),
        (W.Record.P2BulletRival, W.Record.P2BulletRivalOffset),
        (W.Record.InternalRank, W.Record.InternalRankOffset),
        (W.Record.RankInterval, W.Record.RankIntervalOffset),
        (W.Record.RankMax, W.Record.RankMaxOffset),
        (W.Record.P1Charge, W.Record.P1ChargeOffset),
        (W.Record.P2Charge, W.Record.P2ChargeOffset),
        (W.Record.P1CardAttackLevel, W.Record.P1CardAttackLevelOffset),
        (W.Record.P2CardAttackLevel, W.Record.P2CardAttackLevelOffset),
        (W.Record.P1BossCardAttackLevel, W.Record.P1BossCardAttackLevelOffset),
        (W.Record.P2BossCardAttackLevel, W.Record.P2BossCardAttackLevelOffset),
        (W.Record.P1BossType, W.Record.P1BossTypeOffset),
        (W.Record.P2BossType, W.Record.P2BossTypeOffset),
        (W.Record.P1BossSub, W.Record.P1BossSubOffset),
        (W.Record.P2BossSub, W.Record.P2BossSubOffset),
        (W.Record.P1BossDepth, W.Record.P1BossDepthOffset),
        (W.Record.P2BossDepth, W.Record.P2BossDepthOffset),
        (W.Record.P1BossHp, W.Record.P1BossHpOffset),
        (W.Record.P2BossHp, W.Record.P2BossHpOffset),
        (W.Record.P1ExActive, W.Record.P1ExActiveOffset),
        (W.Record.P2ExActive, W.Record.P2ExActiveOffset),
        (W.Record.P1ExTriggered, W.Record.P1ExTriggeredOffset),
        (W.Record.P2ExTriggered, W.Record.P2ExTriggeredOffset),
        (W.Record.P1CpuQuickTimerCur, W.Record.P1CpuQuickTimerCurOffset),
        (W.Record.P2CpuQuickTimerCur, W.Record.P2CpuQuickTimerCurOffset),
        (W.Record.P1CpuStandTimerCur, W.Record.P1CpuStandTimerCurOffset),
        (W.Record.P2CpuStandTimerCur, W.Record.P2CpuStandTimerCurOffset),
        (W.Record.P1ComboGaugeCur, W.Record.P1ComboGaugeCurOffset),
        (W.Record.P2ComboGaugeCur, W.Record.P2ComboGaugeCurOffset),
        (W.Record.LilyCounter, W.Record.LilyCounterOffset),
        (W.Record.P1PosX, W.Record.P1PosXOffset),
        (W.Record.P2PosX, W.Record.P2PosXOffset),
        (W.Record.P1PosY, W.Record.P1PosYOffset),
        (W.Record.P2PosY, W.Record.P2PosYOffset),
        (W.Record.P1InputReplay, W.Record.P1InputReplayOffset),
        (W.Record.P2InputReplay, W.Record.P2InputReplayOffset),
        (W.Record.P1WhiteBulletPoints, W.Record.P1WhiteBulletPointsOffset),
        (W.Record.P2WhiteBulletPoints, W.Record.P2WhiteBulletPointsOffset),
        (W.Record.P1GhostPoints, W.Record.P1GhostPointsOffset),
        (W.Record.P2GhostPoints, W.Record.P2GhostPointsOffset),
        (W.Record.P1ExPoints, W.Record.P1ExPointsOffset),
        (W.Record.P2ExPoints, W.Record.P2ExPointsOffset),
        (W.Record.P1BossPosX, W.Record.P1BossPosXOffset),
        (W.Record.P2BossPosX, W.Record.P2BossPosXOffset),
        (W.Record.P1BossPosY, W.Record.P1BossPosYOffset),
        (W.Record.P2BossPosY, W.Record.P2BossPosYOffset),
        (W.Record.P1CpuChargeInstruction, W.Record.P1CpuChargeInstructionOffset),
        (W.Record.P2CpuChargeInstruction, W.Record.P2CpuChargeInstructionOffset),
        (W.Record.RngState, W.Record.RngStateOffset),
        (W.Record.P1HitKind, W.Record.P1HitKindOffset),
        (W.Record.P1HitObj, W.Record.P1HitObjOffset),
        (W.Record.P1HitX, W.Record.P1HitXOffset),
        (W.Record.P1HitY, W.Record.P1HitYOffset),
        (W.Record.P2HitKind, W.Record.P2HitKindOffset),
        (W.Record.P2HitObj, W.Record.P2HitObjOffset),
        (W.Record.P2HitX, W.Record.P2HitXOffset),
        (W.Record.P2HitY, W.Record.P2HitYOffset),
        (W.Record.HitDamageBase, W.Record.HitDamageBaseOffset),
        (W.Record.CoordRingState, W.Record.CoordRingStateOffset),
        (W.Record.OsInterruptTimeLo, W.Record.OsInterruptTimeLoOffset),
        (W.Record.OsTickCountLo, W.Record.OsTickCountLoOffset),
        (W.Record.P1HitObjPtr, W.Record.P1HitObjPtrOffset),
        (W.Record.P2HitObjPtr, W.Record.P2HitObjPtrOffset),
        (W.Record.P1HitLaserX, W.Record.P1HitLaserXOffset),
        (W.Record.P1HitLaserY, W.Record.P1HitLaserYOffset),
        (W.Record.P2HitLaserX, W.Record.P2HitLaserXOffset),
        (W.Record.P2HitLaserY, W.Record.P2HitLaserYOffset),
        (W.Record.P1HitLaserAngle, W.Record.P1HitLaserAngleOffset),
        (W.Record.P2HitLaserAngle, W.Record.P2HitLaserAngleOffset),
        (W.Record.P1HitElemRadius, W.Record.P1HitElemRadiusOffset),
        (W.Record.P2HitElemRadius, W.Record.P2HitElemRadiusOffset),
        (W.Record.P1PlayerState, W.Record.P1PlayerStateOffset),
        (W.Record.P2PlayerState, W.Record.P2PlayerStateOffset),
        (W.Record.P1InvincibleTimer, W.Record.P1InvincibleTimerOffset),
        (W.Record.P2InvincibleTimer, W.Record.P2InvincibleTimerOffset),
        (W.Record.P1BulletMgr, W.Record.P1BulletMgrOffset),
        (W.Record.P2BulletMgr, W.Record.P2BulletMgrOffset),
        (W.Record.P1SpeedMult, W.Record.P1SpeedMultOffset),
        (W.Record.P2SpeedMult, W.Record.P2SpeedMultOffset),
        (W.Record.P1HitListCount, W.Record.P1HitListCountOffset),
        (W.Record.P2HitListCount, W.Record.P2HitListCountOffset),
        (W.Record.P1GameFlags, W.Record.P1GameFlagsOffset),
        (W.Record.P2GameFlags, W.Record.P2GameFlagsOffset),
        (W.Record.GlobalState, W.Record.GlobalStateOffset),
        (W.Record.P1MoveDirAngle, W.Record.P1MoveDirAngleOffset),
        (W.Record.P2MoveDirAngle, W.Record.P2MoveDirAngleOffset),
        (W.Record.P1CpuDirLock, W.Record.P1CpuDirLockOffset),
        (W.Record.P2CpuDirLock, W.Record.P2CpuDirLockOffset),
        (W.Record.P1CpuPrevDir, W.Record.P1CpuPrevDirOffset),
        (W.Record.P2CpuPrevDir, W.Record.P2CpuPrevDirOffset),
        (W.Record.P1CpuDirHist0, W.Record.P1CpuDirHist0Offset),
        (W.Record.P1CpuDirHist1, W.Record.P1CpuDirHist1Offset),
        (W.Record.P1CpuDirHist2, W.Record.P1CpuDirHist2Offset),
        (W.Record.P1CpuDirHist3, W.Record.P1CpuDirHist3Offset),
        (W.Record.P1CpuDirHist4, W.Record.P1CpuDirHist4Offset),
        (W.Record.P1CpuDirHist5, W.Record.P1CpuDirHist5Offset),
        (W.Record.P1CpuDirHist6, W.Record.P1CpuDirHist6Offset),
        (W.Record.P1CpuDirHist7, W.Record.P1CpuDirHist7Offset),
        (W.Record.P2CpuDirHist0, W.Record.P2CpuDirHist0Offset),
        (W.Record.P2CpuDirHist1, W.Record.P2CpuDirHist1Offset),
        (W.Record.P2CpuDirHist2, W.Record.P2CpuDirHist2Offset),
        (W.Record.P2CpuDirHist3, W.Record.P2CpuDirHist3Offset),
        (W.Record.P2CpuDirHist4, W.Record.P2CpuDirHist4Offset),
        (W.Record.P2CpuDirHist5, W.Record.P2CpuDirHist5Offset),
        (W.Record.P2CpuDirHist6, W.Record.P2CpuDirHist6Offset),
        (W.Record.P2CpuDirHist7, W.Record.P2CpuDirHist7Offset),
        (W.Record.P1CpuDodgeDirHist0, W.Record.P1CpuDodgeDirHist0Offset),
        (W.Record.P1CpuDodgeDirHist1, W.Record.P1CpuDodgeDirHist1Offset),
        (W.Record.P1CpuDodgeDirHist2, W.Record.P1CpuDodgeDirHist2Offset),
        (W.Record.P1CpuDodgeDirHist3, W.Record.P1CpuDodgeDirHist3Offset),
        (W.Record.P1CpuDodgeDirHist4, W.Record.P1CpuDodgeDirHist4Offset),
        (W.Record.P1CpuDodgeDirHist5, W.Record.P1CpuDodgeDirHist5Offset),
        (W.Record.P1CpuDodgeDirHist6, W.Record.P1CpuDodgeDirHist6Offset),
        (W.Record.P1CpuDodgeDirHist7, W.Record.P1CpuDodgeDirHist7Offset),
        (W.Record.P2CpuDodgeDirHist0, W.Record.P2CpuDodgeDirHist0Offset),
        (W.Record.P2CpuDodgeDirHist1, W.Record.P2CpuDodgeDirHist1Offset),
        (W.Record.P2CpuDodgeDirHist2, W.Record.P2CpuDodgeDirHist2Offset),
        (W.Record.P2CpuDodgeDirHist3, W.Record.P2CpuDodgeDirHist3Offset),
        (W.Record.P2CpuDodgeDirHist4, W.Record.P2CpuDodgeDirHist4Offset),
        (W.Record.P2CpuDodgeDirHist5, W.Record.P2CpuDodgeDirHist5Offset),
        (W.Record.P2CpuDodgeDirHist6, W.Record.P2CpuDodgeDirHist6Offset),
        (W.Record.P2CpuDodgeDirHist7, W.Record.P2CpuDodgeDirHist7Offset),
        (W.Record.P1Item0Kind, W.Record.P1Item0KindOffset),
        (W.Record.P1Item0X, W.Record.P1Item0XOffset),
        (W.Record.P1Item0Y, W.Record.P1Item0YOffset),
        (W.Record.P1Item0Valid, W.Record.P1Item0ValidOffset),
        (W.Record.P1Item1Kind, W.Record.P1Item1KindOffset),
        (W.Record.P1Item1X, W.Record.P1Item1XOffset),
        (W.Record.P1Item1Y, W.Record.P1Item1YOffset),
        (W.Record.P1Item1Valid, W.Record.P1Item1ValidOffset),
        (W.Record.P1Item2Kind, W.Record.P1Item2KindOffset),
        (W.Record.P1Item2X, W.Record.P1Item2XOffset),
        (W.Record.P1Item2Y, W.Record.P1Item2YOffset),
        (W.Record.P1Item2Valid, W.Record.P1Item2ValidOffset),
        (W.Record.P1Item3Kind, W.Record.P1Item3KindOffset),
        (W.Record.P1Item3X, W.Record.P1Item3XOffset),
        (W.Record.P1Item3Y, W.Record.P1Item3YOffset),
        (W.Record.P1Item3Valid, W.Record.P1Item3ValidOffset),
        (W.Record.P2Item0Kind, W.Record.P2Item0KindOffset),
        (W.Record.P2Item0X, W.Record.P2Item0XOffset),
        (W.Record.P2Item0Y, W.Record.P2Item0YOffset),
        (W.Record.P2Item0Valid, W.Record.P2Item0ValidOffset),
        (W.Record.P2Item1Kind, W.Record.P2Item1KindOffset),
        (W.Record.P2Item1X, W.Record.P2Item1XOffset),
        (W.Record.P2Item1Y, W.Record.P2Item1YOffset),
        (W.Record.P2Item1Valid, W.Record.P2Item1ValidOffset),
        (W.Record.P2Item2Kind, W.Record.P2Item2KindOffset),
        (W.Record.P2Item2X, W.Record.P2Item2XOffset),
        (W.Record.P2Item2Y, W.Record.P2Item2YOffset),
        (W.Record.P2Item2Valid, W.Record.P2Item2ValidOffset),
        (W.Record.P2Item3Kind, W.Record.P2Item3KindOffset),
        (W.Record.P2Item3X, W.Record.P2Item3XOffset),
        (W.Record.P2Item3Y, W.Record.P2Item3YOffset),
        (W.Record.P2Item3Valid, W.Record.P2Item3ValidOffset),
        (W.Record.P1CpuTargetX, W.Record.P1CpuTargetXOffset),
        (W.Record.P2CpuTargetX, W.Record.P2CpuTargetXOffset),
        (W.Record.P1CpuTargetY, W.Record.P1CpuTargetYOffset),
        (W.Record.P2CpuTargetY, W.Record.P2CpuTargetYOffset),
        (W.Record.P1EnemyPrioX, W.Record.P1EnemyPrioXOffset),
        (W.Record.P1EnemyPrioY, W.Record.P1EnemyPrioYOffset),
        (W.Record.P1EnemyPrioFlags, W.Record.P1EnemyPrioFlagsOffset),
        (W.Record.P2EnemyPrioX, W.Record.P2EnemyPrioXOffset),
        (W.Record.P2EnemyPrioY, W.Record.P2EnemyPrioYOffset),
        (W.Record.P2EnemyPrioFlags, W.Record.P2EnemyPrioFlagsOffset),
        (W.Record.P1EnemyFirstX, W.Record.P1EnemyFirstXOffset),
        (W.Record.P1EnemyFirstY, W.Record.P1EnemyFirstYOffset),
        (W.Record.P1EnemyFirstFlags, W.Record.P1EnemyFirstFlagsOffset),
        (W.Record.P2EnemyFirstX, W.Record.P2EnemyFirstXOffset),
        (W.Record.P2EnemyFirstY, W.Record.P2EnemyFirstYOffset),
        (W.Record.P2EnemyFirstFlags, W.Record.P2EnemyFirstFlagsOffset),
        (W.Record.P1SlowMultX, W.Record.P1SlowMultXOffset),
        (W.Record.P2SlowMultX, W.Record.P2SlowMultXOffset),
        (W.Record.P1SlowMultY, W.Record.P1SlowMultYOffset),
        (W.Record.P2SlowMultY, W.Record.P2SlowMultYOffset),
        (W.Record.P1EnemySubMask, W.Record.P1EnemySubMaskOffset),
        (W.Record.P2EnemySubMask, W.Record.P2EnemySubMaskOffset),
        (W.Record.SeqEnd, W.Record.SeqEndOffset),
    ];

    public static long RecordOffset(uint index) => HeaderSize + (long)(index & IndexMask) * RecordSize;

    public static double FloatFromBits(uint bits) => BitConverter.UInt32BitsToSingle(bits);

    public static int S32(uint value) => unchecked((int)value);

    public static (uint C2, uint C3, uint Boss) UnpackEnemyClassCounts(uint word)
        => (word & 0xFFu, (word >> 8) & 0xFFu, (word >> 16) & 0xFFu);

    public static void VerifyHeader(TickBusHeader header)
    {
        var bad = RejectReason(header);
        if (bad is null) return;
        throw new TickBusLayoutException(bad switch
        {
            HeaderReject.Magic => $"magic が違います: 0x{header.Magic:X8}（期待 0x{Magic:X8}）。共有メモリが Tick Bus ではありません。",
            HeaderReject.Version => $"version が違います: {header.Version}（期待 {Version}）。DLL と Python の世代が食い違っています。",
            HeaderReject.RecordSize => $"record_size が違います: {header.RecordSizeWord}（期待 {RecordSize}）",
            _ => $"capacity が違います: {header.CapacityWord}（期待 {Capacity}）",
        });
    }

    public enum HeaderReject { Magic, Version, RecordSize, Capacity }

    public static HeaderReject? RejectReason(TickBusHeader h)
    {
        if (h.Magic != Magic) return HeaderReject.Magic;
        if (h.Version != Version) return HeaderReject.Version;
        if (h.RecordSizeWord != RecordSize) return HeaderReject.RecordSize;
        if (h.CapacityWord != Capacity) return HeaderReject.Capacity;
        return null;
    }

    public static void SelfCheck()
    {
        Check("ヘッダ", HeaderFields, W.Header.All, HeaderSize);
        Check("レコード", RecordFields, W.Record.All, RecordSize);
        if ((Capacity & (int)IndexMask) != 0)
            throw new TickBusLayoutException($"CAPACITY は 2 の冪でなければなりません: {Capacity}");

        static void Check(string label, (string Name, int Offset)[] table, string[] all, int limit)
        {
            if (table.Length != all.Length)
                throw new TickBusLayoutException(
                    $"{label}: 表の語数 {table.Length} が生成物の {all.Length} と違います（tickbus.h が増えた？）");
            for (int i = 0; i < table.Length; i++)
                if (!string.Equals(table[i].Name, all[i], StringComparison.Ordinal))
                    throw new TickBusLayoutException(
                        $"{label}: {i} 番目の語が違います（表 {table[i].Name} / 生成物 {all[i]}）");
            int prev = -1;
            foreach (var (name, off) in table)
            {
                if (off % 4 != 0) throw new TickBusLayoutException($"{label} {name}: オフセット 0x{off:X} が 4 の倍数ではありません");
                if (off <= prev) throw new TickBusLayoutException($"{label} {name}: オフセットが昇順ではありません");
                if (off + 4 > limit) throw new TickBusLayoutException($"{label} {name}: オフセットが範囲外です");
                prev = off;
            }
        }
    }
}
