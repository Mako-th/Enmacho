
#include "detour.h"


#define A_P1_PLAYER_PTR         0x004A7D94u
#define A_P2_PLAYER_PTR         0x004A7DCCu
#define A_P1_STATS_PTR          0x004A7DACu
#define A_P2_STATS_PTR          0x004A7DE4u
#define A_P1_CHARACTER          0x004A7DB0u
#define A_P2_CHARACTER          0x004A7DE8u
#define A_P1_HUMAN_CPU          0x004A7DB8u
#define A_P2_HUMAN_CPU          0x004A7DF0u
#define A_P1_CPU_LEVEL          0x004A7DBCu
#define A_P2_CPU_LEVEL          0x004A7DF4u
#define A_GUI_PTR               0x004A7E38u
#define A_ROUND_FRAMES_A        0x004A7E40u
#define A_FIELD_ID              0x004A7E84u
#define A_STORY_STAGE_INDEX     0x004A7E8Cu
#define A_COMPLETED_ROUNDS      0x004A7E90u
#define A_ROUNDS_REQUIRED       0x004A7E94u
#define A_P1_WINS               0x004A7E98u
#define A_P2_WINS               0x004A7E9Cu
#define A_GAME_MODE             0x004A7EA8u
#define A_DIFFICULTY            0x004A7EACu
#define A_PAUSE_USED            0x004A7EC0u
#define A_INPUT_MASK            0x004ACF34u
#define IM_ELEMENT_SIZE         0x8Eu
#define A_INPUT_MASK_P1         (A_INPUT_MASK - 2u * IM_ELEMENT_SIZE)
#define A_BATTLE_BGM_ID         0x004DC690u
#define A_REPLAY_MANAGER_PTR    0x004A7E64u

TICKBUS_STATIC_ASSERT(A_INPUT_MASK_P1 == 0x004ACE18u, im_p1_addr);

#define O_IM_RAW_CUR            0x2Cu
#define A_INPUT_REPLAY_P1       (A_INPUT_MASK_P1 + O_IM_RAW_CUR)
#define A_INPUT_REPLAY_P2       (A_INPUT_MASK_P1 + IM_ELEMENT_SIZE + O_IM_RAW_CUR)
TICKBUS_STATIC_ASSERT(A_INPUT_REPLAY_P1 == 0x004ACE44u, im_replay_p1_addr);
TICKBUS_STATIC_ASSERT(A_INPUT_REPLAY_P2 == 0x004ACED2u, im_replay_p2_addr);

#define A_P1_ENEMY_PTR          0x004A7DA0u
#define A_P2_ENEMY_PTR          0x004A7DD8u
#define A_P1_BULLET_PTR         0x004A7D98u
#define A_P2_BULLET_PTR         0x004A7DD0u

#define A_INTERNAL_RANK         0x004A7E44u
#define A_RANK_INTERVAL         0x004A7E54u
#define A_RANK_MAX              0x004A7E58u

#define A_LILY_COUNTER          0x004A7E5Cu

#define A_HIT_DAMAGE_BASE       0x004A7E48u
#define A_RNG_STATE             0x004ACE0Cu

#define A_P1_GAME_FLAGS         0x004A7DC4u
#define A_P2_GAME_FLAGS         0x004A7DFCu
#define A_GLOBAL_STATE          0x004A7EC4u

#define A_KUSER_SHARED_DATA     0x7FFE0000u
#define O_KSHARED_INTERRUPT_LO  0x0008u
#define O_KSHARED_TICK_COUNT_LO 0x0320u
#define O_KSHARED_TICK_MULT     0x0004u
#define A_INTERRUPT_TIME_LO     (A_KUSER_SHARED_DATA + O_KSHARED_INTERRUPT_LO)
#define A_OS_TICK_COUNT_LO      (A_KUSER_SHARED_DATA + O_KSHARED_TICK_COUNT_LO)
#define A_OS_TICK_MULT          (A_KUSER_SHARED_DATA + O_KSHARED_TICK_MULT)
TICKBUS_STATIC_ASSERT(A_INTERRUPT_TIME_LO == 0x7FFE0008u, kshared_interrupt_addr);
TICKBUS_STATIC_ASSERT(A_OS_TICK_COUNT_LO == 0x7FFE0320u, kshared_tick_addr);

#define A_EX_MANAGER_PTR        0x004A7E3Cu
#define O_EX_ACTIVE             0x000Cu
#define O_EX_ARRAY              0x001Cu
#define EX_ITEM_SIZE            0x4Cu
#define EX_ITEM_SCAN            0x100u
#define O_EX_ITEM_SIDE          0x08u
#define O_EX_ITEM_INUSE         0x0Cu
#define EX_ITEM_INUSE_FREE      0u
#define O_EX_ITEM_POS_X         0x20u
#define O_EX_ITEM_POS_Y         0x24u
#define O_EX_ITEM_TIMER_I       0x18u
#define O_EX_ITEM_UPDATE        0x40u
#define O_EX_ITEM_STATE_BLOCK   0x34u
#define O_EX_STATE_VARIANT      0x4Cu
#define EX_STATE_SPAN           (O_EX_STATE_VARIANT + 2u)
#define EX_SPAN                 0x4C80u
TICKBUS_STATIC_ASSERT(EX_ITEM_SCAN == COORDBUS_EX_SLOTS, coord_ex_slots);
TICKBUS_STATIC_ASSERT(O_EX_ITEM_UPDATE + 4u <= EX_ITEM_SIZE, ex_item_fields_fit);
TICKBUS_STATIC_ASSERT(((O_EX_ITEM_POS_X | O_EX_ITEM_POS_Y | O_EX_ITEM_TIMER_I
                        | O_EX_ITEM_UPDATE | O_EX_ITEM_SIDE | O_EX_ITEM_INUSE
                        | O_EX_ITEM_STATE_BLOCK
                        | O_EX_ARRAY | EX_ITEM_SIZE) & 3u) == 0u, ex_item_aligned);
TICKBUS_STATIC_ASSERT((O_EX_STATE_VARIANT & 3u) == 0u, ex_state_variant_aligned);
TICKBUS_STATIC_ASSERT(EX_STATE_SPAN == O_EX_STATE_VARIANT + 2u, ex_state_span_fits);
TICKBUS_STATIC_ASSERT(O_EX_ARRAY + (EX_ITEM_SCAN + 1u) * EX_ITEM_SIZE <= EX_SPAN,
                      ex_array_span);

#define O_REPLAY_FLAG           0x114u

#define O_LIFE_RAW              0xA8u
#define O_SPELL_ATTACKS         0xB0u
#define O_BOSS_ATTACKS          0xB4u
#define O_BOSS_REVERSALS        0xB8u
#define O_GAUGE                 0x30388u
#define O_CURRENT_COMBO         0x30414u
#define O_MAXIMUM_COMBO         0x30418u
#define O_SPELL_POINTS_A        0x3041Cu
#define O_SPELL_POINTS_B        0x30420u
#define O_CHARGE                0x30384u
#define O_CARD_ATK_LEVEL        0xA0u
#define O_BOSS_CARD_ATK_LEVEL   0xA4u
#define O_CPU_QUICK_TIMER       0x80u
#define O_CPU_STAND_TIMER       0x8Cu
#define O_CPU_DODGE_MODE        0x98u
#define O_COMBO_GAUGE_RAW       0x30424u
#define O_ZERO_HIT_TIMER        0x3044Cu
#define O_CPU_QUICK_TIMER_CUR   0x88u
#define O_CPU_STAND_TIMER_CUR   0x94u
#define O_COMBO_GAUGE_CUR       0x3042Cu
#define O_POS_X                 0x1B88u
#define O_POS_Y                 0x1B8Cu
#define O_WHITE_BULLET_POINTS   0x3043Cu
#define O_GHOST_POINTS          0x30440u
#define O_EX_POINTS             0x30444u
#define O_CPU_CHARGE_INSTR      0x7Cu
#define O_PLAYER_STATE          0x0000u
#define O_INVINCIBLE_TIMER      0x1B7Cu
#define O_SPEED_MULT            0x1CE4u
#define O_MOVE_DIR_ANGLE        0x1CD8u
#define O_CPU_DIR_LOCK          0x74u
#define O_CPU_PREV_DIR          0x78u
#define O_CPU_DIR_HIST          0x34u
#define O_CPU_DODGE_DIR_HIST    0x54u
#define CPU_HIST_SLOTS          8u
#define CPU_HIST_STRIDE         4u
TICKBUS_STATIC_ASSERT(O_CPU_DIR_HIST + CPU_HIST_SLOTS * CPU_HIST_STRIDE
                      == O_CPU_DODGE_DIR_HIST, cpu_dir_hist_adjacent);
TICKBUS_STATIC_ASSERT(O_CPU_DODGE_DIR_HIST + CPU_HIST_SLOTS * CPU_HIST_STRIDE
                      == O_CPU_DIR_LOCK, cpu_dodge_hist_adjacent);
TICKBUS_STATIC_ASSERT(O_CPU_DIR_LOCK + 4u == O_CPU_PREV_DIR, cpu_dir_lock_before_prev);
TICKBUS_STATIC_ASSERT(O_CPU_PREV_DIR + 4u == O_CPU_CHARGE_INSTR, cpu_prev_dir_before_instr);
#define CPU_AI_OFFSET           0x24u
TICKBUS_STATIC_ASSERT(O_CPU_CHARGE_INSTR - CPU_AI_OFFSET == 0x58u, cpu_ai_offset_by_instr);
TICKBUS_STATIC_ASSERT(O_CPU_DODGE_MODE - CPU_AI_OFFSET == 0x74u, cpu_ai_offset_by_dodge_mode);
#define O_ITEM_ARRAY            0x30454u
#define ITEM_STRIDE             0x2C4u
#define ITEM_SLOTS              4u
#define O_ITEM_KIND             0x00u
#define O_ITEM_X                0x04u
#define O_ITEM_Y                0x08u
#define O_ITEM_VALID            0x1Cu
#define O_CPU_TARGET_X          0x30F64u
#define O_CPU_TARGET_Y          0x30F68u
TICKBUS_STATIC_ASSERT(O_ITEM_ARRAY + ITEM_SLOTS * ITEM_STRIDE == O_CPU_TARGET_X,
                      item_array_ends_at_target);
TICKBUS_STATIC_ASSERT(O_CPU_TARGET_X + 4u == O_CPU_TARGET_Y, cpu_target_xy_adjacent);
#define O_SLOW_MULT_X           0x1CDCu
#define O_SLOW_MULT_Y           0x1CE0u
TICKBUS_STATIC_ASSERT(O_SLOW_MULT_Y + 4u == O_SPEED_MULT, slow_mult_before_speed_mult);
#define CONTROL_CPU             1u
#define CPU_GATE_LEVEL          60u
#define O_SHOTS                 0xC11Cu
#define SHOT_STRIDE             0x484u
#define SHOT_SLOTS              128u
#define O_S_POS_X               0x2A4u
#define O_S_POS_Y               0x2A8u
#define O_S_STATE               0x462u
#define S_STATE_FREE            0u
#define O_S_BOX_W               0x430u
#define O_S_BOX_H               0x434u
#define O_S_BEHAV               0x464u
#define O_S_SPRITE              0x46Cu
#define O_S_ENTRY               0x480u
#define O_SHOT_TABLE            0x30338u
#define PLAYER_SPAN             0x30F70u
TICKBUS_STATIC_ASSERT(O_SHOTS + SHOT_SLOTS * SHOT_STRIDE <= PLAYER_SPAN, shot_array_span);
TICKBUS_STATIC_ASSERT(O_SHOT_TABLE + 4u <= PLAYER_SPAN, shot_table_in_player);
TICKBUS_STATIC_ASSERT(O_CPU_TARGET_Y + 4u <= PLAYER_SPAN, cpu_target_in_player);
TICKBUS_STATIC_ASSERT(O_ITEM_ARRAY + (ITEM_SLOTS - 1u) * ITEM_STRIDE + O_ITEM_VALID + 4u
                      <= PLAYER_SPAN, item_array_in_player);
TICKBUS_STATIC_ASSERT(O_CPU_DIR_HIST + CPU_HIST_SLOTS * CPU_HIST_STRIDE <= PLAYER_SPAN,
                      cpu_dir_hist_in_player);
TICKBUS_STATIC_ASSERT(SHOT_SLOTS == COORDBUS_SHOT_SLOTS, coord_shot_slots);
TICKBUS_STATIC_ASSERT(((O_SHOTS | SHOT_STRIDE | O_S_POS_X | O_S_POS_Y
                        | O_S_BOX_W | O_S_BOX_H | O_S_ENTRY | O_SHOT_TABLE) & 3u) == 0u,
                      shot_fields_aligned);

#define O_HIT_LIST              0x36Cu
#define O_HIT_LIST_COUNT        0x1804u
#define O_HIT_ELEM_BASE         0x04u
#define HIT_ELEM_STRIDE         0x30u
#define HIT_ELEM_SLOTS          0x80u
TICKBUS_STATIC_ASSERT(O_HIT_ELEM_BASE + HIT_ELEM_SLOTS * HIT_ELEM_STRIDE
                      == O_HIT_LIST_COUNT, hit_list_span);
