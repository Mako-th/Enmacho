#ifndef TH09_TICKBUS_H
#define TH09_TICKBUS_H

#include <stddef.h>

#define TICKBUS_NAME_W          L"Local\\TH09TickBus"
#define TICKBUS_MUTEX_NAME_W    L"Local\\TH09TickHook"

#define TICKBUS_MAGIC           0x42543954u
#define TICKBUS_VERSION         15u
#define TICKBUS_HEADER_SIZE     256u
#define TICKBUS_RECORD_SIZE     1088u
#define TICKBUS_CAPACITY        32768u
#define TICKBUS_INDEX_MASK      (TICKBUS_CAPACITY - 1u)
#define TICKBUS_TOTAL_SIZE      (TICKBUS_HEADER_SIZE + TICKBUS_RECORD_SIZE * TICKBUS_CAPACITY)

#define TICKBUS_SEQ_BUILDING    0xFFFFFFFFu

#define TICKBUS_STATE_IDLE      0u
#define TICKBUS_STATE_ARMED     1u
#define TICKBUS_STATE_RUNNING   2u
#define TICKBUS_STATE_DETACHED  3u
#define TICKBUS_STATE_ERROR     0xFu

#define TICKBUS_ERR_NONE            0u
#define TICKBUS_ERR_ALREADY         1u
#define TICKBUS_ERR_SIGNATURE       2u
#define TICKBUS_ERR_ALLOC           3u
#define TICKBUS_ERR_EIP_IN_RANGE    4u
#define TICKBUS_ERR_PROTECT         5u
#define TICKBUS_ERR_NO_THREADS      6u
#define TICKBUS_ERR_SNAPSHOT        7u
#define TICKBUS_ERR_NOT_ATTACHED    8u
#define TICKBUS_ERR_FOREIGN_PATCH   9u
#define TICKBUS_ERR_BUS_MISSING    10u
#define TICKBUS_ERR_BUS_LAYOUT     11u
#define TICKBUS_ERR_MULTITHREAD    12u
#define TICKBUS_ERR_UNREADABLE     13u
#define TICKBUS_ERR_TOO_MANY_THREADS 14u
#define TICKBUS_ERR_BAD_SITE       15u
#define TICKBUS_ERR_NO_VPATCH      16u

#define TICKBUS_CMD_FIELD_MIRROR_P1   0x20u

#define TICKBUS_CMD_FIELD_EXCLUSIVE   0x40u

#define TICKBUS_VPATCH_QUERY    0u
#define TICKBUS_VPATCH_APPLY    1u
#define TICKBUS_VPATCH_RESTORE  2u

#define TICKBUS_VPATCH_OFF      0u
#define TICKBUS_VPATCH_ON       1u
#define TICKBUS_VPATCH_FOREIGN  2u
#define TICKBUS_VPATCH_ABSENT   3u

#define TICKBUS_VPATCH_PACK(err, state) \
    (((unsigned int)(state) & 0xFu) << 8 | ((unsigned int)(err) & 0xFFu))
#define TICKBUS_VPATCH_ERROR(v)   ((v) & 0xFFu)
#define TICKBUS_VPATCH_STATE(v)   (((v) >> 8) & 0xFu)

#define TICKBUS_HIT_VALID             0x80000000u
#define TICKBUS_HIT_OBJ_VALID         0x00040000u
#define TICKBUS_HIT_TYPE_SHIFT        16u
#define TICKBUS_HIT_TYPE_MASK         0x3u
#define TICKBUS_HIT_OBJ_SPRITE_MASK   0xFFFFu
#define TICKBUS_HIT_OBJ_COLOR_SHIFT   16u
#define TICKBUS_HIT_TYPE_BULLET       0u
#define TICKBUS_HIT_TYPE_EX_CIRCLE    1u
#define TICKBUS_HIT_TYPE_LASER        2u
#define TICKBUS_HIT_TYPE_OTHER        3u

#define TICKBUS_HITLIST_VALID         0x80000000u
#define TICKBUS_HITLIST_COUNT_MASK    0x1FFu

#define TICKBUS_COORD_STATE_PRESENT     0x1u
#define TICKBUS_COORD_STATE_ENABLE      0x2u
#define TICKBUS_COORD_STATE_WRITTEN     0x4u
#define TICKBUS_COORD_STATE_DECIM_SHIFT 8u
#define TICKBUS_COORD_STATE_DECIM_MASK  0xFFFu
#define TICKBUS_COORD_STATE_PHASE_SHIFT 20u
#define TICKBUS_COORD_STATE_PHASE_MASK  0xFFFu
#define TICKBUS_COORD_DECIM_NONE        1u

#define TICKBUS_FLAG_PLAYERS_VALID    0x1u
#define TICKBUS_FLAG_GUI_VALID        0x2u
#define TICKBUS_FLAG_STATS_VALID      0x4u
#define TICKBUS_FLAG_REPLAYMGR_VALID  0x8u

#define TICKBUS_STATUS_PACK(state, err, mapped, ticks) \
    (((unsigned int)(state) & 0xFu) << 28 | \
     ((unsigned int)(err) & 0xFFu) << 20 | \
     ((unsigned int)(mapped) & 0x1u) << 19 | \
     ((unsigned int)(ticks) & 0x7FFFFu))
#define TICKBUS_STATUS_STATE(v)   (((v) >> 28) & 0xFu)
#define TICKBUS_STATUS_ERROR(v)   (((v) >> 20) & 0xFFu)
#define TICKBUS_STATUS_MAPPED(v)  (((v) >> 19) & 0x1u)
#define TICKBUS_STATUS_TICKS(v)   ((v) & 0x7FFFFu)

