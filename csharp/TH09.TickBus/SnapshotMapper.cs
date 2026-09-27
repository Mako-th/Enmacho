using R = TH09.Generated.TickWords.Record;

namespace TH09.TickBus;

public readonly struct SnapValue
{
    public enum K { Int, Float, Str }

    public K Kind { get; }
    public long IntValue { get; }
    public double FloatValue { get; }
    public string? StrValue { get; }

    private SnapValue(K kind, long i, double f, string? s)
    {
        Kind = kind; IntValue = i; FloatValue = f; StrValue = s;
    }

    public static SnapValue Int(long v) => new(K.Int, v, 0, null);
    public static SnapValue Float(double v) => new(K.Float, 0, v, null);
    public static SnapValue Str(string v) => new(K.Str, 0, 0, v);
}

public static class SnapshotMapper
{
    public const string ExecReplay = "Replay Playback";
    public const string ExecNet = "Network Play (Adonis)";
    public const string ExecLive = "Live Play";

    public static (string Label, int ReplayActive, uint ReplayFlag) ExecutionType(TickRecord rec, bool adonisLoaded)
    {
        uint replayFlag = rec.At(R.ReplayFlagOffset);
        int replayActive = (rec.At(R.FlagsOffset) & TickBusBits.FlagReplaymgrValid) != 0 && replayFlag == 1 ? 1 : 0;
        string label = replayActive != 0 ? ExecReplay : adonisLoaded ? ExecNet : ExecLive;
        return (label, replayActive, replayFlag);
    }