TICKBUS_STATIC_ASSERT(O_HIT_LIST + O_HIT_LIST_COUNT < PLAYER_SPAN, hit_list_in_player);
TICKBUS_STATIC_ASSERT(O_SPEED_MULT + 4u < PLAYER_SPAN, speed_mult_in_player);
TICKBUS_STATIC_ASSERT(O_MOVE_DIR_ANGLE + 4u < PLAYER_SPAN, move_dir_angle_in_player);
TICKBUS_STATIC_ASSERT(HIT_ELEM_SLOTS <= TICKBUS_HITLIST_COUNT_MASK, hit_list_count_fits);
TICKBUS_STATIC_ASSERT((TICKBUS_HITLIST_COUNT_MASK & TICKBUS_HITLIST_VALID) == 0u,
                      hit_list_count_bits);
TICKBUS_STATIC_ASSERT(HIT_ELEM_SLOTS == COORDBUS_HITLIST_SLOTS, hit_list_slots_match);

#define O_HIT_ELEM_X            0x00u
#define O_HIT_ELEM_Y            0x04u
#define O_HIT_ELEM_LASER_X      0x0Cu
#define O_HIT_ELEM_LASER_Y      0x10u
#define O_HIT_ELEM_HALF_X       0x18u
#define O_HIT_ELEM_HALF_Y       0x1Cu
#define O_HIT_ELEM_RADIUS       0x24u
#define O_HIT_ELEM_ANGLE        0x28u
#define O_HIT_ELEM_OBJ          0x2Cu
#define O_BULLET_SPRITE         0x10C0u
#define O_BULLET_COLOR          0x10C2u
#define BULLET_OBJ_SPAN         0x10C4u

#define O_ENEMY_TOTAL           0x2AC3ACu
#define O_ENEMY_FAIRY           0x2AC3B0u
#define O_ENEMY_BOSS            0x2AC3B4u
#define O_ENEMY_CHARGE          0x2AC3B8u
#define O_ENEMY_AI_PRIO         0x2AC444u
#define O_ENEMY_AI_FIRST        0x2AC448u
#define AI_TARGET_SPAN          (O_Z_CAT + 4u)
#define ENEMY_SPAN              0x2AC3C0u

#define O_ENEMIES               0x5758u
#define ENEMY_STRIDE            0x5430u
#define ENEMY_SLOTS             0x80u
TICKBUS_STATIC_ASSERT(O_ENEMIES + ENEMY_SLOTS * ENEMY_STRIDE <= ENEMY_SPAN, enemy_array_span);

#define O_Z_FLAGS               0x337Cu
#define Z_INUSE_BIT             0x0001u
#define O_Z_CAT                 0x3380u
#define Z_CAT_SHIFT             10
#define Z_CAT_MASK              3u
#define O_Z_KIND_IDX            0x3368u
#define Z_CAT_BOSS              3u
#define O_Z_SUB                 0xA1Cu
#define Z_SUB_C3                1u
#define Z_SUB_MASK_TOP          31u
#define O_Z_ECL_DEPTH           0x2D2Au
#define O_Z_HP                  0x2E48u
#define O_Z_POS_X               0x2D74u
#define O_Z_POS_Y               0x2D78u
TICKBUS_STATIC_ASSERT(AI_TARGET_SPAN <= ENEMY_STRIDE, ai_target_span_fits);
TICKBUS_STATIC_ASSERT(O_Z_POS_X + 4u == O_Z_POS_Y, z_pos_xy_adjacent);
#define O_BULLET_FAIRY          0x25E164u
#define O_BULLET_RIVAL          0x25E168u
#define BULLET_SPAN             0x25E170u

#define O_BULLETS               0x1A900u
#define BULLET_STRIDE           0x10C4u
#define BULLET_SLOTS            537u
#define O_B_POS_X               0x0D4Cu
#define O_B_POS_Y               0x0D50u
#define O_B_STATE               0x0DBEu
#define B_STATE_FREE            0u
#define B_STATE_TERM            6u
#define O_B_KIND                O_BULLET_SPRITE
TICKBUS_STATIC_ASSERT(O_BULLETS + BULLET_SLOTS * BULLET_STRIDE <= BULLET_SPAN,
                      bullet_array_span);
TICKBUS_STATIC_ASSERT(O_BULLET_COLOR == O_BULLET_SPRITE + 2u, bullet_kind_adjacent);
TICKBUS_STATIC_ASSERT(((O_B_KIND | O_BULLETS | BULLET_STRIDE) & 3u) == 0u,
                      bullet_kind_aligned);
TICKBUS_STATIC_ASSERT(BULLET_SLOTS == COORDBUS_BULLET_SLOTS, coord_bullet_slots);
TICKBUS_STATIC_ASSERT(ENEMY_SLOTS == COORDBUS_ENEMY_SLOTS, coord_enemy_slots);

#define BULLET_POOL_LO_BASE     0u
#define BULLET_POOL_LO_COUNT    175u
#define BULLET_POOL_HI_BASE     176u
#define BULLET_POOL_HI_COUNT    360u
TICKBUS_STATIC_ASSERT(O_BULLETS + 175u * BULLET_STRIDE == 0xD1EFCu, bullet_term_lo);
TICKBUS_STATIC_ASSERT(O_BULLETS + 536u * BULLET_STRIDE == 0x24C360u, bullet_term_hi);
TICKBUS_STATIC_ASSERT(O_BULLETS + BULLET_POOL_HI_BASE * BULLET_STRIDE == 0xD2FC0u,
                      bullet_pool_hi_base);
TICKBUS_STATIC_ASSERT(BULLET_POOL_LO_COUNT + 1u + BULLET_POOL_HI_COUNT + 1u == BULLET_SLOTS,
                      bullet_pool_partition);
TICKBUS_STATIC_ASSERT(BULLET_POOL_LO_BASE + BULLET_POOL_LO_COUNT + 1u == BULLET_POOL_HI_BASE,
                      bullet_pool_gap);

#define O_LASERS                0x24D424u
#define LASER_STRIDE            0x59Cu
#define LASER_SLOTS             48u
#define O_L_ALIVE               0x584u
#define L_ALIVE_FREE            0u
#define O_L_POS_X               0x548u
#define O_L_POS_Y               0x54Cu
#define O_L_ANGLE               0x554u
#define O_L_TAIL                0x558u
#define O_L_HEAD                0x55Cu
#define O_L_WIDTH               0x564u
#define O_L_TIMER_I             0x590u
#define O_L_GATE0               0x574u
#define O_L_GATE2               0x580u
#define O_L_PHASE               0x598u
#define O_L_KIND                0x594u
#define O_L_TRANSFORM_FLAGS     0x594u
#define O_L_COLOR               0x596u
TICKBUS_STATIC_ASSERT(O_LASERS + LASER_SLOTS * LASER_STRIDE == O_BULLET_FAIRY,
                      laser_array_end);
TICKBUS_STATIC_ASSERT(O_LASERS + LASER_SLOTS * LASER_STRIDE <= BULLET_SPAN,
                      laser_array_span);
TICKBUS_STATIC_ASSERT(O_L_COLOR == O_L_TRANSFORM_FLAGS + 2u, laser_kind_adjacent);
TICKBUS_STATIC_ASSERT(((O_L_KIND | O_LASERS | LASER_STRIDE) & 3u) == 0u,
                      laser_kind_aligned);
TICKBUS_STATIC_ASSERT(LASER_SLOTS == COORDBUS_LASER_SLOTS, coord_laser_slots);

#define O_STATS_LIVES           0x00u
#define O_STATS_SCORE           0x04u
#define O_STATS_SCORE_MIRROR    0x08u
#define STATS_SPAN              0x10u

#define O_RESULT_STATE          0x1095Cu
#define O_RESULT_WINNER         0x10964u
#define O_LIFE_BONUS            0x11E88u
#define O_MAX_COMBO_BONUS       0x11E8Cu
#define O_SPELL_ATTACK_BONUS    0x11E90u
#define O_BOSS_ATTACK_BONUS     0x11E94u
#define O_BOSS_REVERSAL_BONUS   0x11E98u
#define O_REMAINING_PLAYERS_BON 0x11E9Cu
#define O_CLEAR_BONUS_TOTAL     0x11EA0u
#define GUI_SPAN                0x11EB0u

volatile TickBusHeader *g_tickbus;
volatile CoordBusHeader *g_coordbus;
volatile unsigned int   g_coord_ticks;
volatile unsigned int   g_coord_skipped;
volatile unsigned int   g_hook_state = TICKBUS_STATE_IDLE;
volatile unsigned int   g_last_error = TICKBUS_ERR_NONE;
volatile unsigned int   g_tick_count;
volatile unsigned int   g_input_ticks;
volatile unsigned int   g_input_poll_ticks;
volatile unsigned int   g_input_hits;
volatile unsigned int   g_hit_ticks;
volatile unsigned int   g_speed_ticks;
volatile unsigned int   g_origin_hits;
volatile unsigned int   g_origin_dropped;
volatile unsigned int   g_enemy_kind_clash;
volatile unsigned int   g_origin_armed;
volatile unsigned int   g_blast_hits;
volatile unsigned int   g_blast_dropped;
volatile unsigned int   g_blast_armed;
volatile unsigned int   g_hitlist_armed;
volatile unsigned int   g_hitlist_clamped;
volatile unsigned int   g_laser_origin_hits;
volatile unsigned int   g_laser_origin_dropped;
volatile unsigned int   g_laser_origin_armed;

static unsigned int rd32(unsigned int addr)
{
    return *(const volatile unsigned int *)(unsigned int *)addr;
}

static unsigned int rd16z(unsigned int addr)
{
    return (unsigned int)*(const volatile unsigned short *)(unsigned short *)addr;
}

static unsigned int rd8z(unsigned int addr)
{
    return (unsigned int)*(const volatile unsigned char *)(unsigned char *)addr;
}

static int ptr_ok(unsigned int p, unsigned int span)
{
    if (p < 0x00010000u)
        return 0;
    if ((p & 3u) != 0u)
        return 0;
    if (p > 0x7FFE0000u)
        return 0;
    if ((0x7FFE0000u - p) < span)
        return 0;
    return 1;
}

typedef struct BossPick {
    unsigned int type;
    unsigned int sub;
    unsigned int depth;
    unsigned int hp;
    unsigned int n_c2;
    unsigned int n_c3;
    unsigned int n_boss;
    unsigned int sub_mask;
    unsigned int pos_x;
    unsigned int pos_y;
} BossPick;

static void pick_boss(unsigned int mgr, BossPick *out)
{
    unsigned int i, best_type = 0u, best_addr = 0u;

    out->type = 0u; out->sub = 0u; out->depth = 0u; out->hp = 0u;
    out->n_c2 = 0u; out->n_c3 = 0u; out->n_boss = 0u;
    out->pos_x = 0u; out->pos_y = 0u; out->sub_mask = 0u;
    for (i = 0u; i < ENEMY_SLOTS; i++) {
        unsigned int e = mgr + O_ENEMIES + i * ENEMY_STRIDE;
        unsigned int type, sub;
        if ((rd32(e + O_Z_FLAGS) & Z_INUSE_BIT) == 0u)
            continue;
        sub = rd16z(e + O_Z_SUB);
        out->sub_mask |= 1u << (sub < Z_SUB_MASK_TOP ? sub : Z_SUB_MASK_TOP);
        type = (rd32(e + O_Z_CAT) >> Z_CAT_SHIFT) & Z_CAT_MASK;
        if (type == 0u)
            continue;
        if (type == Z_CAT_BOSS) {
            out->n_boss++;
        } else if (sub == Z_SUB_C3) {
            out->n_c3++;
        } else {
            out->n_c2++;
        }
        if (type <= best_type)
            continue;
        best_type = type;
        best_addr = e;
    }
    if (best_type == 0u)
        return;
    out->type  = best_type;
    out->sub   = rd16z(best_addr + O_Z_SUB);
    out->depth = rd16z(best_addr + O_Z_ECL_DEPTH);
    out->hp    = rd32(best_addr + O_Z_HP);
    out->pos_x = rd32(best_addr + O_Z_POS_X);
    out->pos_y = rd32(best_addr + O_Z_POS_Y);
}

static void coord_zero_slot(volatile CoordRecord *r, unsigned int s)
{
    r->x[s] = 0u;
    r->y[s] = 0u;
    r->kind[s] = 0u;
    r->state[s] = 0u;
}

static void coord_zero_laser(volatile CoordRecord *r, unsigned int s, unsigned int l)
{
    coord_zero_slot(r, s);
    r->laser_angle[l] = 0u;
    r->laser_tail[l] = 0u;
    r->laser_head[l] = 0u;
    r->laser_width[l] = 0u;
    r->laser_timer[l] = 0u;
    r->laser_gate0[l] = 0u;
    r->laser_gate2[l] = 0u;
    r->laser_phase[l] = 0u;
}