#define TICKBUS_STATIC_ASSERT(cond, tag) \
    typedef char tickbus_static_assert_##tag[(cond) ? 1 : -1]

#define TICKBUS_HEADER_FIELDS(X) \
    X(0x00, magic)        \
    X(0x04, version)      \
    X(0x08, record_size)  \
    X(0x0C, capacity)     \
    X(0x10, write_index)  \
    X(0x14, hook_state)   \
    X(0x18, last_error)   \
    X(0x1C, game_pid)     \
    X(0x20, hook_addr)    \
    X(0x24, writer_tid)   \
    X(0x28, dropped)      \
    X(0x2C, armed_ticks)  \
    X(0x30, cmd_fields)   \
    X(0x34, input_hook_addr) \
    X(0x38, input_ticks)  \
    X(0x3C, input_error)  \
    X(0x40, cmd_seq)      \
    X(0x44, cmd_mask)     \
    X(0x48, cmd_ticks)    \
    X(0x4C, cmd_ack)      \
    X(0x50, input_hits)   \
    X(0x54, input_last_ecx) \
    X(0x58, input_poll_ticks) \
    \
    X(0x5C, hit_hook_addr)    \
    X(0x60, hit_error)        \
    X(0x64, hit_ticks)        \
    \
    X(0x68, speed_hook_addr)  \
    X(0x6C, speed_error)      \
    X(0x70, speed_ticks)

#define TICKBUS_HEADER_TAIL_COUNT 35u

typedef struct TickBusHeader {
#define TICKBUS_X(off, name) unsigned int name;
    TICKBUS_HEADER_FIELDS(TICKBUS_X)
#undef TICKBUS_X
    unsigned int reserved_tail[TICKBUS_HEADER_TAIL_COUNT];
} TickBusHeader;

