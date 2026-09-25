#ifndef TH09_DETOUR_H
#define TH09_DETOUR_H

#include "tickbus.h"

#define DETOUR_SITE_CAPTURE       0u
#define DETOUR_SITE_INPUT_FEED    1u
#define DETOUR_SITE_INPUT_POLL    2u
#define DETOUR_SITE_HIT           3u
#define DETOUR_SITE_SPEED_RESET   4u
#define DETOUR_SITE_BULLET_LO     5u
#define DETOUR_SITE_BULLET_HI     6u
#define DETOUR_SITE_ALLOC_FAIL_LO 7u
#define DETOUR_SITE_ALLOC_FAIL_HI 8u
#define DETOUR_SITE_ENEMY_BLAST   9u
#define DETOUR_SITE_LASER_BORN    10u
#define DETOUR_SITE_COUNT         11u

#define DETOUR_MAX_PUSH_OPS 3u

#define DETOUR_MAX_PATCH_LEN 10u

#define DETOUR_SUSPEND_RETRIES 20
#define DETOUR_RETRY_WAIT_MS   4

#define DETOUR_MAX_THREADS 256

#define DETOUR_TRAMPOLINE_SIZE 64

unsigned int detour_install(unsigned int site, void *fn);
unsigned int detour_remove(unsigned int site);
int          detour_is_installed(unsigned int site);
unsigned int detour_trampoline_addr(unsigned int site);
unsigned int detour_site_addr(unsigned int site);

#define VPATCH_MODULE_NAME    L"vpatch_th09.dll"
#define VPATCH_SKIP_LEN       7u
#define VPATCH_SKIP_IMM_OFF   3u

#define VPATCH_RVA_SHIFT      12u
#define VPATCH_RVA_MASK       0xFFFFFu
unsigned int vpatch_skip(unsigned int op);

extern volatile TickBusHeader *g_tickbus;
extern volatile CoordBusHeader *g_coordbus;
extern volatile unsigned int   g_coord_ticks;
extern volatile unsigned int   g_coord_skipped;
extern volatile unsigned int   g_hook_state;
extern volatile unsigned int   g_last_error;
extern volatile unsigned int   g_tick_count;
extern volatile unsigned int   g_input_ticks;
extern volatile unsigned int   g_input_poll_ticks;
extern volatile unsigned int   g_input_hits;
extern volatile unsigned int   g_hit_ticks;
extern volatile unsigned int   g_speed_ticks;
extern volatile unsigned int   g_origin_hits;
extern volatile unsigned int   g_origin_dropped;
extern volatile unsigned int   g_origin_armed;
extern volatile unsigned int   g_enemy_kind_clash;
extern volatile unsigned int   g_blast_hits;
extern volatile unsigned int   g_blast_dropped;
extern volatile unsigned int   g_blast_armed;
extern volatile unsigned int   g_hitlist_armed;
extern volatile unsigned int   g_hitlist_clamped;
extern volatile unsigned int   g_laser_origin_hits;
extern volatile unsigned int   g_laser_origin_dropped;
extern volatile unsigned int   g_laser_origin_armed;

void tickhook_capture(void);
void tickhook_input(unsigned int im_ptr);
void tickhook_input_poll(unsigned int im_ptr);
void tickhook_hit(unsigned int elem_ptr);
void tickhook_speed_reset(unsigned int player_ptr);
void tickhook_bullet_lo(unsigned int bullet_ptr, unsigned int shooter, unsigned int frame);
void tickhook_bullet_hi(unsigned int bullet_ptr, unsigned int shooter, unsigned int frame);
void tickhook_alloc_fail(void);
void tickhook_reset_bullet_origin(void);
void tickhook_enemy_blast(unsigned int enemy_ptr, unsigned int pos_ptr);
void tickhook_reset_enemy_blast(void);
void tickhook_reset_hitlist(void);
void tickhook_laser_born(unsigned int laser_ptr, unsigned int shooter_cfg);
void tickhook_reset_laser_origin(void);

unsigned int capture_input_mask_addr(void);
unsigned int capture_input_mask_p1_addr(void);

#endif