    public static List<(string Name, SnapValue Value)> FromRecord(TickRecord rec, bool adonisLoaded = false)
    {
        SnapValue U(int off) => SnapValue.Int(rec.At(off));
        SnapValue F(int off) => SnapValue.Float(TickBusLayout.FloatFromBits(rec.At(off)));
        SnapValue I(int off) => SnapValue.Int(TickBusLayout.S32(rec.At(off)));
        SnapValue X10(int off) => SnapValue.Int((long)rec.At(off) * 10);

        var (execLabel, replayActive, replayFlag) = ExecutionType(rec, adonisLoaded);
        var c1 = TickBusLayout.UnpackEnemyClassCounts(rec.At(R.P1EnemyClassCountsOffset));
        var c2 = TickBusLayout.UnpackEnemyClassCounts(rec.At(R.P2EnemyClassCountsOffset));

        return
        [
            ("mode", U(R.ModeOffset)),
            ("difficulty", U(R.DifficultyOffset)),
            ("stage_index", U(R.StageIndexOffset)),
            ("field_id", U(R.FieldIdOffset)),
            ("battle_bgm_id", U(R.BattleBgmIdOffset)),
            ("p1_character", U(R.P1CharacterOffset)),
            ("p2_character", U(R.P2CharacterOffset)),
            ("p1_control", U(R.P1ControlOffset)),
            ("p2_control", U(R.P2ControlOffset)),
            ("p1_cpu_level", U(R.P1CpuLevelOffset)),
            ("p2_cpu_level", U(R.P2CpuLevelOffset)),
            ("round_frames", U(R.RoundFramesOffset)),
            ("completed_rounds", U(R.CompletedRoundsOffset)),
            ("rounds_required", U(R.RoundsRequiredOffset)),
            ("p1_wins", U(R.P1WinsOffset)),
            ("p2_wins", U(R.P2WinsOffset)),
            ("result_state", U(R.ResultStateOffset)),
            ("result_winner", U(R.ResultWinnerOffset)),
            ("pause_used", U(R.PauseUsedOffset)),
            ("input_mask", U(R.InputMaskOffset)),
            ("p1_life_raw", U(R.P1LifeRawOffset)),
            ("p2_life_raw", U(R.P2LifeRawOffset)),
            ("p1_gauge", F(R.P1GaugeOffset)),
            ("p2_gauge", F(R.P2GaugeOffset)),
            ("p1_lives", F(R.P1LivesOffset)),
            ("p2_lives", F(R.P2LivesOffset)),
            ("p1_score", X10(R.P1ScoreRawOffset)),
            ("p2_score", X10(R.P2ScoreRawOffset)),
            ("p1_score_mirror", X10(R.P1ScoreMirrorOffset)),
            ("p2_score_mirror", X10(R.P2ScoreMirrorOffset)),
            ("p1_current_combo", U(R.P1CurrentComboOffset)),
            ("p2_current_combo", U(R.P2CurrentComboOffset)),
            ("p1_max_combo", U(R.P1MaxComboOffset)),
            ("p2_max_combo", U(R.P2MaxComboOffset)),
            ("p1_spell_points", U(R.P1SpellPointsOffset)),
            ("p2_spell_points", U(R.P2SpellPointsOffset)),
            ("p1_spell_attacks", U(R.P1SpellAttacksOffset)),
            ("p2_spell_attacks", U(R.P2SpellAttacksOffset)),
            ("p1_boss_attacks", U(R.P1BossAttacksOffset)),
            ("p2_boss_attacks", U(R.P2BossAttacksOffset)),
            ("p1_boss_reversals", U(R.P1BossReversalsOffset)),
            ("p2_boss_reversals", U(R.P2BossReversalsOffset)),
            ("clear_life_bonus", U(R.ClearLifeBonusOffset)),
            ("clear_max_combo_bonus", U(R.ClearMaxComboBonusOffset)),
            ("clear_spell_bonus", U(R.ClearSpellBonusOffset)),
            ("clear_boss_bonus", U(R.ClearBossBonusOffset)),
            ("clear_reversal_bonus", U(R.ClearReversalBonusOffset)),
            ("clear_lives_bonus", U(R.ClearLivesBonusOffset)),
            ("clear_total", U(R.ClearTotalOffset)),
            ("execution_type", SnapValue.Str(execLabel)),
            ("replay_active", SnapValue.Int(replayActive)),
            ("replay_flag", SnapValue.Int(replayFlag)),
            ("p1_cpu_dodge_mode", I(R.P1CpuDodgeModeOffset)),
            ("p2_cpu_dodge_mode", I(R.P2CpuDodgeModeOffset)),
            ("p1_cpu_quick_disable_timer", I(R.P1CpuQuickTimerOffset)),
            ("p2_cpu_quick_disable_timer", I(R.P2CpuQuickTimerOffset)),
            ("p1_cpu_standstill_timer", I(R.P1CpuStandTimerOffset)),
            ("p2_cpu_standstill_timer", I(R.P2CpuStandTimerOffset)),
            ("p1_zero_hit_timer", I(R.P1ZeroHitTimerOffset)),
            ("p2_zero_hit_timer", I(R.P2ZeroHitTimerOffset)),
            ("p1_combo_gauge_raw", I(R.P1ComboGaugeRawOffset)),
            ("p2_combo_gauge_raw", I(R.P2ComboGaugeRawOffset)),
            ("p1_enemy_total", U(R.P1EnemyTotalOffset)),
            ("p2_enemy_total", U(R.P2EnemyTotalOffset)),
            ("p1_enemy_fairy", U(R.P1EnemyFairyOffset)),
            ("p2_enemy_fairy", U(R.P2EnemyFairyOffset)),
            ("p1_enemy_boss", U(R.P1EnemyBossOffset)),
            ("p2_enemy_boss", U(R.P2EnemyBossOffset)),
            ("p1_enemy_charge", U(R.P1EnemyChargeOffset)),
            ("p2_enemy_charge", U(R.P2EnemyChargeOffset)),
            ("p1_bullet_fairy", U(R.P1BulletFairyOffset)),
            ("p2_bullet_fairy", U(R.P2BulletFairyOffset)),
            ("p1_bullet_rival", U(R.P1BulletRivalOffset)),
            ("p2_bullet_rival", U(R.P2BulletRivalOffset)),
            ("internal_rank", U(R.InternalRankOffset)),
            ("rank_interval", U(R.RankIntervalOffset)),
            ("rank_max", U(R.RankMaxOffset)),
            ("p1_charge", F(R.P1ChargeOffset)),
            ("p2_charge", F(R.P2ChargeOffset)),
            ("p1_card_attack_level", U(R.P1CardAttackLevelOffset)),
            ("p2_card_attack_level", U(R.P2CardAttackLevelOffset)),
            ("p1_boss_card_attack_level", U(R.P1BossCardAttackLevelOffset)),
            ("p2_boss_card_attack_level", U(R.P2BossCardAttackLevelOffset)),
            ("p1_boss_type", U(R.P1BossTypeOffset)),
            ("p2_boss_type", U(R.P2BossTypeOffset)),
            ("p1_boss_sub", U(R.P1BossSubOffset)),
            ("p2_boss_sub", U(R.P2BossSubOffset)),
            ("p1_boss_depth", U(R.P1BossDepthOffset)),
            ("p2_boss_depth", U(R.P2BossDepthOffset)),
            ("p1_boss_hp", U(R.P1BossHpOffset)),
            ("p2_boss_hp", U(R.P2BossHpOffset)),
            ("p1_ex_active", U(R.P1ExActiveOffset)),
            ("p2_ex_active", U(R.P2ExActiveOffset)),
            ("p1_ex_triggered", U(R.P1ExTriggeredOffset)),
            ("p2_ex_triggered", U(R.P2ExTriggeredOffset)),
            ("p1_cpu_quick_disable_timer_cur", I(R.P1CpuQuickTimerCurOffset)),
            ("p2_cpu_quick_disable_timer_cur", I(R.P2CpuQuickTimerCurOffset)),
            ("p1_cpu_standstill_timer_cur", I(R.P1CpuStandTimerCurOffset)),
            ("p2_cpu_standstill_timer_cur", I(R.P2CpuStandTimerCurOffset)),
            ("p1_combo_gauge_cur", I(R.P1ComboGaugeCurOffset)),
            ("p2_combo_gauge_cur", I(R.P2ComboGaugeCurOffset)),
            ("lily_counter", U(R.LilyCounterOffset)),
            ("p1_pos_x", F(R.P1PosXOffset)),
            ("p2_pos_x", F(R.P2PosXOffset)),
            ("p1_pos_y", F(R.P1PosYOffset)),
            ("p2_pos_y", F(R.P2PosYOffset)),
            ("p1_input_replay", U(R.P1InputReplayOffset)),
            ("p2_input_replay", U(R.P2InputReplayOffset)),
            ("p1_c2_count", SnapValue.Int(c1.C2)),
            ("p1_c3_count", SnapValue.Int(c1.C3)),
            ("p1_boss_count", SnapValue.Int(c1.Boss)),
            ("p2_c2_count", SnapValue.Int(c2.C2)),
            ("p2_c3_count", SnapValue.Int(c2.C3)),
            ("p2_boss_count", SnapValue.Int(c2.Boss)),
            ("p1_white_bullet_points", I(R.P1WhiteBulletPointsOffset)),
            ("p2_white_bullet_points", I(R.P2WhiteBulletPointsOffset)),
            ("p1_ghost_points", I(R.P1GhostPointsOffset)),
            ("p2_ghost_points", I(R.P2GhostPointsOffset)),
            ("p1_ex_points", I(R.P1ExPointsOffset)),
            ("p2_ex_points", I(R.P2ExPointsOffset)),
            ("p1_boss_pos_x", F(R.P1BossPosXOffset)),
            ("p1_boss_pos_y", F(R.P1BossPosYOffset)),
            ("p2_boss_pos_x", F(R.P2BossPosXOffset)),
            ("p2_boss_pos_y", F(R.P2BossPosYOffset)),
            ("p1_cpu_charge_instruction", F(R.P1CpuChargeInstructionOffset)),
            ("p2_cpu_charge_instruction", F(R.P2CpuChargeInstructionOffset)),
            ("rng_state", U(R.RngStateOffset)),
            ("hit_damage_base", U(R.HitDamageBaseOffset)),
            ("p1_hit_kind", U(R.P1HitKindOffset)),
            ("p2_hit_kind", U(R.P2HitKindOffset)),
            ("p1_hit_obj", U(R.P1HitObjOffset)),
            ("p2_hit_obj", U(R.P2HitObjOffset)),
            ("p1_hit_x", F(R.P1HitXOffset)),
            ("p2_hit_x", F(R.P2HitXOffset)),
            ("p1_hit_y", F(R.P1HitYOffset)),
            ("p2_hit_y", F(R.P2HitYOffset)),
            ("coord_ring_state", U(R.CoordRingStateOffset)),
            ("os_interrupt_time_lo", U(R.OsInterruptTimeLoOffset)),
            ("os_tick_count_lo", U(R.OsTickCountLoOffset)),
            ("p1_hit_obj_ptr", U(R.P1HitObjPtrOffset)),
            ("p2_hit_obj_ptr", U(R.P2HitObjPtrOffset)),
            ("p1_hit_laser_x", U(R.P1HitLaserXOffset)),
            ("p1_hit_laser_y", U(R.P1HitLaserYOffset)),
            ("p2_hit_laser_x", U(R.P2HitLaserXOffset)),
            ("p2_hit_laser_y", U(R.P2HitLaserYOffset)),
            ("p1_hit_laser_angle", U(R.P1HitLaserAngleOffset)),
            ("p2_hit_laser_angle", U(R.P2HitLaserAngleOffset)),
            ("p1_hit_elem_radius", U(R.P1HitElemRadiusOffset)),
            ("p2_hit_elem_radius", U(R.P2HitElemRadiusOffset)),
            ("p1_player_state", U(R.P1PlayerStateOffset)),
            ("p2_player_state", U(R.P2PlayerStateOffset)),
            ("p1_invincible_timer", I(R.P1InvincibleTimerOffset)),
            ("p2_invincible_timer", I(R.P2InvincibleTimerOffset)),
            ("p1_bullet_mgr", U(R.P1BulletMgrOffset)),
            ("p2_bullet_mgr", U(R.P2BulletMgrOffset)),
            ("p1_speed_mult", F(R.P1SpeedMultOffset)),
            ("p2_speed_mult", F(R.P2SpeedMultOffset)),
            ("p1_hit_list_count", U(R.P1HitListCountOffset)),
            ("p2_hit_list_count", U(R.P2HitListCountOffset)),
            ("p1_game_flags", U(R.P1GameFlagsOffset)),
            ("p2_game_flags", U(R.P2GameFlagsOffset)),
            ("p1_move_dir_angle", F(R.P1MoveDirAngleOffset)),
            ("p2_move_dir_angle", F(R.P2MoveDirAngleOffset)),
            ("global_state", U(R.GlobalStateOffset)),
            ("p1_spell_points_mirror", U(R.P1SpellPointsMirrorOffset)),
            ("p2_spell_points_mirror", U(R.P2SpellPointsMirrorOffset)),
        ];
    }
}