#define TICKBUS_X(off, name) \
    TICKBUS_STATIC_ASSERT(offsetof(TickBusHeader, name) == (off), hdr_##name);
TICKBUS_HEADER_FIELDS(TICKBUS_X)
#undef TICKBUS_X
TICKBUS_STATIC_ASSERT(sizeof(TickBusHeader) == TICKBUS_HEADER_SIZE, hdr_size);

#define TICKBUS_RECORD_FIELDS(X) \
    X(0x000, seq_begin)          \
    X(0x004, flags)              \
    \
    X(0x008, p1_enemy_class_counts) \
    X(0x00C, p2_enemy_class_counts) \
    X(0x010, mode)               \
    X(0x014, difficulty)         \
    X(0x018, stage_index)        \
    X(0x01C, field_id)           \
    X(0x020, battle_bgm_id)      \
    X(0x024, round_frames)       \
    X(0x028, completed_rounds)   \
    X(0x02C, rounds_required)    \
    X(0x030, pause_used)         \
    X(0x034, p1_wins)            \
    X(0x038, p2_wins)            \
    X(0x03C, result_state)       \
    X(0x040, result_winner)      \
    X(0x044, input_mask)         \
    X(0x048, replay_flag)        \
    X(0x04C, p1_character)       \
    X(0x050, p2_character)       \
    X(0x054, p1_control)         \
    X(0x058, p2_control)         \
    X(0x05C, p1_cpu_level)       \
    X(0x060, p2_cpu_level)       \
    X(0x064, p1_life_raw)        \
    X(0x068, p2_life_raw)        \
    X(0x06C, p1_lives)           \
    X(0x070, p2_lives)           \
    X(0x074, p1_score_raw)       \
    X(0x078, p2_score_raw)       \
    X(0x07C, p1_score_mirror)    \
    X(0x080, p2_score_mirror)    \
    X(0x084, p1_current_combo)   \
    X(0x088, p2_current_combo)   \
    X(0x08C, p1_max_combo)       \
    X(0x090, p2_max_combo)       \
    X(0x094, p1_spell_attacks)   \
    X(0x098, p2_spell_attacks)   \
    X(0x09C, p1_boss_attacks)    \
    X(0x0A0, p2_boss_attacks)    \
    X(0x0A4, p1_boss_reversals)  \
    X(0x0A8, p2_boss_reversals)  \
    X(0x0AC, p1_spell_points)    \
    X(0x0B0, p2_spell_points)    \
    \
    X(0x0B4, p1_spell_points_mirror) \
    X(0x0B8, p2_spell_points_mirror) \
    X(0x0BC, p1_gauge)           \
    X(0x0C0, p2_gauge)           \
    X(0x0C4, clear_life_bonus)   \
    X(0x0C8, clear_max_combo_bonus) \
    X(0x0CC, clear_spell_bonus)  \
    X(0x0D0, clear_boss_bonus)   \
    X(0x0D4, clear_reversal_bonus) \
    X(0x0D8, clear_lives_bonus)  \
    X(0x0DC, clear_total)        \
    \
    X(0x0E0, p1_cpu_dodge_mode)  \
    X(0x0E4, p2_cpu_dodge_mode)  \
    X(0x0E8, p1_cpu_quick_timer) \
    X(0x0EC, p2_cpu_quick_timer) \
    X(0x0F0, p1_cpu_stand_timer) \
    X(0x0F4, p2_cpu_stand_timer) \
    X(0x0F8, p1_zero_hit_timer)  \
    X(0x0FC, p2_zero_hit_timer)  \
    X(0x100, p1_combo_gauge_raw) \
    X(0x104, p2_combo_gauge_raw) \
    X(0x108, p1_enemy_total)    \
    X(0x10C, p2_enemy_total)    \
    X(0x110, p1_enemy_fairy)    \
    X(0x114, p2_enemy_fairy)    \
    X(0x118, p1_enemy_boss)     \
    X(0x11C, p2_enemy_boss)     \
    X(0x120, p1_enemy_charge)   \
    X(0x124, p2_enemy_charge)   \
    X(0x128, p1_bullet_fairy)   \
    X(0x12C, p2_bullet_fairy)   \
    X(0x130, p1_bullet_rival)   \
    X(0x134, p2_bullet_rival)   \
    \
    X(0x138, internal_rank)      \
    X(0x13C, rank_interval)      \
    X(0x140, rank_max)           \
    X(0x144, p1_charge)          \
    X(0x148, p2_charge)          \
    X(0x14C, p1_card_attack_level) \
    X(0x150, p2_card_attack_level) \
    X(0x154, p1_boss_card_attack_level) \
    X(0x158, p2_boss_card_attack_level) \
    \
    X(0x15C, p1_boss_type)       \
    X(0x160, p2_boss_type)       \
    X(0x164, p1_boss_sub)        \
    X(0x168, p2_boss_sub)        \
    X(0x16C, p1_boss_depth)      \
    X(0x170, p2_boss_depth)      \
    X(0x174, p1_boss_hp)         \
    X(0x178, p2_boss_hp)         \
    X(0x17C, p1_ex_active)       \
    X(0x180, p2_ex_active)       \
    X(0x184, p1_ex_triggered)    \
    X(0x188, p2_ex_triggered)    \
    X(0x18C, p1_cpu_quick_timer_cur) \
    X(0x190, p2_cpu_quick_timer_cur) \
    X(0x194, p1_cpu_stand_timer_cur) \
    X(0x198, p2_cpu_stand_timer_cur) \
    X(0x19C, p1_combo_gauge_cur)     \
    X(0x1A0, p2_combo_gauge_cur)     \
    X(0x1A4, lily_counter)       \
    X(0x1A8, p1_pos_x)           \
    X(0x1AC, p2_pos_x)           \
    X(0x1B0, p1_pos_y)           \
    X(0x1B4, p2_pos_y)           \
    X(0x1B8, p1_input_replay)    \
    X(0x1BC, p2_input_replay)    \
    \
    \
    X(0x1C0, p1_white_bullet_points) \
    X(0x1C4, p2_white_bullet_points) \
    X(0x1C8, p1_ghost_points)    \
    X(0x1CC, p2_ghost_points)    \
    X(0x1D0, p1_ex_points)       \
    X(0x1D4, p2_ex_points)       \
    \
    X(0x1D8, p1_boss_pos_x)      \
    X(0x1DC, p2_boss_pos_x)      \
    X(0x1E0, p1_boss_pos_y)      \
    X(0x1E4, p2_boss_pos_y)      \
    \
    X(0x1E8, p1_cpu_charge_instruction) \
    X(0x1EC, p2_cpu_charge_instruction) \
    \
    X(0x1F0, rng_state)          \
    \
    X(0x1F4, p1_hit_kind)        \
    X(0x1F8, p1_hit_obj)         \
    X(0x1FC, p1_hit_x)           \
    X(0x200, p1_hit_y)           \
    X(0x204, p2_hit_kind)        \
    X(0x208, p2_hit_obj)         \
    X(0x20C, p2_hit_x)           \
    X(0x210, p2_hit_y)           \
    \
    X(0x214, hit_damage_base) \
    \
    X(0x218, coord_ring_state) \
    \
    X(0x21C, os_interrupt_time_lo) \
    X(0x220, os_tick_count_lo) \
    \
    X(0x224, p1_hit_obj_ptr) \
    X(0x228, p2_hit_obj_ptr) \
    \
    X(0x22C, p1_hit_laser_x) \
    X(0x230, p1_hit_laser_y) \
    X(0x234, p2_hit_laser_x) \
    X(0x238, p2_hit_laser_y) \
    \
    X(0x23C, p1_hit_laser_angle) \
    X(0x240, p2_hit_laser_angle) \
    \
    X(0x244, p1_hit_elem_radius) \
    X(0x248, p2_hit_elem_radius) \
    \
    X(0x24C, p1_player_state) \
    X(0x250, p2_player_state) \
    X(0x254, p1_invincible_timer) \
    X(0x258, p2_invincible_timer) \
    \
    X(0x25C, p1_bullet_mgr) \
    X(0x260, p2_bullet_mgr) \
    \
    X(0x264, p1_speed_mult) \
    X(0x268, p2_speed_mult) \
    X(0x26C, p1_hit_list_count) \
    X(0x270, p2_hit_list_count) \
    \
    X(0x274, p1_game_flags) \
    X(0x278, p2_game_flags) \
    X(0x27C, global_state) \
    \
    X(0x280, p1_move_dir_angle) \
    X(0x284, p2_move_dir_angle) \
    \
    X(0x288, p1_cpu_dir_lock) \
    X(0x28C, p2_cpu_dir_lock) \
    X(0x290, p1_cpu_prev_dir) \
    X(0x294, p2_cpu_prev_dir) \
    \
    X(0x298, p1_cpu_dir_hist0) \
    X(0x29C, p1_cpu_dir_hist1) \
    X(0x2A0, p1_cpu_dir_hist2) \
    X(0x2A4, p1_cpu_dir_hist3) \
    X(0x2A8, p1_cpu_dir_hist4) \
    X(0x2AC, p1_cpu_dir_hist5) \
    X(0x2B0, p1_cpu_dir_hist6) \
    X(0x2B4, p1_cpu_dir_hist7) \
    X(0x2B8, p2_cpu_dir_hist0) \
    X(0x2BC, p2_cpu_dir_hist1) \
    X(0x2C0, p2_cpu_dir_hist2) \
    X(0x2C4, p2_cpu_dir_hist3) \
    X(0x2C8, p2_cpu_dir_hist4) \
    X(0x2CC, p2_cpu_dir_hist5) \
    X(0x2D0, p2_cpu_dir_hist6) \
    X(0x2D4, p2_cpu_dir_hist7) \
    \
    X(0x2D8, p1_cpu_dodge_dir_hist0) \
    X(0x2DC, p1_cpu_dodge_dir_hist1) \
    X(0x2E0, p1_cpu_dodge_dir_hist2) \
    X(0x2E4, p1_cpu_dodge_dir_hist3) \
    X(0x2E8, p1_cpu_dodge_dir_hist4) \
    X(0x2EC, p1_cpu_dodge_dir_hist5) \
    X(0x2F0, p1_cpu_dodge_dir_hist6) \
    X(0x2F4, p1_cpu_dodge_dir_hist7) \
    X(0x2F8, p2_cpu_dodge_dir_hist0) \
    X(0x2FC, p2_cpu_dodge_dir_hist1) \
    X(0x300, p2_cpu_dodge_dir_hist2) \
    X(0x304, p2_cpu_dodge_dir_hist3) \
    X(0x308, p2_cpu_dodge_dir_hist4) \
    X(0x30C, p2_cpu_dodge_dir_hist5) \
    X(0x310, p2_cpu_dodge_dir_hist6) \
    X(0x314, p2_cpu_dodge_dir_hist7) \
    \
    X(0x318, p1_item0_kind) \
    X(0x31C, p1_item0_x) \
    X(0x320, p1_item0_y) \
    X(0x324, p1_item0_valid) \
    X(0x328, p1_item1_kind) \
    X(0x32C, p1_item1_x) \
    X(0x330, p1_item1_y) \
    X(0x334, p1_item1_valid) \
    X(0x338, p1_item2_kind) \
    X(0x33C, p1_item2_x) \
    X(0x340, p1_item2_y) \
    X(0x344, p1_item2_valid) \
    X(0x348, p1_item3_kind) \
    X(0x34C, p1_item3_x) \
    X(0x350, p1_item3_y) \
    X(0x354, p1_item3_valid) \
    X(0x358, p2_item0_kind) \
    X(0x35C, p2_item0_x) \
    X(0x360, p2_item0_y) \
    X(0x364, p2_item0_valid) \
    X(0x368, p2_item1_kind) \
    X(0x36C, p2_item1_x) \
    X(0x370, p2_item1_y) \
    X(0x374, p2_item1_valid) \
    X(0x378, p2_item2_kind) \
    X(0x37C, p2_item2_x) \
    X(0x380, p2_item2_y) \
    X(0x384, p2_item2_valid) \
    X(0x388, p2_item3_kind) \
    X(0x38C, p2_item3_x) \
    X(0x390, p2_item3_y) \
    X(0x394, p2_item3_valid) \
    \
    X(0x398, p1_cpu_target_x) \
    X(0x39C, p2_cpu_target_x) \
    X(0x3A0, p1_cpu_target_y) \
    X(0x3A4, p2_cpu_target_y) \
    \
    X(0x3A8, p1_enemy_prio_x) \
    X(0x3AC, p1_enemy_prio_y) \
    X(0x3B0, p1_enemy_prio_flags) \
    X(0x3B4, p2_enemy_prio_x) \
    X(0x3B8, p2_enemy_prio_y) \
    X(0x3BC, p2_enemy_prio_flags) \
    X(0x3C0, p1_enemy_first_x) \
    X(0x3C4, p1_enemy_first_y) \
    X(0x3C8, p1_enemy_first_flags) \
    X(0x3CC, p2_enemy_first_x) \
    X(0x3D0, p2_enemy_first_y) \
    X(0x3D4, p2_enemy_first_flags) \
    \
    X(0x3D8, p1_slow_mult_x) \
    X(0x3DC, p2_slow_mult_x) \
    X(0x3E0, p1_slow_mult_y) \
    X(0x3E4, p2_slow_mult_y) \
    \
    X(0x3E8, p1_enemy_sub_mask) \
    X(0x3EC, p2_enemy_sub_mask)

#define TICKBUS_RECORD_TAIL_COUNT 19u
#define TICKBUS_RECORD_SEQ_END_OFF 0x43Cu

typedef struct TickRecord {
#define TICKBUS_X(off, name) unsigned int name;
    TICKBUS_RECORD_FIELDS(TICKBUS_X)
#undef TICKBUS_X
    unsigned int reserved_tail[TICKBUS_RECORD_TAIL_COUNT];
    unsigned int seq_end;
} TickRecord;

#define TICKBUS_X(off, name) \
    TICKBUS_STATIC_ASSERT(offsetof(TickRecord, name) == (off), rec_##name);
TICKBUS_RECORD_FIELDS(TICKBUS_X)
#undef TICKBUS_X
TICKBUS_STATIC_ASSERT(offsetof(TickRecord, seq_end) == TICKBUS_RECORD_SEQ_END_OFF, rec_seq_end);
TICKBUS_STATIC_ASSERT(sizeof(TickRecord) == TICKBUS_RECORD_SIZE, rec_size);
TICKBUS_STATIC_ASSERT((TICKBUS_CAPACITY & TICKBUS_INDEX_MASK) == 0u, capacity_pow2);

static __inline void tickbus_barrier(void)
{
    __asm__ __volatile__("" ::: "memory");
}

static __inline unsigned int tickbus_current_tid(void)
{
    unsigned int tid;
    __asm__ __volatile__("movl %%fs:0x24, %0" : "=r"(tid));
    return tid;
}

static __inline volatile TickRecord *tickbus_record(volatile TickBusHeader *h,
                                                    unsigned int idx)
{
    volatile unsigned char *base = (volatile unsigned char *)h + TICKBUS_HEADER_SIZE;
    return (volatile TickRecord *)(base + (idx & TICKBUS_INDEX_MASK) * TICKBUS_RECORD_SIZE);
}

static __inline int tickbus_header_ok(volatile TickBusHeader *h)
{
    if (!h)
        return 0;
    if (h->magic != TICKBUS_MAGIC)
        return 0;
    if (h->version != TICKBUS_VERSION)
        return 0;
    if (h->record_size != TICKBUS_RECORD_SIZE)
        return 0;
    if (h->capacity != TICKBUS_CAPACITY)
        return 0;
    return 1;
}

static __inline volatile TickRecord *tickbus_begin_write(volatile TickBusHeader *h,
                                                         unsigned int *out_idx)
{
    volatile TickRecord *r;
    unsigned int idx = h->write_index;

    if (idx == TICKBUS_SEQ_BUILDING)
        idx = 0u;

    r = tickbus_record(h, idx);
    r->seq_begin = TICKBUS_SEQ_BUILDING;
    tickbus_barrier();
    *out_idx = idx;
    return r;
}

static __inline void tickbus_end_write(volatile TickBusHeader *h,
                                       volatile TickRecord *r, unsigned int idx)
{
    tickbus_barrier();
    r->seq_end = idx;
    tickbus_barrier();
    r->seq_begin = idx;
    tickbus_barrier();
    h->write_index = idx + 1u;
}


#define COORDBUS_NAME_W         L"Local\\TH09TickCoord"
#define COORDBUS_MAGIC          0x43543954u
#define COORDBUS_VERSION        10u
#define COORDBUS_HEADER_SIZE    256u

#define COORDBUS_BULLET_SLOTS   537u
#define COORDBUS_ENEMY_SLOTS    128u
#define COORDBUS_LASER_SLOTS    48u
#define COORDBUS_SIDE_SLOTS     (COORDBUS_BULLET_SLOTS + COORDBUS_ENEMY_SLOTS)
#define COORDBUS_LASER_TOTAL    (2u * COORDBUS_LASER_SLOTS)
#define COORDBUS_EX_SLOTS       256u
#define COORDBUS_SHOT_SLOTS     128u
#define COORDBUS_SHOT_TOTAL     (2u * COORDBUS_SHOT_SLOTS)
#define COORDBUS_BLAST_SLOTS    32u
#define COORDBUS_HITLIST_SLOTS  128u
#define COORDBUS_HITLIST_TOTAL  (2u * COORDBUS_HITLIST_SLOTS)
#define COORDBUS_SLOTS          (2u * COORDBUS_SIDE_SLOTS + COORDBUS_LASER_TOTAL \
                                 + COORDBUS_EX_SLOTS + COORDBUS_SHOT_TOTAL)

#define COORDBUS_BASE_P1_BULLET 0u
#define COORDBUS_BASE_P1_ENEMY  COORDBUS_BULLET_SLOTS
#define COORDBUS_BASE_P2_BULLET COORDBUS_SIDE_SLOTS
#define COORDBUS_BASE_P2_ENEMY  (COORDBUS_SIDE_SLOTS + COORDBUS_BULLET_SLOTS)
#define COORDBUS_BASE_P1_LASER  (2u * COORDBUS_SIDE_SLOTS)
#define COORDBUS_BASE_P2_LASER  (2u * COORDBUS_SIDE_SLOTS + COORDBUS_LASER_SLOTS)
#define COORDBUS_BASE_EX        (2u * COORDBUS_SIDE_SLOTS + COORDBUS_LASER_TOTAL)
#define COORDBUS_BASE_P1_SHOT   (2u * COORDBUS_SIDE_SLOTS + COORDBUS_LASER_TOTAL \
                                 + COORDBUS_EX_SLOTS)