static void coord_zero_ex(volatile CoordRecord *r, unsigned int s, unsigned int e)
{
    coord_zero_slot(r, s);
    r->ex_timer[e] = 0u;
    r->ex_side[e] = 0u;
    r->ex_variant[e] = 0u;
}

static void coord_zero_shot(volatile CoordRecord *r, unsigned int s, unsigned int k)
{
    coord_zero_slot(r, s);
    r->shot_w[k] = 0u;
    r->shot_h[k] = 0u;
}

#define O_ENEMY_SHOOTER         0x2E74u

#define ORIGIN_RET(call_addr)   ((call_addr) + 5u)

static const unsigned int k_origin_sites[COORDBUS_ORIGIN_SITE_COUNT] = {
    ORIGIN_RET(0x00408171u),
    ORIGIN_RET(0x0040A3AAu),
    ORIGIN_RET(0x0040D8FAu),
    ORIGIN_RET(0x00410A65u),
    ORIGIN_RET(0x0041439Fu),
    ORIGIN_RET(0x0044584Bu),
    ORIGIN_RET(0x0044588Bu),
    ORIGIN_RET(0x004461C2u),
    ORIGIN_RET(0x0044663Cu),
    ORIGIN_RET(0x0044667Au),
    ORIGIN_RET(0x00446AC7u),
    ORIGIN_RET(0x0044705Eu),
    ORIGIN_RET(0x0044732Bu),
    ORIGIN_RET(0x0044AEDCu),
    ORIGIN_RET(0x0044B72Bu),
    ORIGIN_RET(0x0044B86Bu),
    ORIGIN_RET(0x0044BF77u),
    ORIGIN_RET(0x0044C5DAu),
};
#define ORIGIN_SITE_ECL_UPDATE  1u
#define ORIGIN_SITE_ECL_SHOOT   2u
TICKBUS_STATIC_ASSERT(ORIGIN_SITE_ECL_UPDATE <= COORDBUS_ORIGIN_SITE_COUNT
                      && ORIGIN_SITE_ECL_SHOOT <= COORDBUS_ORIGIN_SITE_COUNT,
                      origin_ecl_ids);

static volatile unsigned short g_bullet_origin[2][BULLET_SLOTS];
static unsigned int g_side_bullet_mgr[2];
static unsigned int g_side_enemy_mgr[2];
static int g_alloc_failed;

static unsigned int origin_site_id(unsigned int ret_addr)
{
    unsigned int i;
    for (i = 0u; i < COORDBUS_ORIGIN_SITE_COUNT; i++) {
        if (k_origin_sites[i] == ret_addr)
            return i + 1u;
    }
    return 0u;
}

static unsigned int origin_enemy_slot(unsigned int side, unsigned int shooter)
{
    unsigned int base = g_side_enemy_mgr[side];
    unsigned int off, idx;

    if (!base || shooter < O_ENEMY_SHOOTER)
        return 0u;
    off = (shooter - O_ENEMY_SHOOTER) - (base + O_ENEMIES);
    idx = off / ENEMY_STRIDE;
    if (idx >= ENEMY_SLOTS || idx * ENEMY_STRIDE != off)
        return 0u;
    return idx + 1u;
}

static void bullet_origin_record(unsigned int bullet_ptr, unsigned int shooter,
                                 unsigned int frame, unsigned int pool_base,
                                 unsigned int pool_count)
{
    unsigned int mgr, off, slot, side, site, word;

    if (g_alloc_failed) {
        g_alloc_failed = 0;
        g_origin_dropped++;
        return;
    }
    mgr = rd32(frame - 4u);
    if (!mgr) {
        g_origin_dropped++;
        return;
    }
    if (mgr == g_side_bullet_mgr[0])
        side = 0u;
    else if (mgr == g_side_bullet_mgr[1])
        side = 1u;
    else {
        g_origin_dropped++;
        return;
    }
    off  = bullet_ptr - (mgr + O_BULLETS);
    slot = off / BULLET_STRIDE;
    if (slot < pool_base || slot >= pool_base + pool_count
            || slot * BULLET_STRIDE != off) {
        g_origin_dropped++;
        return;
    }
    site = origin_site_id(rd32(frame + 4u));
    word = site << COORDBUS_ORIGIN_SITE_SHIFT;
    if (site == ORIGIN_SITE_ECL_UPDATE || site == ORIGIN_SITE_ECL_SHOOT)
        word |= origin_enemy_slot(side, shooter) << COORDBUS_ORIGIN_ENEMY_SHIFT;
    g_bullet_origin[side][slot] = (unsigned short)word;
    g_origin_hits++;
}

void tickhook_bullet_lo(unsigned int bullet_ptr, unsigned int shooter, unsigned int frame)
{
    bullet_origin_record(bullet_ptr, shooter, frame,
                         BULLET_POOL_LO_BASE, BULLET_POOL_LO_COUNT);
}

void tickhook_bullet_hi(unsigned int bullet_ptr, unsigned int shooter, unsigned int frame)
{
    bullet_origin_record(bullet_ptr, shooter, frame,
                         BULLET_POOL_HI_BASE, BULLET_POOL_HI_COUNT);
}

void tickhook_alloc_fail(void)
{
    g_alloc_failed = 1;
}

static void clear_bullet_origin_side(unsigned int side)
{
    unsigned int i;
    for (i = 0u; i < BULLET_SLOTS; i++)
        g_bullet_origin[side][i] = 0u;
}

void tickhook_reset_bullet_origin(void)
{
    unsigned int side;
    for (side = 0u; side < 2u; side++) {
        clear_bullet_origin_side(side);
        g_side_bullet_mgr[side] = 0u;
        g_side_enemy_mgr[side] = 0u;
    }
    g_alloc_failed = 0;
    g_origin_hits = 0u;
    g_origin_dropped = 0u;
}

#define O_Z_BLAST_POS           0x2DD4u
TICKBUS_STATIC_ASSERT(O_Z_BLAST_POS + 8u <= ENEMY_STRIDE, blast_pos_in_enemy);

static volatile unsigned int g_blast_x[COORDBUS_BLAST_SLOTS];
static volatile unsigned int g_blast_y[COORDBUS_BLAST_SLOTS];
static volatile unsigned int g_blast_kind[COORDBUS_BLAST_SLOTS];
static volatile unsigned int g_blast_word[COORDBUS_BLAST_SLOTS];
static unsigned int g_blast_count;

static volatile unsigned int g_hl_x[COORDBUS_HITLIST_TOTAL];
static volatile unsigned int g_hl_y[COORDBUS_HITLIST_TOTAL];
static volatile unsigned int g_hl_pivot_x[COORDBUS_HITLIST_TOTAL];
static volatile unsigned int g_hl_pivot_y[COORDBUS_HITLIST_TOTAL];
static volatile unsigned int g_hl_half_x[COORDBUS_HITLIST_TOTAL];
static volatile unsigned int g_hl_half_y[COORDBUS_HITLIST_TOTAL];
static volatile unsigned int g_hl_radius[COORDBUS_HITLIST_TOTAL];
static volatile unsigned int g_hl_angle[COORDBUS_HITLIST_TOTAL];
static unsigned int g_hl_count[2];

static void coord_write_hitlist(volatile CoordRecord *r, unsigned int base,
                                unsigned int count)
{
    unsigned int i, k;
    for (i = 0u; i < COORDBUS_HITLIST_SLOTS; i++) {
        k = base + i;
        if (i < count) {
            r->hitlist_x[k]       = g_hl_x[k];
            r->hitlist_y[k]       = g_hl_y[k];
            r->hitlist_pivot_x[k] = g_hl_pivot_x[k];
            r->hitlist_pivot_y[k] = g_hl_pivot_y[k];
            r->hitlist_half_x[k]  = g_hl_half_x[k];
            r->hitlist_half_y[k]  = g_hl_half_y[k];
            r->hitlist_radius[k]  = g_hl_radius[k];
            r->hitlist_angle[k]   = g_hl_angle[k];
        } else {
            r->hitlist_x[k]       = 0u;
            r->hitlist_y[k]       = 0u;
            r->hitlist_pivot_x[k] = 0u;
            r->hitlist_pivot_y[k] = 0u;
            r->hitlist_half_x[k]  = 0u;
            r->hitlist_half_y[k]  = 0u;
            r->hitlist_radius[k]  = 0u;
            r->hitlist_angle[k]   = 0u;
        }
    }
}

static unsigned int coord_gather_hitlist(unsigned int player_ptr, unsigned int side,
                                         unsigned int count)
{
    unsigned int i, elem = player_ptr + O_HIT_LIST + O_HIT_ELEM_BASE;
    unsigned int base = side * COORDBUS_HITLIST_SLOTS;
    if (count > COORDBUS_HITLIST_SLOTS) {
        count = COORDBUS_HITLIST_SLOTS;
        g_hitlist_clamped++;
    }
    for (i = 0u; i < count; i++, elem += HIT_ELEM_STRIDE) {
        g_hl_x[base + i]       = rd32(elem + O_HIT_ELEM_X);
        g_hl_y[base + i]       = rd32(elem + O_HIT_ELEM_Y);
        g_hl_pivot_x[base + i] = rd32(elem + O_HIT_ELEM_LASER_X);
        g_hl_pivot_y[base + i] = rd32(elem + O_HIT_ELEM_LASER_Y);
        g_hl_half_x[base + i]  = rd32(elem + O_HIT_ELEM_HALF_X);
        g_hl_half_y[base + i]  = rd32(elem + O_HIT_ELEM_HALF_Y);
        g_hl_radius[base + i]  = rd32(elem + O_HIT_ELEM_RADIUS);
        g_hl_angle[base + i]   = rd32(elem + O_HIT_ELEM_ANGLE);
    }
    return count;
}

void tickhook_reset_hitlist(void)
{
    unsigned int i;
    for (i = 0u; i < COORDBUS_HITLIST_TOTAL; i++) {
        g_hl_x[i] = 0u;
        g_hl_y[i] = 0u;
        g_hl_pivot_x[i] = 0u;
        g_hl_pivot_y[i] = 0u;
        g_hl_half_x[i] = 0u;
        g_hl_half_y[i] = 0u;
        g_hl_radius[i] = 0u;
        g_hl_angle[i] = 0u;
    }
    g_hl_count[0] = 0u;
    g_hl_count[1] = 0u;
    g_hitlist_clamped = 0u;
}

static unsigned int blast_enemy_slot(unsigned int side, unsigned int enemy)
{
    unsigned int base = g_side_enemy_mgr[side];
    unsigned int off, idx;

    if (!base)
        return 0u;
    base += O_ENEMIES;
    if (enemy < base)
        return 0u;
    off = enemy - base;
    idx = off / ENEMY_STRIDE;
    if (idx >= ENEMY_SLOTS || idx * ENEMY_STRIDE != off)
        return 0u;
    return idx + 1u;
}

void tickhook_enemy_blast(unsigned int enemy_ptr, unsigned int pos_ptr)
{
    unsigned int side, slot = 0u, word;

    if (g_blast_count >= COORDBUS_BLAST_SLOTS) {
        g_blast_dropped++;
        return;
    }
    if (!ptr_ok(pos_ptr, 8u) || !ptr_ok(enemy_ptr, O_Z_CAT + 4u)) {
        g_blast_dropped++;
        return;
    }
    for (side = 0u; side < 2u; side++) {
        slot = blast_enemy_slot(side, enemy_ptr);
        if (slot)
            break;
    }
    if (!slot) {
        g_blast_dropped++;
        return;
    }
    word = COORDBUS_BLAST_PRESENT
         | ((side & COORDBUS_BLAST_SIDE_MASK) << COORDBUS_BLAST_SIDE_SHIFT)
         | ((slot & COORDBUS_BLAST_ENEMY_MASK) << COORDBUS_BLAST_ENEMY_SHIFT)
         | ((rd8z(enemy_ptr + O_Z_KIND_IDX) & COORDBUS_BLAST_IDX_MASK)
            << COORDBUS_BLAST_IDX_SHIFT);
    if (pos_ptr == enemy_ptr + O_Z_BLAST_POS)
        word |= COORDBUS_BLAST_POS_OK;

    g_blast_x[g_blast_count] = rd32(pos_ptr);
    g_blast_y[g_blast_count] = rd32(pos_ptr + 4u);
    g_blast_kind[g_blast_count] = rd32(enemy_ptr + O_Z_CAT);
    g_blast_word[g_blast_count] = word;
    g_blast_count++;
    g_blast_hits++;
}

static void coord_write_blasts(volatile CoordRecord *r, unsigned int count)
{
    unsigned int i;
    for (i = 0u; i < COORDBUS_BLAST_SLOTS; i++) {
        if (i < count) {
            r->blast_x[i]    = g_blast_x[i];
            r->blast_y[i]    = g_blast_y[i];
            r->blast_kind[i] = g_blast_kind[i];
            r->blast_word[i] = g_blast_word[i];
        } else {
            r->blast_x[i]    = 0u;
            r->blast_y[i]    = 0u;
            r->blast_kind[i] = 0u;
            r->blast_word[i] = 0u;
        }
    }
}