#define COORDBUS_BASE_P2_SHOT   (COORDBUS_BASE_P1_SHOT + COORDBUS_SHOT_SLOTS)

#define COORDBUS_RECORD_SIZE    44040u
#define COORDBUS_CAPACITY       8192u
#define COORDBUS_INDEX_MASK     (COORDBUS_CAPACITY - 1u)
#define COORDBUS_TOTAL_SIZE     (COORDBUS_HEADER_SIZE + COORDBUS_RECORD_SIZE * COORDBUS_CAPACITY)

#define COORDBUS_FLAG_P1_BULLETS  0x1u
#define COORDBUS_FLAG_P1_ENEMIES  0x2u
#define COORDBUS_FLAG_P2_BULLETS  0x4u
#define COORDBUS_FLAG_P2_ENEMIES  0x8u
#define COORDBUS_FLAG_P1_LASERS   0x10u
#define COORDBUS_FLAG_P2_LASERS   0x20u
#define COORDBUS_FLAG_EX          0x40u
#define COORDBUS_FLAG_BULLET_ORIGIN 0x80u
#define COORDBUS_FLAG_P1_SHOTS    0x100u
#define COORDBUS_FLAG_P2_SHOTS    0x200u
#define COORDBUS_FLAG_ENEMY_KIND_IDX   0x400u
#define COORDBUS_FLAG_ENEMY_BLAST      0x1000u
#define COORDBUS_FLAG_HITLIST          0x2000u
#define COORDBUS_FLAG_LASER_ORIGIN     0x4000u
#define COORDBUS_FLAG_ENEMY_KIND_CLASH 0x800u

#define COORDBUS_ENEMY_KIND_WORD_MASK  0x00007FFFu
#define COORDBUS_ENEMY_KIND_IDX_SHIFT  16u
#define COORDBUS_ENEMY_KIND_IDX_MASK   0x000000FFu
#define COORDBUS_ENEMY_KIND_CLASH_MASK (~COORDBUS_ENEMY_KIND_WORD_MASK)
TICKBUS_STATIC_ASSERT((COORDBUS_ENEMY_KIND_WORD_MASK
                       & (COORDBUS_ENEMY_KIND_IDX_MASK
                          << COORDBUS_ENEMY_KIND_IDX_SHIFT)) == 0u,
                      cenemy_idx_no_overlap);
TICKBUS_STATIC_ASSERT((COORDBUS_ENEMY_KIND_WORD_MASK
                       & COORDBUS_ENEMY_KIND_CLASH_MASK) == 0u,
                      cenemy_clash_no_overlap);
TICKBUS_STATIC_ASSERT((COORDBUS_ENEMY_KIND_WORD_MASK & 0x8000u) == 0u, cenemy_bit15_free);
TICKBUS_STATIC_ASSERT(((COORDBUS_ENEMY_KIND_IDX_MASK
                        << COORDBUS_ENEMY_KIND_IDX_SHIFT) & 0x8000u) == 0u,
                      cenemy_bit15_not_idx);