void tickhook_reset_enemy_blast(void)
{
    unsigned int i;
    for (i = 0u; i < COORDBUS_BLAST_SLOTS; i++) {
        g_blast_x[i] = 0u;
        g_blast_y[i] = 0u;
        g_blast_kind[i] = 0u;
        g_blast_word[i] = 0u;
    }
    g_blast_count = 0u;
    g_blast_hits = 0u;
    g_blast_dropped = 0u;
}

#define O_ENEMY_LASER_CFG   0x30C4u
TICKBUS_STATIC_ASSERT(O_ENEMY_LASER_CFG < ENEMY_STRIDE, laser_cfg_in_enemy);

static volatile unsigned short g_laser_origin[2][LASER_SLOTS];

static void clear_laser_origin_side(unsigned int side)
{
    unsigned int i;
    for (i = 0u; i < LASER_SLOTS; i++)
        g_laser_origin[side][i] = 0u;
}

void tickhook_reset_laser_origin(void)
{
    unsigned int side;
    for (side = 0u; side < 2u; side++)
        clear_laser_origin_side(side);
    g_laser_origin_hits = 0u;
    g_laser_origin_dropped = 0u;
}

void tickhook_laser_born(unsigned int laser_ptr, unsigned int shooter_cfg)
{
    unsigned int side, lbase, loff, lidx = 0u;
    unsigned int word = 0u;

    for (side = 0u; side < 2u; side++) {
        unsigned int mgr = g_side_bullet_mgr[side];
        if (!mgr)
            continue;
        lbase = mgr + O_LASERS;
        if (laser_ptr < lbase)
            continue;
        loff = laser_ptr - lbase;
        lidx = loff / LASER_STRIDE;
        if (lidx < LASER_SLOTS && lidx * LASER_STRIDE == loff)
            break;
    }
    if (side >= 2u) {
        g_laser_origin_dropped++;
        return;
    }

    {
        unsigned int ebase = g_side_enemy_mgr[side];
        if (ebase && shooter_cfg >= O_ENEMY_LASER_CFG) {
            unsigned int eoff = (shooter_cfg - O_ENEMY_LASER_CFG) - (ebase + O_ENEMIES);
            unsigned int eidx = eoff / ENEMY_STRIDE;
            if (eidx < ENEMY_SLOTS && eidx * ENEMY_STRIDE == eoff) {
                unsigned int enemy_ptr = ebase + O_ENEMIES + eidx * ENEMY_STRIDE;
                unsigned int sub = rd16z(enemy_ptr + O_Z_SUB);
                unsigned int sub_word = (sub >= COORDBUS_LASER_ORIGIN_SUB_CAP)
                                       ? COORDBUS_LASER_ORIGIN_SUB_MASK : sub + 1u;
                word = (sub_word << COORDBUS_LASER_ORIGIN_SUB_SHIFT)
                     | ((eidx + 1u) << COORDBUS_LASER_ORIGIN_ENEMY_SHIFT);
            }
        }
    }
    g_laser_origin[side][lidx] = (unsigned short)word;
    g_laser_origin_hits++;
}

static void coord_gather_bullets(unsigned int mgr, volatile CoordRecord *r,
                                 unsigned int base, unsigned int side)
{
    unsigned int i, p = mgr + O_BULLETS;
    for (i = 0u; i < BULLET_SLOTS; i++, p += BULLET_STRIDE) {
        unsigned int st = rd16z(p + O_B_STATE);
        if (st == B_STATE_FREE || st == B_STATE_TERM) {
            coord_zero_slot(r, base + i);
            g_bullet_origin[side][i] = 0u;
            continue;
        }
        r->x[base + i] = rd32(p + O_B_POS_X);
        r->y[base + i] = rd32(p + O_B_POS_Y);
        r->kind[base + i] = rd32(p + O_B_KIND);
        r->state[base + i] = (unsigned short)(st | g_bullet_origin[side][i]);
    }
}

static unsigned int coord_gather_enemies(unsigned int mgr, volatile CoordRecord *r,
                                         unsigned int base)
{
    unsigned int clash = 0u;
    unsigned int i, p = mgr + O_ENEMIES;
    for (i = 0u; i < ENEMY_SLOTS; i++, p += ENEMY_STRIDE) {
        unsigned int st = rd16z(p + O_Z_FLAGS);
        if ((st & Z_INUSE_BIT) == 0u) {
            coord_zero_slot(r, base + i);
            continue;
        }
        r->x[base + i] = rd32(p + O_Z_POS_X);
        r->y[base + i] = rd32(p + O_Z_POS_Y);
        {
            unsigned int cat = rd32(p + O_Z_CAT);
            if (cat & COORDBUS_ENEMY_KIND_CLASH_MASK) {
                clash++;
                r->kind[base + i] = cat;
            } else {
                r->kind[base + i] = cat
                    | ((rd8z(p + O_Z_KIND_IDX) & COORDBUS_ENEMY_KIND_IDX_MASK)
                       << COORDBUS_ENEMY_KIND_IDX_SHIFT);
            }
        }
        r->state[base + i] = (unsigned short)st;
    }
    return clash;
}

static void coord_gather_shots(unsigned int player, volatile CoordRecord *r,
                               unsigned int base, unsigned int kbase)
{
    unsigned int i, p = player + O_SHOTS;
    unsigned int table = rd32(player + O_SHOT_TABLE);
    for (i = 0u; i < SHOT_SLOTS; i++, p += SHOT_STRIDE) {
        unsigned int st = rd16z(p + O_S_STATE);
        unsigned int entry, off, kind;
        if (st == S_STATE_FREE) {
            coord_zero_shot(r, base + i, kbase + i);
            continue;
        }
        r->x[base + i] = rd32(p + O_S_POS_X);
        r->y[base + i] = rd32(p + O_S_POS_Y);
        kind = rd16z(p + O_S_SPRITE) & COORDBUS_SHOT_KIND_SPRITE_MASK;
        kind |= (rd16z(p + O_S_BEHAV) & COORDBUS_SHOT_KIND_BEHAV_MASK)
                << COORDBUS_SHOT_KIND_BEHAV_SHIFT;
        entry = rd32(p + O_S_ENTRY);
        if (table && entry > table) {
            off = (entry - table) >> 2;
            if (off <= COORDBUS_SHOT_KIND_ENTRY_MASK)
                kind |= off << COORDBUS_SHOT_KIND_ENTRY_SHIFT;
        }
        r->kind[base + i] = kind;
        r->shot_w[kbase + i] = rd32(p + O_S_BOX_W);
        r->shot_h[kbase + i] = rd32(p + O_S_BOX_H);
        r->state[base + i] = (unsigned short)st;
    }
}

static void coord_gather_lasers(unsigned int mgr, volatile CoordRecord *r,
                                unsigned int base, unsigned int lbase,
                                unsigned int side)
{
    unsigned int i, p = mgr + O_LASERS;
    for (i = 0u; i < LASER_SLOTS; i++, p += LASER_STRIDE) {
        unsigned int st = rd32(p + O_L_ALIVE);
        if (st == L_ALIVE_FREE) {
            coord_zero_laser(r, base + i, lbase + i);
            g_laser_origin[side][i] = 0u;
            continue;
        }
        r->x[base + i] = rd32(p + O_L_POS_X);
        r->y[base + i] = rd32(p + O_L_POS_Y);
        r->kind[base + i] = rd32(p + O_L_KIND);
        r->laser_angle[lbase + i] = rd32(p + O_L_ANGLE);
        r->laser_tail[lbase + i]  = rd32(p + O_L_TAIL);
        r->laser_head[lbase + i]  = rd32(p + O_L_HEAD);
        r->laser_width[lbase + i] = rd32(p + O_L_WIDTH);
        r->laser_timer[lbase + i] = rd32(p + O_L_TIMER_I);
        r->laser_gate0[lbase + i] = rd32(p + O_L_GATE0);
        r->laser_gate2[lbase + i] = rd32(p + O_L_GATE2);
        r->laser_phase[lbase + i] = rd16z(p + O_L_PHASE);
        r->state[base + i] = (unsigned short)(st | g_laser_origin[side][i]);
    }
}

static void coord_gather_ex(unsigned int mgr, volatile CoordRecord *r,
                            unsigned int base, unsigned int ebase)
{
    unsigned int i, sb, p = mgr + O_EX_ARRAY;
    for (i = 0u; i < EX_ITEM_SCAN; i++, p += EX_ITEM_SIZE) {
        unsigned int st = rd32(p + O_EX_ITEM_INUSE);
        if (st == EX_ITEM_INUSE_FREE) {
            coord_zero_ex(r, base + i, ebase + i);
            continue;
        }
        r->x[base + i] = rd32(p + O_EX_ITEM_POS_X);
        r->y[base + i] = rd32(p + O_EX_ITEM_POS_Y);
        r->kind[base + i] = rd32(p + O_EX_ITEM_UPDATE);
        r->ex_timer[ebase + i] = rd32(p + O_EX_ITEM_TIMER_I);
        r->ex_side[ebase + i] = rd32(p + O_EX_ITEM_SIDE);
        sb = rd32(p + O_EX_ITEM_STATE_BLOCK);
        r->ex_variant[ebase + i] = ptr_ok(sb, EX_STATE_SPAN)
                                 ? rd16z(sb + O_EX_STATE_VARIANT) : 0u;
        r->state[base + i] = (unsigned short)st;
    }
}

static void coord_clear(volatile CoordRecord *r, unsigned int base, unsigned int n)
{
    unsigned int i;
    for (i = 0u; i < n; i++)
        coord_zero_slot(r, base + i);
}

static void coord_clear_lasers(volatile CoordRecord *r, unsigned int base,
                               unsigned int lbase, unsigned int n)
{
    unsigned int i;
    for (i = 0u; i < n; i++)
        coord_zero_laser(r, base + i, lbase + i);
}

static void coord_clear_ex(volatile CoordRecord *r, unsigned int base,
                           unsigned int ebase, unsigned int n)
{
    unsigned int i;
    for (i = 0u; i < n; i++)
        coord_zero_ex(r, base + i, ebase + i);
}

static void coord_clear_shots(volatile CoordRecord *r, unsigned int base,
                              unsigned int kbase, unsigned int n)
{
    unsigned int i;
    for (i = 0u; i < n; i++)
        coord_zero_shot(r, base + i, kbase + i);
}

static unsigned int coord_write(unsigned int idx, unsigned int b1, unsigned int b2,
                                unsigned int e1, unsigned int e2, unsigned int ex,
                                unsigned int p1p, unsigned int p2p,
                                unsigned int blast_n,
                                unsigned int hl_n1, unsigned int hl_n2)
{
    volatile CoordBusHeader *h = g_coordbus;
    volatile CoordRecord *r;
    unsigned int flags = 0u;
    unsigned int clash = 0u;
    unsigned int state = (TICKBUS_COORD_DECIM_NONE << TICKBUS_COORD_STATE_DECIM_SHIFT);

    if (!h)
        return 0u;
    state |= TICKBUS_COORD_STATE_PRESENT;
    if (h->enable == 0u) {
        g_coord_skipped++;
        h->skipped_ticks = g_coord_skipped;
        return state;
    }
    state |= TICKBUS_COORD_STATE_ENABLE;

    r = coordbus_begin_write(h, idx);
    if (ptr_ok(b1, BULLET_SPAN)) {
        coord_gather_bullets(b1, r, COORDBUS_BASE_P1_BULLET, 0u);
        coord_gather_lasers(b1, r, COORDBUS_BASE_P1_LASER, 0u, 0u);
        flags |= COORDBUS_FLAG_P1_BULLETS | COORDBUS_FLAG_P1_LASERS;
    } else {
        coord_clear(r, COORDBUS_BASE_P1_BULLET, BULLET_SLOTS);
        coord_clear_lasers(r, COORDBUS_BASE_P1_LASER, 0u, LASER_SLOTS);
        clear_bullet_origin_side(0u);
        clear_laser_origin_side(0u);
    }
    if (ptr_ok(e1, ENEMY_SPAN)) {
        clash += coord_gather_enemies(e1, r, COORDBUS_BASE_P1_ENEMY);
        flags |= COORDBUS_FLAG_P1_ENEMIES;
    } else {
        coord_clear(r, COORDBUS_BASE_P1_ENEMY, ENEMY_SLOTS);
    }
    if (ptr_ok(b2, BULLET_SPAN)) {
        coord_gather_bullets(b2, r, COORDBUS_BASE_P2_BULLET, 1u);
        coord_gather_lasers(b2, r, COORDBUS_BASE_P2_LASER, LASER_SLOTS, 1u);
        flags |= COORDBUS_FLAG_P2_BULLETS | COORDBUS_FLAG_P2_LASERS;
    } else {
        coord_clear(r, COORDBUS_BASE_P2_BULLET, BULLET_SLOTS);
        coord_clear_lasers(r, COORDBUS_BASE_P2_LASER, LASER_SLOTS, LASER_SLOTS);
        clear_bullet_origin_side(1u);
        clear_laser_origin_side(1u);
    }
    if (ptr_ok(e2, ENEMY_SPAN)) {
        clash += coord_gather_enemies(e2, r, COORDBUS_BASE_P2_ENEMY);
        flags |= COORDBUS_FLAG_P2_ENEMIES;
    } else {
        coord_clear(r, COORDBUS_BASE_P2_ENEMY, ENEMY_SLOTS);
    }
    if (ptr_ok(p1p, PLAYER_SPAN)) {
        coord_gather_shots(p1p, r, COORDBUS_BASE_P1_SHOT, 0u);
        flags |= COORDBUS_FLAG_P1_SHOTS;
    } else {
        coord_clear_shots(r, COORDBUS_BASE_P1_SHOT, 0u, SHOT_SLOTS);
    }
    if (ptr_ok(p2p, PLAYER_SPAN)) {
        coord_gather_shots(p2p, r, COORDBUS_BASE_P2_SHOT, SHOT_SLOTS);
        flags |= COORDBUS_FLAG_P2_SHOTS;
    } else {
        coord_clear_shots(r, COORDBUS_BASE_P2_SHOT, SHOT_SLOTS, SHOT_SLOTS);
    }
    if (ptr_ok(ex, EX_SPAN)) {
        coord_gather_ex(ex, r, COORDBUS_BASE_EX, 0u);
        flags |= COORDBUS_FLAG_EX;
    } else {
        coord_clear_ex(r, COORDBUS_BASE_EX, 0u, EX_ITEM_SCAN);
    }
    coord_write_blasts(r, blast_n);
    if (g_blast_armed)
        flags |= COORDBUS_FLAG_ENEMY_BLAST;
    coord_write_hitlist(r, 0u, hl_n1);
    coord_write_hitlist(r, COORDBUS_HITLIST_SLOTS, hl_n2);
    if (g_hitlist_armed)
        flags |= COORDBUS_FLAG_HITLIST;
    if (g_origin_armed)
        flags |= COORDBUS_FLAG_BULLET_ORIGIN;
    if (g_laser_origin_armed)
        flags |= COORDBUS_FLAG_LASER_ORIGIN;
    flags |= COORDBUS_FLAG_ENEMY_KIND_IDX;
    if (clash) {
        flags |= COORDBUS_FLAG_ENEMY_KIND_CLASH;
        g_enemy_kind_clash += clash;
        h->enemy_kind_clash = g_enemy_kind_clash;
    }
    r->flags = flags;
    coordbus_end_write(h, r, idx);

    g_coord_ticks++;
    h->coord_ticks = g_coord_ticks;
    return state | TICKBUS_COORD_STATE_WRITTEN;
}