#define COORDBUS_SHOT_KIND_SPRITE_MASK  0x000000FFu
#define COORDBUS_SHOT_KIND_BEHAV_SHIFT  8u
#define COORDBUS_SHOT_KIND_BEHAV_MASK   0x0000000Fu
#define COORDBUS_SHOT_KIND_ENTRY_SHIFT  12u
#define COORDBUS_SHOT_KIND_ENTRY_MASK   0x00000FFFu
#define COORDBUS_SHOT_STATE_FREE        0u
#define COORDBUS_SHOT_STATE_ALIVE       1u
#define COORDBUS_SHOT_STATE_VANISH      2u
TICKBUS_STATIC_ASSERT((COORDBUS_SHOT_KIND_SPRITE_MASK
                       & (COORDBUS_SHOT_KIND_BEHAV_MASK
                          << COORDBUS_SHOT_KIND_BEHAV_SHIFT)) == 0u,
                      cshot_behav_no_overlap);
TICKBUS_STATIC_ASSERT(((COORDBUS_SHOT_KIND_BEHAV_MASK
                        << COORDBUS_SHOT_KIND_BEHAV_SHIFT)
                       & (COORDBUS_SHOT_KIND_ENTRY_MASK
                          << COORDBUS_SHOT_KIND_ENTRY_SHIFT)) == 0u,
                      cshot_entry_no_overlap);
TICKBUS_STATIC_ASSERT((((unsigned long)COORDBUS_SHOT_KIND_ENTRY_MASK
                        << COORDBUS_SHOT_KIND_ENTRY_SHIFT)
                       | (COORDBUS_SHOT_KIND_BEHAV_MASK
                          << COORDBUS_SHOT_KIND_BEHAV_SHIFT)
                       | COORDBUS_SHOT_KIND_SPRITE_MASK) <= 0xFFFFFFuL,
                      cshot_kind_fits_u24);
TICKBUS_STATIC_ASSERT(COORDBUS_SHOT_STATE_FREE != COORDBUS_SHOT_STATE_ALIVE
                      && COORDBUS_SHOT_STATE_ALIVE != COORDBUS_SHOT_STATE_VANISH,
                      cshot_state_distinct);

#define COORDBUS_STATE_ALIVE_MASK   0x0007u
#define COORDBUS_ORIGIN_SITE_SHIFT  3u
#define COORDBUS_ORIGIN_SITE_MASK   0x001Fu
#define COORDBUS_ORIGIN_SITE_COUNT  18u
#define COORDBUS_ORIGIN_ENEMY_SHIFT 8u
#define COORDBUS_ORIGIN_ENEMY_MASK  0x00FFu
TICKBUS_STATIC_ASSERT(COORDBUS_ORIGIN_SITE_COUNT <= COORDBUS_ORIGIN_SITE_MASK,
                      corigin_site_fits);
TICKBUS_STATIC_ASSERT((COORDBUS_STATE_ALIVE_MASK
                       & (COORDBUS_ORIGIN_SITE_MASK << COORDBUS_ORIGIN_SITE_SHIFT)) == 0u,
                      corigin_site_no_overlap);
TICKBUS_STATIC_ASSERT(((COORDBUS_ORIGIN_SITE_MASK << COORDBUS_ORIGIN_SITE_SHIFT)
                       & (COORDBUS_ORIGIN_ENEMY_MASK << COORDBUS_ORIGIN_ENEMY_SHIFT)) == 0u,
                      corigin_enemy_no_overlap);
TICKBUS_STATIC_ASSERT(COORDBUS_ENEMY_SLOTS <= COORDBUS_ORIGIN_ENEMY_MASK,
                      corigin_enemy_fits);
TICKBUS_STATIC_ASSERT(((COORDBUS_ORIGIN_ENEMY_MASK << COORDBUS_ORIGIN_ENEMY_SHIFT)
                       | (COORDBUS_ORIGIN_SITE_MASK << COORDBUS_ORIGIN_SITE_SHIFT)
                       | COORDBUS_STATE_ALIVE_MASK) <= 0xFFFFu,
                      corigin_fits_u16);

#define COORDBUS_LASER_ORIGIN_SUB_SHIFT    COORDBUS_ORIGIN_SITE_SHIFT
#define COORDBUS_LASER_ORIGIN_SUB_MASK     COORDBUS_ORIGIN_SITE_MASK
#define COORDBUS_LASER_ORIGIN_ENEMY_SHIFT  COORDBUS_ORIGIN_ENEMY_SHIFT
#define COORDBUS_LASER_ORIGIN_ENEMY_MASK   COORDBUS_ORIGIN_ENEMY_MASK
#define COORDBUS_LASER_ORIGIN_SUB_CAP      30u
TICKBUS_STATIC_ASSERT(COORDBUS_LASER_ORIGIN_SUB_CAP < COORDBUS_LASER_ORIGIN_SUB_MASK,
                      claser_origin_sub_cap_below_mask);

#define COORDBUS_BLAST_PRESENT      0x00000001u
#define COORDBUS_BLAST_SIDE_SHIFT   1u
#define COORDBUS_BLAST_SIDE_MASK    0x00000001u
#define COORDBUS_BLAST_POS_OK       0x00000004u
#define COORDBUS_BLAST_ENEMY_SHIFT  8u
#define COORDBUS_BLAST_ENEMY_MASK   0x000000FFu
#define COORDBUS_BLAST_IDX_SHIFT    16u
#define COORDBUS_BLAST_IDX_MASK     0x000000FFu
TICKBUS_STATIC_ASSERT(COORDBUS_ENEMY_SLOTS <= COORDBUS_BLAST_ENEMY_MASK,
                      cblast_enemy_fits);
TICKBUS_STATIC_ASSERT((COORDBUS_BLAST_PRESENT
                       & (COORDBUS_BLAST_SIDE_MASK << COORDBUS_BLAST_SIDE_SHIFT)) == 0u,
                      cblast_present_side);
TICKBUS_STATIC_ASSERT(((COORDBUS_BLAST_SIDE_MASK << COORDBUS_BLAST_SIDE_SHIFT)
                       & COORDBUS_BLAST_POS_OK) == 0u, cblast_side_posok);
TICKBUS_STATIC_ASSERT(((COORDBUS_BLAST_ENEMY_MASK << COORDBUS_BLAST_ENEMY_SHIFT)
                       & (COORDBUS_BLAST_IDX_MASK << COORDBUS_BLAST_IDX_SHIFT)) == 0u,
                      cblast_enemy_idx);
TICKBUS_STATIC_ASSERT(((COORDBUS_BLAST_ENEMY_MASK << COORDBUS_BLAST_ENEMY_SHIFT)
                       & (COORDBUS_BLAST_PRESENT | COORDBUS_BLAST_POS_OK
                          | (COORDBUS_BLAST_SIDE_MASK
                             << COORDBUS_BLAST_SIDE_SHIFT))) == 0u,
                      cblast_low_bits);

#define COORDBUS_HEADER_FIELDS(X) \
    X(0x00, magic)         \
    X(0x04, version)       \
    X(0x08, record_size)   \
    X(0x0C, capacity)      \
    X(0x10, write_index)   \
    X(0x14, coord_state)   \
    X(0x18, last_error)    \
    X(0x1C, game_pid)      \
    X(0x20, slot_count)    \
    \
    X(0x24, enable)        \
    X(0x28, coord_ticks)   \
    X(0x2C, skipped_ticks) \
    \
    X(0x30, origin_hits)   \
    X(0x34, origin_dropped) \
    \
    X(0x38, origin_error) \
    \
    X(0x3C, enemy_kind_clash) \
    \
    X(0x40, blast_hits)    \
    X(0x44, blast_dropped) \
    X(0x48, blast_error)   \
    \
    X(0x4C, hitlist_clamped) \
    \
    X(0x50, laser_origin_hits) \
    X(0x54, laser_origin_dropped) \
    X(0x58, laser_origin_error)

#define COORDBUS_HEADER_TAIL_COUNT 41u

typedef struct CoordBusHeader {
#define TICKBUS_X(off, name) unsigned int name;
    COORDBUS_HEADER_FIELDS(TICKBUS_X)
#undef TICKBUS_X
    unsigned int reserved_tail[COORDBUS_HEADER_TAIL_COUNT];
} CoordBusHeader;