static volatile unsigned char g_ex_prev[EX_ITEM_SCAN];
static unsigned int  g_ex_base;

static void clear_ex_prev(void)
{
    unsigned int i;
    for (i = 0u; i < EX_ITEM_SCAN; i++)
        g_ex_prev[i] = 0u;
}

#define IM_FIELD_COUNT 5
static const unsigned int g_im_offset[IM_FIELD_COUNT] = {
    0x00u, 0x04u, 0x06u, 0x2Cu, 0x32u
};
#define IM_FIELDS_MASK    0x1Fu
#define IM_FIELDS_DEFAULT 0x01u
TICKBUS_STATIC_ASSERT(IM_FIELDS_MASK == (1u << IM_FIELD_COUNT) - 1u, im_fields_mask);
TICKBUS_STATIC_ASSERT((IM_FIELDS_MASK & TICKBUS_CMD_FIELD_MIRROR_P1) == 0u, im_mirror_bit);
TICKBUS_STATIC_ASSERT((IM_FIELDS_MASK & TICKBUS_CMD_FIELD_EXCLUSIVE) == 0u, im_exclusive_bit);
TICKBUS_STATIC_ASSERT(
    (TICKBUS_CMD_FIELD_MIRROR_P1 & TICKBUS_CMD_FIELD_EXCLUSIVE) == 0u, im_mirror_vs_excl);

#define LOCK_LEASE_MS  30000u

static unsigned int g_cmd_seq_seen;
static unsigned int g_cmd_mask;
static unsigned int g_cmd_left;
static unsigned int g_cmd_fields;
static unsigned int g_cmd_tick_seen;
static unsigned int g_cmd_latch_ms;

static void wr16(unsigned int addr, unsigned int value)
{
    *(volatile unsigned short *)(unsigned short *)addr = (unsigned short)value;
}

unsigned int capture_input_mask_addr(void)    { return A_INPUT_MASK; }
unsigned int capture_input_mask_p1_addr(void) { return A_INPUT_MASK_P1; }

static unsigned int os_millis(void)
{
    unsigned int lo   = rd32(A_OS_TICK_COUNT_LO);
    unsigned int mult = rd32(A_OS_TICK_MULT);
    return (unsigned int)(((unsigned long long)lo * (unsigned long long)mult) >> 24);
}

static void apply_input_command(volatile TickBusHeader *h)
{
    unsigned int seq = h->cmd_seq;

    unsigned int i;
    int exclusive = 0;

    if (seq != g_cmd_seq_seen) {
        unsigned int fields = h->cmd_fields;
        if (!(fields & IM_FIELDS_MASK))
            fields |= IM_FIELDS_DEFAULT;
        g_cmd_seq_seen = seq;
        g_cmd_mask     = h->cmd_mask;
        g_cmd_left     = h->cmd_ticks;
        g_cmd_fields   = fields;
        g_cmd_latch_ms = os_millis();
        h->cmd_ack     = seq;
    }

    if (g_cmd_left == 0u)
        return;

    if (g_cmd_fields & TICKBUS_CMD_FIELD_EXCLUSIVE)
        exclusive = (os_millis() - g_cmd_latch_ms) <= LOCK_LEASE_MS;

    if (g_cmd_tick_seen != g_tick_count) {
        g_cmd_tick_seen = g_tick_count;
        g_cmd_left--;
    }

    for (i = 0; i < IM_FIELD_COUNT; i++) {
        unsigned int addr;
        if (!(g_cmd_fields & (1u << i)))
            continue;
        addr = A_INPUT_MASK + g_im_offset[i];
        wr16(addr, exclusive ? g_cmd_mask : (rd16z(addr) | g_cmd_mask));
    }

    if (g_cmd_fields & TICKBUS_CMD_FIELD_MIRROR_P1)
        wr16(A_INPUT_MASK_P1, rd16z(A_INPUT_MASK_P1) | g_cmd_mask);
}

static int input_site_common(unsigned int im_ptr)
{
    volatile TickBusHeader *h = g_tickbus;

    g_input_hits++;
    if (h) {
        h->input_hits = g_input_hits;
        h->input_last_ecx = im_ptr;
    }

    if (im_ptr != A_INPUT_MASK)
        return 0;
    if (!h)
        return 1;

    apply_input_command(h);
    return 1;
}

void tickhook_input(unsigned int im_ptr)
{
    volatile TickBusHeader *h;
    if (!input_site_common(im_ptr))
        return;
    g_input_ticks++;
    h = g_tickbus;
    if (h)
        h->input_ticks = g_input_ticks;
}

void tickhook_input_poll(unsigned int im_ptr)
{
    volatile TickBusHeader *h;
    if (!input_site_common(im_ptr))
        return;
    g_input_poll_ticks++;
    h = g_tickbus;
    if (h)
        h->input_poll_ticks = g_input_poll_ticks;
}

static unsigned int g_hit_kind[2];
static unsigned int g_hit_obj[2];
static unsigned int g_hit_obj_ptr[2];
static unsigned int g_hit_x[2];
static unsigned int g_hit_y[2];
static unsigned int g_hit_laser_x[2];
static unsigned int g_hit_laser_y[2];
static unsigned int g_hit_laser_angle[2];
static unsigned int g_hit_elem_radius[2];

static int hit_side_of(unsigned int elem)
{
    unsigned int p;
    int side;

    for (side = 0; side < 2; side++) {
        p = rd32(side ? A_P2_PLAYER_PTR : A_P1_PLAYER_PTR);
        if (!ptr_ok(p, PLAYER_SPAN))
            continue;
        if (elem >= p + O_HIT_LIST + O_HIT_ELEM_BASE
                && elem < p + O_HIT_LIST + O_HIT_LIST_COUNT)
            return side;
    }
    return -1;
}

void tickhook_hit(unsigned int elem_ptr)
{
    volatile TickBusHeader *h;
    unsigned int obj, kind, objword = 0u, angle, radius;
    int side;

    g_hit_ticks++;
    h = g_tickbus;
    if (h)
        h->hit_ticks = g_hit_ticks;

    side = hit_side_of(elem_ptr);
    if (side < 0)
        return;

    obj = rd32(elem_ptr + O_HIT_ELEM_OBJ);
    if (obj != 0u) {
        kind = TICKBUS_HIT_TYPE_BULLET << TICKBUS_HIT_TYPE_SHIFT;
        if (ptr_ok(obj, BULLET_OBJ_SPAN)) {
            kind |= TICKBUS_HIT_OBJ_VALID;
            objword = rd16z(obj + O_BULLET_SPRITE)
                    | (rd16z(obj + O_BULLET_COLOR) << TICKBUS_HIT_OBJ_COLOR_SHIFT);
        }
    } else if ((radius = rd32(elem_ptr + O_HIT_ELEM_RADIUS)) != 0u) {
        kind = TICKBUS_HIT_TYPE_EX_CIRCLE << TICKBUS_HIT_TYPE_SHIFT;
        g_hit_elem_radius[side] = radius;
    } else if ((angle = rd32(elem_ptr + O_HIT_ELEM_ANGLE)) != 0u) {
        kind = TICKBUS_HIT_TYPE_LASER << TICKBUS_HIT_TYPE_SHIFT;
        g_hit_laser_x[side] = rd32(elem_ptr + O_HIT_ELEM_LASER_X);
        g_hit_laser_y[side] = rd32(elem_ptr + O_HIT_ELEM_LASER_Y);
        g_hit_laser_angle[side] = angle;
    } else {
        kind = TICKBUS_HIT_TYPE_OTHER << TICKBUS_HIT_TYPE_SHIFT;
    }

    g_hit_kind[side] = kind | TICKBUS_HIT_VALID;
    g_hit_obj[side]  = objword;
    g_hit_obj_ptr[side] = obj;
    g_hit_x[side]    = rd32(elem_ptr + O_HIT_ELEM_X);
    g_hit_y[side]    = rd32(elem_ptr + O_HIT_ELEM_Y);
}

static unsigned int g_speed_mult[2];
static unsigned int g_hit_list_count[2];


void tickhook_speed_reset(unsigned int player_ptr)
{
    volatile TickBusHeader *h;
    unsigned int count;
    int side;

    g_speed_ticks++;
    h = g_tickbus;
    if (h)
        h->speed_ticks = g_speed_ticks;

    if (player_ptr == rd32(A_P1_PLAYER_PTR))
        side = 0;
    else if (player_ptr == rd32(A_P2_PLAYER_PTR))
        side = 1;
    else
        return;

    g_speed_mult[side] = rd32(player_ptr + O_SPEED_MULT);
    count = rd32(player_ptr + O_HIT_LIST + O_HIT_LIST_COUNT);
    g_hit_list_count[side] = TICKBUS_HITLIST_VALID
                           | (count & TICKBUS_HITLIST_COUNT_MASK);

    g_hl_count[side] = coord_gather_hitlist(player_ptr, (unsigned int)side, count);
}

void tickhook_capture(void)
{
    volatile TickBusHeader *h;
    volatile TickRecord *r;
    unsigned int idx, tid, flags;
    unsigned int p1p, p2p, s1, s2, gui, rm;
    unsigned int e1, e2, b1, b2, ex;
    unsigned int i;
    unsigned int hit_kind1, hit_obj1, hit_ptr1, hit_x1, hit_y1;
    unsigned int hit_lx1, hit_ly1, hit_lx2, hit_ly2;
    unsigned int hit_la1, hit_la2;
    unsigned int hit_er1, hit_er2;
    unsigned int hit_kind2, hit_obj2, hit_ptr2, hit_x2, hit_y2;
    unsigned int speed1, speed2, hlist1, hlist2;
    unsigned int blast_n;
    unsigned int hl_n1, hl_n2;
    unsigned int ctrl1, ctrl2, clev1, clev2, ai1, ai2;
    unsigned int emgr1, emgr2;
    unsigned int prio1, prio2, first1, first2;
    BossPick boss1, boss2;

    g_tick_count++;

    hit_kind1 = g_hit_kind[0]; hit_obj1 = g_hit_obj[0]; hit_ptr1 = g_hit_obj_ptr[0];
    hit_lx1 = g_hit_laser_x[0]; hit_ly1 = g_hit_laser_y[0];
    hit_lx2 = g_hit_laser_x[1]; hit_ly2 = g_hit_laser_y[1];
    hit_la1 = g_hit_laser_angle[0]; hit_la2 = g_hit_laser_angle[1];
    hit_x1 = g_hit_x[0]; hit_y1 = g_hit_y[0];
    hit_kind2 = g_hit_kind[1]; hit_obj2 = g_hit_obj[1]; hit_ptr2 = g_hit_obj_ptr[1];
    hit_x2 = g_hit_x[1]; hit_y2 = g_hit_y[1];
    g_hit_kind[0] = 0u; g_hit_obj[0] = 0u; g_hit_obj_ptr[0] = 0u;
    g_hit_laser_x[0] = 0u; g_hit_laser_y[0] = 0u;
    g_hit_laser_x[1] = 0u; g_hit_laser_y[1] = 0u;
    g_hit_laser_angle[0] = 0u; g_hit_laser_angle[1] = 0u;
    hit_er1 = g_hit_elem_radius[0]; hit_er2 = g_hit_elem_radius[1];
    g_hit_elem_radius[0] = 0u; g_hit_elem_radius[1] = 0u;
    g_hit_x[0] = 0u; g_hit_y[0] = 0u;
    g_hit_kind[1] = 0u; g_hit_obj[1] = 0u; g_hit_obj_ptr[1] = 0u;
    g_hit_x[1] = 0u; g_hit_y[1] = 0u;
    speed1 = g_speed_mult[0]; hlist1 = g_hit_list_count[0];
    speed2 = g_speed_mult[1]; hlist2 = g_hit_list_count[1];
    g_speed_mult[0] = 0u; g_hit_list_count[0] = 0u;
    g_speed_mult[1] = 0u; g_hit_list_count[1] = 0u;
    hl_n1 = g_hl_count[0]; hl_n2 = g_hl_count[1];
    g_hl_count[0] = 0u;    g_hl_count[1] = 0u;
    blast_n = g_blast_count;
    g_blast_count = 0u;

    h = g_tickbus;
    if (!h)
        return;

    if (g_hook_state != TICKBUS_STATE_RUNNING) {
        h->armed_ticks = g_tick_count;
        return;
    }

    tid = tickbus_current_tid();
    if (h->writer_tid == 0u)
        h->writer_tid = tid;
    else if (h->writer_tid != tid)
        h->last_error = TICKBUS_ERR_MULTITHREAD;

    r = tickbus_begin_write(h, &idx);

    p1p = rd32(A_P1_PLAYER_PTR);
    p2p = rd32(A_P2_PLAYER_PTR);
    s1  = rd32(A_P1_STATS_PTR);
    s2  = rd32(A_P2_STATS_PTR);
    gui = rd32(A_GUI_PTR);
    rm  = rd32(A_REPLAY_MANAGER_PTR);

    flags = 0u;
    if (ptr_ok(p1p, PLAYER_SPAN) && ptr_ok(p2p, PLAYER_SPAN))
        flags |= TICKBUS_FLAG_PLAYERS_VALID;
    if (ptr_ok(gui, GUI_SPAN))
        flags |= TICKBUS_FLAG_GUI_VALID;
    if (ptr_ok(s1, STATS_SPAN) && ptr_ok(s2, STATS_SPAN))
        flags |= TICKBUS_FLAG_STATS_VALID;
    if (ptr_ok(rm, O_REPLAY_FLAG + 4u))
        flags |= TICKBUS_FLAG_REPLAYMGR_VALID;

    r->flags = flags;

    r->mode             = rd32(A_GAME_MODE);
    r->difficulty       = rd32(A_DIFFICULTY);
    r->stage_index      = rd32(A_STORY_STAGE_INDEX);
    r->field_id         = rd32(A_FIELD_ID);
    r->battle_bgm_id    = rd32(A_BATTLE_BGM_ID);
    r->round_frames     = rd32(A_ROUND_FRAMES_A);
    r->completed_rounds = rd32(A_COMPLETED_ROUNDS);
    r->rounds_required  = rd32(A_ROUNDS_REQUIRED);
    r->pause_used       = rd32(A_PAUSE_USED);
    r->p1_wins          = rd32(A_P1_WINS);
    r->p2_wins          = rd32(A_P2_WINS);
    r->input_mask       = rd16z(A_INPUT_MASK);
    r->p1_character     = rd32(A_P1_CHARACTER);
    r->p2_character     = rd32(A_P2_CHARACTER);
    ctrl1 = rd32(A_P1_HUMAN_CPU);
    ctrl2 = rd32(A_P2_HUMAN_CPU);
    clev1 = rd32(A_P1_CPU_LEVEL);
    clev2 = rd32(A_P2_CPU_LEVEL);
    r->p1_control       = ctrl1;
    r->p2_control       = ctrl2;
    r->p1_cpu_level     = clev1;
    r->p2_cpu_level     = clev2;
    r->p1_game_flags    = rd32(A_P1_GAME_FLAGS);
    r->p2_game_flags    = rd32(A_P2_GAME_FLAGS);
    r->global_state     = rd32(A_GLOBAL_STATE);

    if (flags & TICKBUS_FLAG_GUI_VALID) {
        r->result_state           = rd32(gui + O_RESULT_STATE);
        r->result_winner          = rd32(gui + O_RESULT_WINNER);
        r->clear_life_bonus       = rd32(gui + O_LIFE_BONUS);
        r->clear_max_combo_bonus  = rd32(gui + O_MAX_COMBO_BONUS);
        r->clear_spell_bonus      = rd32(gui + O_SPELL_ATTACK_BONUS);
        r->clear_boss_bonus       = rd32(gui + O_BOSS_ATTACK_BONUS);
        r->clear_reversal_bonus   = rd32(gui + O_BOSS_REVERSAL_BONUS);
        r->clear_lives_bonus      = rd32(gui + O_REMAINING_PLAYERS_BON);
        r->clear_total            = rd32(gui + O_CLEAR_BONUS_TOTAL);
    } else {
        r->result_state           = 0u;
        r->result_winner          = 0u;
        r->clear_life_bonus       = 0u;
        r->clear_max_combo_bonus  = 0u;
        r->clear_spell_bonus      = 0u;
        r->clear_boss_bonus       = 0u;
        r->clear_reversal_bonus   = 0u;
        r->clear_lives_bonus      = 0u;
        r->clear_total            = 0u;
    }

    r->replay_flag = (flags & TICKBUS_FLAG_REPLAYMGR_VALID)
                   ? rd32(rm + O_REPLAY_FLAG) : 0u;

    if (flags & TICKBUS_FLAG_PLAYERS_VALID) {
        r->p1_life_raw       = rd32(p1p + O_LIFE_RAW);
        r->p2_life_raw       = rd32(p2p + O_LIFE_RAW);
        r->p1_spell_attacks  = rd32(p1p + O_SPELL_ATTACKS);
        r->p2_spell_attacks  = rd32(p2p + O_SPELL_ATTACKS);
        r->p1_boss_attacks   = rd32(p1p + O_BOSS_ATTACKS);
        r->p2_boss_attacks   = rd32(p2p + O_BOSS_ATTACKS);
        r->p1_boss_reversals = rd32(p1p + O_BOSS_REVERSALS);
        r->p2_boss_reversals = rd32(p2p + O_BOSS_REVERSALS);
        r->p1_current_combo  = rd32(p1p + O_CURRENT_COMBO);
        r->p2_current_combo  = rd32(p2p + O_CURRENT_COMBO);
        r->p1_max_combo      = rd32(p1p + O_MAXIMUM_COMBO);
        r->p2_max_combo      = rd32(p2p + O_MAXIMUM_COMBO);
        r->p1_spell_points   = rd32(p1p + O_SPELL_POINTS_A);
        r->p2_spell_points   = rd32(p2p + O_SPELL_POINTS_A);
        r->p1_spell_points_mirror = rd32(p1p + O_SPELL_POINTS_B);
        r->p2_spell_points_mirror = rd32(p2p + O_SPELL_POINTS_B);
        r->p1_gauge          = rd32(p1p + O_GAUGE);
        r->p2_gauge          = rd32(p2p + O_GAUGE);
        r->p1_move_dir_angle = rd32(p1p + O_MOVE_DIR_ANGLE);
        r->p2_move_dir_angle = rd32(p2p + O_MOVE_DIR_ANGLE);
    } else {
        r->p1_life_raw       = 0u;
        r->p2_life_raw       = 0u;
        r->p1_spell_attacks  = 0u;
        r->p2_spell_attacks  = 0u;
        r->p1_boss_attacks   = 0u;
        r->p2_boss_attacks   = 0u;
        r->p1_boss_reversals = 0u;
        r->p2_boss_reversals = 0u;
        r->p1_current_combo  = 0u;
        r->p2_current_combo  = 0u;
        r->p1_max_combo      = 0u;
        r->p2_max_combo      = 0u;
        r->p1_spell_points   = 0u;
        r->p2_spell_points   = 0u;
        r->p1_spell_points_mirror = 0u;
        r->p2_spell_points_mirror = 0u;
        r->p1_gauge          = 0u;
        r->p2_gauge          = 0u;
        r->p1_move_dir_angle = 0u;
        r->p2_move_dir_angle = 0u;
    }

    if (flags & TICKBUS_FLAG_STATS_VALID) {
        r->p1_lives         = rd32(s1 + O_STATS_LIVES);
        r->p2_lives         = rd32(s2 + O_STATS_LIVES);
        r->p1_score_raw     = rd32(s1 + O_STATS_SCORE);
        r->p2_score_raw     = rd32(s2 + O_STATS_SCORE);
        r->p1_score_mirror  = rd32(s1 + O_STATS_SCORE_MIRROR);
        r->p2_score_mirror  = rd32(s2 + O_STATS_SCORE_MIRROR);
    } else {
        r->p1_lives         = 0u;
        r->p2_lives         = 0u;
        r->p1_score_raw     = 0u;
        r->p2_score_raw     = 0u;
        r->p1_score_mirror  = 0u;
        r->p2_score_mirror  = 0u;
    }

    if (flags & TICKBUS_FLAG_PLAYERS_VALID) {
        r->p1_cpu_dodge_mode  = rd32(p1p + O_CPU_DODGE_MODE);
        r->p2_cpu_dodge_mode  = rd32(p2p + O_CPU_DODGE_MODE);
        r->p1_cpu_quick_timer = rd32(p1p + O_CPU_QUICK_TIMER);
        r->p2_cpu_quick_timer = rd32(p2p + O_CPU_QUICK_TIMER);
        r->p1_cpu_stand_timer = rd32(p1p + O_CPU_STAND_TIMER);
        r->p2_cpu_stand_timer = rd32(p2p + O_CPU_STAND_TIMER);
        r->p1_zero_hit_timer  = rd32(p1p + O_ZERO_HIT_TIMER);
        r->p2_zero_hit_timer  = rd32(p2p + O_ZERO_HIT_TIMER);
        r->p1_combo_gauge_raw = rd32(p1p + O_COMBO_GAUGE_RAW);
        r->p2_combo_gauge_raw = rd32(p2p + O_COMBO_GAUGE_RAW);
    } else {
        r->p1_cpu_dodge_mode  = 0u;
        r->p2_cpu_dodge_mode  = 0u;
        r->p1_cpu_quick_timer = 0u;
        r->p2_cpu_quick_timer = 0u;
        r->p1_cpu_stand_timer = 0u;
        r->p2_cpu_stand_timer = 0u;
        r->p1_zero_hit_timer  = 0u;
        r->p2_zero_hit_timer  = 0u;
        r->p1_combo_gauge_raw = 0u;
        r->p2_combo_gauge_raw = 0u;
    }

    e1 = rd32(A_P1_ENEMY_PTR);
    e2 = rd32(A_P2_ENEMY_PTR);
    if (ptr_ok(e1, ENEMY_SPAN) && ptr_ok(e2, ENEMY_SPAN)) {
        r->p1_enemy_total  = rd32(e1 + O_ENEMY_TOTAL);
        r->p2_enemy_total  = rd32(e2 + O_ENEMY_TOTAL);
        r->p1_enemy_fairy  = rd32(e1 + O_ENEMY_FAIRY);
        r->p2_enemy_fairy  = rd32(e2 + O_ENEMY_FAIRY);
        r->p1_enemy_boss   = rd32(e1 + O_ENEMY_BOSS);
        r->p2_enemy_boss   = rd32(e2 + O_ENEMY_BOSS);
        r->p1_enemy_charge = rd32(e1 + O_ENEMY_CHARGE);
        r->p2_enemy_charge = rd32(e2 + O_ENEMY_CHARGE);
        pick_boss(e1, &boss1);
        pick_boss(e2, &boss2);
        g_side_enemy_mgr[0] = e1;
        g_side_enemy_mgr[1] = e2;
    } else {
        g_side_enemy_mgr[0] = 0u;
        g_side_enemy_mgr[1] = 0u;
        r->p1_enemy_total  = 0u;
        r->p2_enemy_total  = 0u;
        r->p1_enemy_fairy  = 0u;
        r->p2_enemy_fairy  = 0u;
        r->p1_enemy_boss   = 0u;
        r->p2_enemy_boss   = 0u;
        r->p1_enemy_charge = 0u;
        r->p2_enemy_charge = 0u;
        boss1.type = 0u; boss1.sub = 0u; boss1.depth = 0u; boss1.hp = 0u;
        boss1.n_c2 = 0u; boss1.n_c3 = 0u; boss1.n_boss = 0u;
        boss1.pos_x = 0u; boss1.pos_y = 0u;
        boss2.type = 0u; boss2.sub = 0u; boss2.depth = 0u; boss2.hp = 0u;
        boss2.n_c2 = 0u; boss2.n_c3 = 0u; boss2.n_boss = 0u;
        boss2.pos_x = 0u; boss2.pos_y = 0u;
    }
    r->p1_boss_type  = boss1.type;
    r->p2_boss_type  = boss2.type;
    r->p1_boss_sub   = boss1.sub;
    r->p2_boss_sub   = boss2.sub;
    r->p1_boss_depth = boss1.depth;
    r->p2_boss_depth = boss2.depth;
    r->p1_boss_hp    = boss1.hp;
    r->p2_boss_hp    = boss2.hp;
    r->p1_enemy_sub_mask = boss1.sub_mask;
    r->p2_enemy_sub_mask = boss2.sub_mask;
    r->p1_enemy_class_counts = (boss1.n_c2 & 0xFFu)
                             | ((boss1.n_c3 & 0xFFu) << 8)
                             | ((boss1.n_boss & 0xFFu) << 16);
    r->p2_enemy_class_counts = (boss2.n_c2 & 0xFFu)
                             | ((boss2.n_c3 & 0xFFu) << 8)
                             | ((boss2.n_boss & 0xFFu) << 16);
    r->p1_boss_pos_x = boss1.pos_x;
    r->p2_boss_pos_x = boss2.pos_x;
    r->p1_boss_pos_y = boss1.pos_y;
    r->p2_boss_pos_y = boss2.pos_y;

    r->internal_rank = rd32(A_INTERNAL_RANK);
    r->rank_interval = rd32(A_RANK_INTERVAL);
    r->rank_max      = rd32(A_RANK_MAX);
    if (flags & TICKBUS_FLAG_PLAYERS_VALID) {
        r->p1_charge = rd32(p1p + O_CHARGE);
        r->p2_charge = rd32(p2p + O_CHARGE);
        r->p1_card_attack_level = rd32(p1p + O_CARD_ATK_LEVEL);
        r->p2_card_attack_level = rd32(p2p + O_CARD_ATK_LEVEL);
        r->p1_boss_card_attack_level = rd32(p1p + O_BOSS_CARD_ATK_LEVEL);
        r->p2_boss_card_attack_level = rd32(p2p + O_BOSS_CARD_ATK_LEVEL);
        r->p1_cpu_quick_timer_cur = rd32(p1p + O_CPU_QUICK_TIMER_CUR);
        r->p2_cpu_quick_timer_cur = rd32(p2p + O_CPU_QUICK_TIMER_CUR);
        r->p1_cpu_stand_timer_cur = rd32(p1p + O_CPU_STAND_TIMER_CUR);
        r->p2_cpu_stand_timer_cur = rd32(p2p + O_CPU_STAND_TIMER_CUR);
        r->p1_combo_gauge_cur     = rd32(p1p + O_COMBO_GAUGE_CUR);
        r->p2_combo_gauge_cur     = rd32(p2p + O_COMBO_GAUGE_CUR);
        r->p1_pos_x = rd32(p1p + O_POS_X);
        r->p2_pos_x = rd32(p2p + O_POS_X);
        r->p1_pos_y = rd32(p1p + O_POS_Y);
        r->p2_pos_y = rd32(p2p + O_POS_Y);
        r->p1_white_bullet_points = rd32(p1p + O_WHITE_BULLET_POINTS);
        r->p2_white_bullet_points = rd32(p2p + O_WHITE_BULLET_POINTS);
        r->p1_ghost_points        = rd32(p1p + O_GHOST_POINTS);
        r->p2_ghost_points        = rd32(p2p + O_GHOST_POINTS);
        r->p1_ex_points           = rd32(p1p + O_EX_POINTS);
        r->p2_ex_points           = rd32(p2p + O_EX_POINTS);
        r->p1_cpu_charge_instruction = rd32(p1p + O_CPU_CHARGE_INSTR);
        r->p2_cpu_charge_instruction = rd32(p2p + O_CPU_CHARGE_INSTR);
        r->p1_player_state = rd32(p1p + O_PLAYER_STATE);
        r->p2_player_state = rd32(p2p + O_PLAYER_STATE);
        r->p1_invincible_timer = rd32(p1p + O_INVINCIBLE_TIMER);
        r->p2_invincible_timer = rd32(p2p + O_INVINCIBLE_TIMER);
    } else {
        r->p1_charge = 0u;
        r->p2_charge = 0u;
        r->p1_card_attack_level = 0u;
        r->p2_card_attack_level = 0u;
        r->p1_boss_card_attack_level = 0u;
        r->p2_boss_card_attack_level = 0u;
        r->p1_cpu_quick_timer_cur = 0u;
        r->p2_cpu_quick_timer_cur = 0u;
        r->p1_cpu_stand_timer_cur = 0u;
        r->p2_cpu_stand_timer_cur = 0u;
        r->p1_combo_gauge_cur     = 0u;
        r->p2_combo_gauge_cur     = 0u;
        r->p1_pos_x = 0u;
        r->p2_pos_x = 0u;
        r->p1_pos_y = 0u;
        r->p2_pos_y = 0u;
        r->p1_white_bullet_points = 0u;
        r->p2_white_bullet_points = 0u;
        r->p1_ghost_points        = 0u;
        r->p2_ghost_points        = 0u;
        r->p1_ex_points           = 0u;
        r->p2_ex_points           = 0u;
        r->p1_cpu_charge_instruction = 0u;
        r->p2_cpu_charge_instruction = 0u;
        r->p1_player_state = 0u;
        r->p2_player_state = 0u;
        r->p1_invincible_timer = 0u;
        r->p2_invincible_timer = 0u;
    }

    b1 = rd32(A_P1_BULLET_PTR);
    b2 = rd32(A_P2_BULLET_PTR);
    r->p1_bullet_mgr = b1;
    r->p2_bullet_mgr = b2;
    if (ptr_ok(b1, BULLET_SPAN) && ptr_ok(b2, BULLET_SPAN)) {
        r->p1_bullet_fairy = rd32(b1 + O_BULLET_FAIRY);
        r->p2_bullet_fairy = rd32(b2 + O_BULLET_FAIRY);
        r->p1_bullet_rival = rd32(b1 + O_BULLET_RIVAL);
        r->p2_bullet_rival = rd32(b2 + O_BULLET_RIVAL);
        g_side_bullet_mgr[0] = b1;
        g_side_bullet_mgr[1] = b2;
    } else {
        g_side_bullet_mgr[0] = 0u;
        g_side_bullet_mgr[1] = 0u;
        r->p1_bullet_fairy = 0u;
        r->p2_bullet_fairy = 0u;
        r->p1_bullet_rival = 0u;
        r->p2_bullet_rival = 0u;
    }

    ex = rd32(A_EX_MANAGER_PTR);
    if (ptr_ok(ex, EX_SPAN)) {
        unsigned int trig1 = 0u, trig2 = 0u;
        if (ex != g_ex_base) {
            g_ex_base = ex;
            clear_ex_prev();
        }
        for (i = 0u; i < EX_ITEM_SCAN; i++) {
            unsigned int item = ex + O_EX_ARRAY + i * EX_ITEM_SIZE;
            unsigned int inuse = (rd32(item + O_EX_ITEM_INUSE) != 0u) ? 1u : 0u;
            if (inuse && !g_ex_prev[i]) {
                if (rd32(item + O_EX_ITEM_SIDE) == 0u)
                    trig1++;
                else
                    trig2++;
            }
            g_ex_prev[i] = (unsigned char)inuse;
        }
        r->p1_ex_active = rd32(ex + O_EX_ACTIVE);
        r->p2_ex_active = rd32(ex + O_EX_ACTIVE + 4u);
        r->p1_ex_triggered = trig1;
        r->p2_ex_triggered = trig2;
    } else {
        if (g_ex_base != 0u) {
            g_ex_base = 0u;
            clear_ex_prev();
        }
        r->p1_ex_active = 0u;
        r->p2_ex_active = 0u;
        r->p1_ex_triggered = 0u;
        r->p2_ex_triggered = 0u;
    }

    r->lily_counter = rd32(A_LILY_COUNTER);
    r->p1_input_replay = rd16z(A_INPUT_REPLAY_P1);
    r->p2_input_replay = rd16z(A_INPUT_REPLAY_P2);

    r->rng_state = rd16z(A_RNG_STATE);

    r->hit_damage_base = rd32(A_HIT_DAMAGE_BASE);

    r->p1_hit_kind = hit_kind1;
    r->p1_hit_obj  = hit_obj1;
    r->p1_hit_laser_x = hit_lx1;
    r->p1_hit_laser_y = hit_ly1;
    r->p2_hit_laser_x = hit_lx2;
    r->p2_hit_laser_y = hit_ly2;
    r->p1_hit_laser_angle = hit_la1;
    r->p2_hit_laser_angle = hit_la2;
    r->p1_hit_elem_radius = hit_er1;
    r->p2_hit_elem_radius = hit_er2;
    r->p1_hit_x    = hit_x1;
    r->p1_hit_y    = hit_y1;
    r->p2_hit_kind = hit_kind2;
    r->p2_hit_obj  = hit_obj2;
    r->p2_hit_x    = hit_x2;
    r->p2_hit_y    = hit_y2;
    r->p1_hit_obj_ptr = hit_ptr1;
    r->p2_hit_obj_ptr = hit_ptr2;
    r->p1_speed_mult = speed1;
    r->p2_speed_mult = speed2;
    r->p1_hit_list_count = hlist1;
    r->p2_hit_list_count = hlist2;

    ai1 = ((flags & TICKBUS_FLAG_PLAYERS_VALID)
           && ctrl1 == CONTROL_CPU && clev1 == CPU_GATE_LEVEL) ? p1p : 0u;
    ai2 = ((flags & TICKBUS_FLAG_PLAYERS_VALID)
           && ctrl2 == CONTROL_CPU && clev2 == CPU_GATE_LEVEL) ? p2p : 0u;

    r->p1_cpu_dir_lock = ai1 ? rd32(ai1 + O_CPU_DIR_LOCK) : 0u;
    r->p2_cpu_dir_lock = ai2 ? rd32(ai2 + O_CPU_DIR_LOCK) : 0u;
    r->p1_cpu_prev_dir = ai1 ? rd32(ai1 + O_CPU_PREV_DIR) : 0u;
    r->p2_cpu_prev_dir = ai2 ? rd32(ai2 + O_CPU_PREV_DIR) : 0u;

    r->p1_cpu_dir_hist0 = ai1 ? rd32(ai1 + O_CPU_DIR_HIST + 0u * CPU_HIST_STRIDE) : 0u;
    r->p1_cpu_dir_hist1 = ai1 ? rd32(ai1 + O_CPU_DIR_HIST + 1u * CPU_HIST_STRIDE) : 0u;
    r->p1_cpu_dir_hist2 = ai1 ? rd32(ai1 + O_CPU_DIR_HIST + 2u * CPU_HIST_STRIDE) : 0u;
    r->p1_cpu_dir_hist3 = ai1 ? rd32(ai1 + O_CPU_DIR_HIST + 3u * CPU_HIST_STRIDE) : 0u;
    r->p1_cpu_dir_hist4 = ai1 ? rd32(ai1 + O_CPU_DIR_HIST + 4u * CPU_HIST_STRIDE) : 0u;
    r->p1_cpu_dir_hist5 = ai1 ? rd32(ai1 + O_CPU_DIR_HIST + 5u * CPU_HIST_STRIDE) : 0u;
    r->p1_cpu_dir_hist6 = ai1 ? rd32(ai1 + O_CPU_DIR_HIST + 6u * CPU_HIST_STRIDE) : 0u;
    r->p1_cpu_dir_hist7 = ai1 ? rd32(ai1 + O_CPU_DIR_HIST + 7u * CPU_HIST_STRIDE) : 0u;
    r->p2_cpu_dir_hist0 = ai2 ? rd32(ai2 + O_CPU_DIR_HIST + 0u * CPU_HIST_STRIDE) : 0u;
    r->p2_cpu_dir_hist1 = ai2 ? rd32(ai2 + O_CPU_DIR_HIST + 1u * CPU_HIST_STRIDE) : 0u;
    r->p2_cpu_dir_hist2 = ai2 ? rd32(ai2 + O_CPU_DIR_HIST + 2u * CPU_HIST_STRIDE) : 0u;
    r->p2_cpu_dir_hist3 = ai2 ? rd32(ai2 + O_CPU_DIR_HIST + 3u * CPU_HIST_STRIDE) : 0u;
    r->p2_cpu_dir_hist4 = ai2 ? rd32(ai2 + O_CPU_DIR_HIST + 4u * CPU_HIST_STRIDE) : 0u;
    r->p2_cpu_dir_hist5 = ai2 ? rd32(ai2 + O_CPU_DIR_HIST + 5u * CPU_HIST_STRIDE) : 0u;
    r->p2_cpu_dir_hist6 = ai2 ? rd32(ai2 + O_CPU_DIR_HIST + 6u * CPU_HIST_STRIDE) : 0u;
    r->p2_cpu_dir_hist7 = ai2 ? rd32(ai2 + O_CPU_DIR_HIST + 7u * CPU_HIST_STRIDE) : 0u;

    r->p1_cpu_dodge_dir_hist0 = ai1 ? rd32(ai1 + O_CPU_DODGE_DIR_HIST + 0u * CPU_HIST_STRIDE) : 0u;
    r->p1_cpu_dodge_dir_hist1 = ai1 ? rd32(ai1 + O_CPU_DODGE_DIR_HIST + 1u * CPU_HIST_STRIDE) : 0u;
    r->p1_cpu_dodge_dir_hist2 = ai1 ? rd32(ai1 + O_CPU_DODGE_DIR_HIST + 2u * CPU_HIST_STRIDE) : 0u;
    r->p1_cpu_dodge_dir_hist3 = ai1 ? rd32(ai1 + O_CPU_DODGE_DIR_HIST + 3u * CPU_HIST_STRIDE) : 0u;
    r->p1_cpu_dodge_dir_hist4 = ai1 ? rd32(ai1 + O_CPU_DODGE_DIR_HIST + 4u * CPU_HIST_STRIDE) : 0u;
    r->p1_cpu_dodge_dir_hist5 = ai1 ? rd32(ai1 + O_CPU_DODGE_DIR_HIST + 5u * CPU_HIST_STRIDE) : 0u;
    r->p1_cpu_dodge_dir_hist6 = ai1 ? rd32(ai1 + O_CPU_DODGE_DIR_HIST + 6u * CPU_HIST_STRIDE) : 0u;
    r->p1_cpu_dodge_dir_hist7 = ai1 ? rd32(ai1 + O_CPU_DODGE_DIR_HIST + 7u * CPU_HIST_STRIDE) : 0u;
    r->p2_cpu_dodge_dir_hist0 = ai2 ? rd32(ai2 + O_CPU_DODGE_DIR_HIST + 0u * CPU_HIST_STRIDE) : 0u;
    r->p2_cpu_dodge_dir_hist1 = ai2 ? rd32(ai2 + O_CPU_DODGE_DIR_HIST + 1u * CPU_HIST_STRIDE) : 0u;
    r->p2_cpu_dodge_dir_hist2 = ai2 ? rd32(ai2 + O_CPU_DODGE_DIR_HIST + 2u * CPU_HIST_STRIDE) : 0u;
    r->p2_cpu_dodge_dir_hist3 = ai2 ? rd32(ai2 + O_CPU_DODGE_DIR_HIST + 3u * CPU_HIST_STRIDE) : 0u;
    r->p2_cpu_dodge_dir_hist4 = ai2 ? rd32(ai2 + O_CPU_DODGE_DIR_HIST + 4u * CPU_HIST_STRIDE) : 0u;
    r->p2_cpu_dodge_dir_hist5 = ai2 ? rd32(ai2 + O_CPU_DODGE_DIR_HIST + 5u * CPU_HIST_STRIDE) : 0u;
    r->p2_cpu_dodge_dir_hist6 = ai2 ? rd32(ai2 + O_CPU_DODGE_DIR_HIST + 6u * CPU_HIST_STRIDE) : 0u;
    r->p2_cpu_dodge_dir_hist7 = ai2 ? rd32(ai2 + O_CPU_DODGE_DIR_HIST + 7u * CPU_HIST_STRIDE) : 0u;

    r->p1_item0_kind = ai1 ? rd32(ai1 + O_ITEM_ARRAY + 0u * ITEM_STRIDE + O_ITEM_KIND) : 0u;
    r->p1_item0_x = ai1 ? rd32(ai1 + O_ITEM_ARRAY + 0u * ITEM_STRIDE + O_ITEM_X) : 0u;
    r->p1_item0_y = ai1 ? rd32(ai1 + O_ITEM_ARRAY + 0u * ITEM_STRIDE + O_ITEM_Y) : 0u;
    r->p1_item0_valid = ai1 ? rd32(ai1 + O_ITEM_ARRAY + 0u * ITEM_STRIDE + O_ITEM_VALID) : 0u;
    r->p1_item1_kind = ai1 ? rd32(ai1 + O_ITEM_ARRAY + 1u * ITEM_STRIDE + O_ITEM_KIND) : 0u;
    r->p1_item1_x = ai1 ? rd32(ai1 + O_ITEM_ARRAY + 1u * ITEM_STRIDE + O_ITEM_X) : 0u;
    r->p1_item1_y = ai1 ? rd32(ai1 + O_ITEM_ARRAY + 1u * ITEM_STRIDE + O_ITEM_Y) : 0u;
    r->p1_item1_valid = ai1 ? rd32(ai1 + O_ITEM_ARRAY + 1u * ITEM_STRIDE + O_ITEM_VALID) : 0u;
    r->p1_item2_kind = ai1 ? rd32(ai1 + O_ITEM_ARRAY + 2u * ITEM_STRIDE + O_ITEM_KIND) : 0u;
    r->p1_item2_x = ai1 ? rd32(ai1 + O_ITEM_ARRAY + 2u * ITEM_STRIDE + O_ITEM_X) : 0u;
    r->p1_item2_y = ai1 ? rd32(ai1 + O_ITEM_ARRAY + 2u * ITEM_STRIDE + O_ITEM_Y) : 0u;
    r->p1_item2_valid = ai1 ? rd32(ai1 + O_ITEM_ARRAY + 2u * ITEM_STRIDE + O_ITEM_VALID) : 0u;
    r->p1_item3_kind = ai1 ? rd32(ai1 + O_ITEM_ARRAY + 3u * ITEM_STRIDE + O_ITEM_KIND) : 0u;
    r->p1_item3_x = ai1 ? rd32(ai1 + O_ITEM_ARRAY + 3u * ITEM_STRIDE + O_ITEM_X) : 0u;
    r->p1_item3_y = ai1 ? rd32(ai1 + O_ITEM_ARRAY + 3u * ITEM_STRIDE + O_ITEM_Y) : 0u;
    r->p1_item3_valid = ai1 ? rd32(ai1 + O_ITEM_ARRAY + 3u * ITEM_STRIDE + O_ITEM_VALID) : 0u;
    r->p2_item0_kind = ai2 ? rd32(ai2 + O_ITEM_ARRAY + 0u * ITEM_STRIDE + O_ITEM_KIND) : 0u;
    r->p2_item0_x = ai2 ? rd32(ai2 + O_ITEM_ARRAY + 0u * ITEM_STRIDE + O_ITEM_X) : 0u;
    r->p2_item0_y = ai2 ? rd32(ai2 + O_ITEM_ARRAY + 0u * ITEM_STRIDE + O_ITEM_Y) : 0u;
    r->p2_item0_valid = ai2 ? rd32(ai2 + O_ITEM_ARRAY + 0u * ITEM_STRIDE + O_ITEM_VALID) : 0u;
    r->p2_item1_kind = ai2 ? rd32(ai2 + O_ITEM_ARRAY + 1u * ITEM_STRIDE + O_ITEM_KIND) : 0u;
    r->p2_item1_x = ai2 ? rd32(ai2 + O_ITEM_ARRAY + 1u * ITEM_STRIDE + O_ITEM_X) : 0u;
    r->p2_item1_y = ai2 ? rd32(ai2 + O_ITEM_ARRAY + 1u * ITEM_STRIDE + O_ITEM_Y) : 0u;
    r->p2_item1_valid = ai2 ? rd32(ai2 + O_ITEM_ARRAY + 1u * ITEM_STRIDE + O_ITEM_VALID) : 0u;
    r->p2_item2_kind = ai2 ? rd32(ai2 + O_ITEM_ARRAY + 2u * ITEM_STRIDE + O_ITEM_KIND) : 0u;
    r->p2_item2_x = ai2 ? rd32(ai2 + O_ITEM_ARRAY + 2u * ITEM_STRIDE + O_ITEM_X) : 0u;
    r->p2_item2_y = ai2 ? rd32(ai2 + O_ITEM_ARRAY + 2u * ITEM_STRIDE + O_ITEM_Y) : 0u;
    r->p2_item2_valid = ai2 ? rd32(ai2 + O_ITEM_ARRAY + 2u * ITEM_STRIDE + O_ITEM_VALID) : 0u;
    r->p2_item3_kind = ai2 ? rd32(ai2 + O_ITEM_ARRAY + 3u * ITEM_STRIDE + O_ITEM_KIND) : 0u;
    r->p2_item3_x = ai2 ? rd32(ai2 + O_ITEM_ARRAY + 3u * ITEM_STRIDE + O_ITEM_X) : 0u;
    r->p2_item3_y = ai2 ? rd32(ai2 + O_ITEM_ARRAY + 3u * ITEM_STRIDE + O_ITEM_Y) : 0u;
    r->p2_item3_valid = ai2 ? rd32(ai2 + O_ITEM_ARRAY + 3u * ITEM_STRIDE + O_ITEM_VALID) : 0u;

    r->p1_cpu_target_x = ai1 ? rd32(ai1 + O_CPU_TARGET_X) : 0u;
    r->p2_cpu_target_x = ai2 ? rd32(ai2 + O_CPU_TARGET_X) : 0u;
    r->p1_cpu_target_y = ai1 ? rd32(ai1 + O_CPU_TARGET_Y) : 0u;
    r->p2_cpu_target_y = ai2 ? rd32(ai2 + O_CPU_TARGET_Y) : 0u;

    emgr1 = ai1 ? g_side_enemy_mgr[0] : 0u;
    emgr2 = ai2 ? g_side_enemy_mgr[1] : 0u;
    prio1 = emgr1 ? rd32(emgr1 + O_ENEMY_AI_PRIO) : 0u;
    prio2 = emgr2 ? rd32(emgr2 + O_ENEMY_AI_PRIO) : 0u;
    first1 = emgr1 ? rd32(emgr1 + O_ENEMY_AI_FIRST) : 0u;
    first2 = emgr2 ? rd32(emgr2 + O_ENEMY_AI_FIRST) : 0u;
    if (!ptr_ok(prio1, AI_TARGET_SPAN)) prio1 = 0u;
    if (!ptr_ok(prio2, AI_TARGET_SPAN)) prio2 = 0u;
    if (!ptr_ok(first1, AI_TARGET_SPAN)) first1 = 0u;
    if (!ptr_ok(first2, AI_TARGET_SPAN)) first2 = 0u;
    r->p1_enemy_prio_x = prio1 ? rd32(prio1 + O_Z_POS_X) : 0u;
    r->p1_enemy_prio_y = prio1 ? rd32(prio1 + O_Z_POS_Y) : 0u;
    r->p1_enemy_prio_flags = prio1 ? rd32(prio1 + O_Z_CAT) : 0u;
    r->p2_enemy_prio_x = prio2 ? rd32(prio2 + O_Z_POS_X) : 0u;
    r->p2_enemy_prio_y = prio2 ? rd32(prio2 + O_Z_POS_Y) : 0u;
    r->p2_enemy_prio_flags = prio2 ? rd32(prio2 + O_Z_CAT) : 0u;
    r->p1_enemy_first_x = first1 ? rd32(first1 + O_Z_POS_X) : 0u;
    r->p1_enemy_first_y = first1 ? rd32(first1 + O_Z_POS_Y) : 0u;
    r->p1_enemy_first_flags = first1 ? rd32(first1 + O_Z_CAT) : 0u;
    r->p2_enemy_first_x = first2 ? rd32(first2 + O_Z_POS_X) : 0u;
    r->p2_enemy_first_y = first2 ? rd32(first2 + O_Z_POS_Y) : 0u;
    r->p2_enemy_first_flags = first2 ? rd32(first2 + O_Z_CAT) : 0u;

    r->p1_slow_mult_x = ai1 ? rd32(ai1 + O_SLOW_MULT_X) : 0u;
    r->p2_slow_mult_x = ai2 ? rd32(ai2 + O_SLOW_MULT_X) : 0u;
    r->p1_slow_mult_y = ai1 ? rd32(ai1 + O_SLOW_MULT_Y) : 0u;
    r->p2_slow_mult_y = ai2 ? rd32(ai2 + O_SLOW_MULT_Y) : 0u;

    r->os_interrupt_time_lo = rd32(A_INTERRUPT_TIME_LO);
    r->os_tick_count_lo     = rd32(A_OS_TICK_COUNT_LO);

    for (i = 0u; i < TICKBUS_RECORD_TAIL_COUNT; i++)
        r->reserved_tail[i] = 0u;

    r->coord_ring_state = coord_write(idx, b1, b2, e1, e2, ex, p1p, p2p, blast_n,
                                      hl_n1, hl_n2);

    tickbus_end_write(h, r, idx);
}