#define TICKBUS_X(off, name) \
    TICKBUS_STATIC_ASSERT(offsetof(CoordBusHeader, name) == (off), chdr_##name);
COORDBUS_HEADER_FIELDS(TICKBUS_X)
#undef TICKBUS_X
TICKBUS_STATIC_ASSERT(sizeof(CoordBusHeader) == COORDBUS_HEADER_SIZE, chdr_size);

#define COORDBUS_RECORD_FIELDS(X) \
    X(0x0000, seq_begin)   \
    X(0x0004, flags)       \
    X(0x0008, x)           \
    X(0x1E50, y)           \
    X(0x3C98, kind)        \
    X(0x5AE0, laser_angle) \
    X(0x5C60, laser_tail)  \
    X(0x5DE0, laser_head)  \
    X(0x5F60, laser_width) \
    X(0x60E0, laser_timer) \
    X(0x6260, laser_gate0) \
    X(0x63E0, laser_gate2) \
    X(0x6560, laser_phase) \
    X(0x66E0, ex_timer)    \
    X(0x6AE0, ex_side)     \
    X(0x6EE0, ex_variant)  \
    X(0x72E0, shot_w)      \
    X(0x76E0, shot_h)      \
    X(0x7AE0, blast_x)     \
    X(0x7B60, blast_y)     \
    X(0x7BE0, blast_kind)  \
    X(0x7C60, blast_word)  \
    X(0x7CE0, hitlist_x)   \
    X(0x80E0, hitlist_y)      \
    X(0x84E0, hitlist_pivot_x) \
    X(0x88E0, hitlist_pivot_y) \
    X(0x8CE0, hitlist_half_x)  \
    X(0x90E0, hitlist_half_y)  \
    X(0x94E0, hitlist_radius)  \
    X(0x98E0, hitlist_angle)   \
    X(0x9CE0, state)       \
    X(0xAC04, seq_end)

typedef struct CoordRecord {
    unsigned int   seq_begin;
    unsigned int   flags;
    unsigned int   x[COORDBUS_SLOTS];
    unsigned int   y[COORDBUS_SLOTS];
    unsigned int   kind[COORDBUS_SLOTS];
    unsigned int   laser_angle[COORDBUS_LASER_TOTAL];
    unsigned int   laser_tail[COORDBUS_LASER_TOTAL];
    unsigned int   laser_head[COORDBUS_LASER_TOTAL];
    unsigned int   laser_width[COORDBUS_LASER_TOTAL];
    unsigned int   laser_timer[COORDBUS_LASER_TOTAL];
    unsigned int   laser_gate0[COORDBUS_LASER_TOTAL];
    unsigned int   laser_gate2[COORDBUS_LASER_TOTAL];
    unsigned int   laser_phase[COORDBUS_LASER_TOTAL];
    unsigned int   ex_timer[COORDBUS_EX_SLOTS];
    unsigned int   ex_side[COORDBUS_EX_SLOTS];
    unsigned int   ex_variant[COORDBUS_EX_SLOTS];
    unsigned int   shot_w[COORDBUS_SHOT_TOTAL];
    unsigned int   shot_h[COORDBUS_SHOT_TOTAL];
    unsigned int   blast_x[COORDBUS_BLAST_SLOTS];
    unsigned int   blast_y[COORDBUS_BLAST_SLOTS];
    unsigned int   blast_kind[COORDBUS_BLAST_SLOTS];
    unsigned int   blast_word[COORDBUS_BLAST_SLOTS];
    unsigned int   hitlist_x[COORDBUS_HITLIST_TOTAL];
    unsigned int   hitlist_y[COORDBUS_HITLIST_TOTAL];
    unsigned int   hitlist_pivot_x[COORDBUS_HITLIST_TOTAL];
    unsigned int   hitlist_pivot_y[COORDBUS_HITLIST_TOTAL];
    unsigned int   hitlist_half_x[COORDBUS_HITLIST_TOTAL];
    unsigned int   hitlist_half_y[COORDBUS_HITLIST_TOTAL];
    unsigned int   hitlist_radius[COORDBUS_HITLIST_TOTAL];
    unsigned int   hitlist_angle[COORDBUS_HITLIST_TOTAL];
    unsigned short state[COORDBUS_SLOTS];
    unsigned int   seq_end;
} CoordRecord;

#define TICKBUS_X(off, name) \
    TICKBUS_STATIC_ASSERT(offsetof(CoordRecord, name) == (off), crec_##name);
COORDBUS_RECORD_FIELDS(TICKBUS_X)
#undef TICKBUS_X
TICKBUS_STATIC_ASSERT(sizeof(CoordRecord) == COORDBUS_RECORD_SIZE, crec_size);
TICKBUS_STATIC_ASSERT((COORDBUS_CAPACITY & COORDBUS_INDEX_MASK) == 0u, ccapacity_pow2);
TICKBUS_STATIC_ASSERT(offsetof(CoordRecord, seq_end) + 4u == COORDBUS_RECORD_SIZE,
                      crec_seq_end_tail);

static __inline volatile CoordRecord *coordbus_record(volatile CoordBusHeader *h,
                                                      unsigned int idx)
{
    volatile unsigned char *base = (volatile unsigned char *)h + COORDBUS_HEADER_SIZE;
    return (volatile CoordRecord *)(base + (idx & COORDBUS_INDEX_MASK) * COORDBUS_RECORD_SIZE);
}

static __inline int coordbus_header_ok(volatile CoordBusHeader *h)
{
    if (!h)
        return 0;
    if (h->magic != COORDBUS_MAGIC)
        return 0;
    if (h->version != COORDBUS_VERSION)
        return 0;
    if (h->record_size != COORDBUS_RECORD_SIZE)
        return 0;
    if (h->capacity != COORDBUS_CAPACITY)
        return 0;
    if (h->slot_count != COORDBUS_SLOTS)
        return 0;
    return 1;
}

static __inline volatile CoordRecord *coordbus_begin_write(volatile CoordBusHeader *h,
                                                           unsigned int idx)
{
    volatile CoordRecord *r = coordbus_record(h, idx);
    r->seq_begin = TICKBUS_SEQ_BUILDING;
    tickbus_barrier();
    return r;
}

static __inline void coordbus_end_write(volatile CoordBusHeader *h,
                                        volatile CoordRecord *r, unsigned int idx)
{
    tickbus_barrier();
    r->seq_end = idx;
    tickbus_barrier();
    r->seq_begin = idx;
    tickbus_barrier();
    h->write_index = idx + 1u;
}

#endif
