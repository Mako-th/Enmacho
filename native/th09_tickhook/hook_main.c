
#include <windows.h>

#include "detour.h"

static HANDLE g_map;
static HANDLE g_coord_map;
static unsigned int g_coord_error;
static HANDLE g_mutex;
static int    g_already;
static unsigned int g_input_error;
static unsigned int g_hit_error;
static unsigned int g_speed_error;
static unsigned int g_origin_error;
static unsigned int g_blast_error;
static unsigned int g_laser_origin_error;

static const unsigned int k_origin_site_order[4] = {
    DETOUR_SITE_ALLOC_FAIL_LO, DETOUR_SITE_ALLOC_FAIL_HI,
    DETOUR_SITE_BULLET_LO,     DETOUR_SITE_BULLET_HI,
};

static void *origin_site_fn(unsigned int site)
{
    if (site == DETOUR_SITE_BULLET_LO)
        return (void *)tickhook_bullet_lo;
    if (site == DETOUR_SITE_BULLET_HI)
        return (void *)tickhook_bullet_hi;
    return (void *)tickhook_alloc_fail;
}

static unsigned int install_bullet_origin(void)
{
    unsigned int i, rc;

    for (i = 0u; i < 4u; i++) {
        rc = detour_install(k_origin_site_order[i],
                            origin_site_fn(k_origin_site_order[i]));
        if (rc != TICKBUS_ERR_NONE) {
            while (i > 0u) {
                i--;
                if (detour_is_installed(k_origin_site_order[i]))
                    (void)detour_remove(k_origin_site_order[i]);
            }
            return rc;
        }
    }
    return TICKBUS_ERR_NONE;
}

static unsigned int remove_bullet_origin(void)
{
    unsigned int i, rc = TICKBUS_ERR_NONE;

    for (i = 4u; i > 0u; i--) {
        unsigned int site = k_origin_site_order[i - 1u];
        if (detour_is_installed(site)) {
            unsigned int r = detour_remove(site);
            if (rc == TICKBUS_ERR_NONE)
                rc = r;
        }
    }
    return rc;
}

static unsigned int open_tickbus(void)
{
    volatile TickBusHeader *h;

    if (g_tickbus)
        return TICKBUS_ERR_NONE;

    if (!g_map) {
        g_map = OpenFileMappingW(FILE_MAP_WRITE, FALSE, TICKBUS_NAME_W);
        if (!g_map)
            return TICKBUS_ERR_BUS_MISSING;
    }

    h = (volatile TickBusHeader *)MapViewOfFile(g_map, FILE_MAP_WRITE, 0, 0,
                                                TICKBUS_TOTAL_SIZE);
    if (!h) {
        CloseHandle(g_map);
        g_map = NULL;
        return TICKBUS_ERR_BUS_MISSING;
    }

    if (!tickbus_header_ok(h)) {
        UnmapViewOfFile((LPCVOID)h);
        CloseHandle(g_map);
        g_map = NULL;
        return TICKBUS_ERR_BUS_LAYOUT;
    }

    g_tickbus = h;
    return TICKBUS_ERR_NONE;
}

static unsigned int open_coordbus(void)
{
    volatile CoordBusHeader *h;

    if (g_coordbus)
        return TICKBUS_ERR_NONE;

    if (!g_coord_map) {
        g_coord_map = OpenFileMappingW(FILE_MAP_WRITE, FALSE, COORDBUS_NAME_W);
        if (!g_coord_map)
            return TICKBUS_ERR_BUS_MISSING;
    }

    h = (volatile CoordBusHeader *)MapViewOfFile(g_coord_map, FILE_MAP_WRITE, 0, 0,
                                                 COORDBUS_TOTAL_SIZE);
    if (!h) {
        CloseHandle(g_coord_map);
        g_coord_map = NULL;
        return TICKBUS_ERR_BUS_MISSING;
    }

    if (!coordbus_header_ok(h)) {
        UnmapViewOfFile((LPCVOID)h);
        CloseHandle(g_coord_map);
        g_coord_map = NULL;
        return TICKBUS_ERR_BUS_LAYOUT;
    }

    g_coordbus = h;
    return TICKBUS_ERR_NONE;
}

static void publish_coord_state(void)
{
    volatile CoordBusHeader *c = g_coordbus;
    if (!c)
        return;
    c->coord_state    = g_hook_state;
    c->last_error     = g_coord_error;
    c->game_pid       = (unsigned int)GetCurrentProcessId();
    c->coord_ticks    = g_coord_ticks;
    c->skipped_ticks  = g_coord_skipped;
    c->origin_hits    = g_origin_hits;
    c->origin_dropped = g_origin_dropped;
    c->origin_error   = g_origin_error;
    c->enemy_kind_clash = g_enemy_kind_clash;
    c->blast_hits    = g_blast_hits;
    c->blast_dropped = g_blast_dropped;
    c->blast_error   = g_blast_error;
    c->laser_origin_hits    = g_laser_origin_hits;
    c->laser_origin_dropped = g_laser_origin_dropped;
    c->laser_origin_error   = g_laser_origin_error;
    c->hitlist_clamped = g_hitlist_clamped;
}

static void publish_state(void)
{
    volatile TickBusHeader *h = g_tickbus;
    if (!h)
        return;
    h->hook_state = g_hook_state;
    h->last_error = g_last_error;
    h->game_pid   = (unsigned int)GetCurrentProcessId();
    h->hook_addr  = detour_is_installed(DETOUR_SITE_CAPTURE)
                  ? detour_site_addr(DETOUR_SITE_CAPTURE) : 0u;
    h->input_hook_addr = detour_is_installed(DETOUR_SITE_INPUT_POLL)
                       ? detour_site_addr(DETOUR_SITE_INPUT_POLL) : 0u;
    h->input_ticks      = g_input_ticks;
    h->input_poll_ticks = g_input_poll_ticks;
    h->input_hits       = g_input_hits;
    h->input_error      = g_input_error;
    h->hit_hook_addr = detour_is_installed(DETOUR_SITE_HIT)
                     ? detour_site_addr(DETOUR_SITE_HIT) : 0u;
    h->hit_error  = g_hit_error;
    h->hit_ticks  = g_hit_ticks;
    h->speed_hook_addr = detour_is_installed(DETOUR_SITE_SPEED_RESET)
                       ? detour_site_addr(DETOUR_SITE_SPEED_RESET) : 0u;
    h->speed_error = g_speed_error;
    h->speed_ticks = g_speed_ticks;
    h->dropped    = 0u;
    publish_coord_state();
}


__declspec(dllexport) DWORD WINAPI TickHookAttach(LPVOID param)
{
    unsigned int rc;
    unsigned int bus_rc;
    int force_armed = ((DWORD)(UINT_PTR)param == 1u);

    if (g_already) {
        g_last_error = TICKBUS_ERR_ALREADY;
        return TICKBUS_ERR_ALREADY;
    }
    if (detour_is_installed(DETOUR_SITE_CAPTURE)) {
        g_last_error = TICKBUS_ERR_ALREADY;
        publish_state();
        return TICKBUS_ERR_ALREADY;
    }

    bus_rc = open_tickbus();
    g_coord_ticks   = 0u;
    g_coord_skipped = 0u;
    g_enemy_kind_clash = 0u;
    g_coord_error   = open_coordbus();

    rc = detour_install(DETOUR_SITE_CAPTURE, (void *)tickhook_capture);
    if (rc != TICKBUS_ERR_NONE) {
        g_hook_state = TICKBUS_STATE_ERROR;
        g_last_error = rc;
        publish_state();
        return rc;
    }

    g_input_ticks      = 0u;
    g_input_poll_ticks = 0u;
    g_input_hits       = 0u;
    (void)detour_install(DETOUR_SITE_INPUT_FEED, (void *)tickhook_input);
    g_input_error  = detour_install(DETOUR_SITE_INPUT_POLL, (void *)tickhook_input_poll);

    g_hit_ticks = 0u;
    g_hit_error = detour_install(DETOUR_SITE_HIT, (void *)tickhook_hit);

    g_speed_ticks = 0u;
    tickhook_reset_hitlist();
    g_hitlist_armed = 0u;
    g_speed_error = detour_install(DETOUR_SITE_SPEED_RESET,
                                   (void *)tickhook_speed_reset);
    if (g_speed_error == TICKBUS_ERR_NONE)
        g_hitlist_armed = 1u;

    tickhook_reset_bullet_origin();
    g_origin_armed = 0u;
    g_origin_error = install_bullet_origin();
    if (g_origin_error == TICKBUS_ERR_NONE)
        g_origin_armed = 1u;

    tickhook_reset_enemy_blast();
    g_blast_armed = 0u;
    g_blast_error = detour_install(DETOUR_SITE_ENEMY_BLAST,
                                   (void *)tickhook_enemy_blast);
    if (g_blast_error == TICKBUS_ERR_NONE)
        g_blast_armed = 1u;

    tickhook_reset_laser_origin();
    g_laser_origin_armed = 0u;
    g_laser_origin_error = detour_install(DETOUR_SITE_LASER_BORN,
                                          (void *)tickhook_laser_born);
    if (g_laser_origin_error == TICKBUS_ERR_NONE)
        g_laser_origin_armed = 1u;

    if (g_tickbus) {
        g_tickbus->writer_tid = 0u;
        g_tickbus->armed_ticks = 0u;
    }
    g_last_error = (bus_rc == TICKBUS_ERR_NONE) ? TICKBUS_ERR_NONE : bus_rc;

    g_hook_state = (g_tickbus && !force_armed)
                 ? TICKBUS_STATE_RUNNING : TICKBUS_STATE_ARMED;
    publish_state();
    return TICKBUS_ERR_NONE;
}

__declspec(dllexport) DWORD WINAPI TickHookDetach(LPVOID param)
{
    unsigned int rc;
    unsigned int in_rc;
    (void)param;

    if (!detour_is_installed(DETOUR_SITE_CAPTURE)) {
        g_last_error = TICKBUS_ERR_NOT_ATTACHED;
        publish_state();
        return TICKBUS_ERR_NOT_ATTACHED;
    }

    g_hook_state = TICKBUS_STATE_DETACHED;
    tickbus_barrier();

    (void)vpatch_skip(TICKBUS_VPATCH_RESTORE);

    in_rc = detour_is_installed(DETOUR_SITE_INPUT_POLL)
          ? detour_remove(DETOUR_SITE_INPUT_POLL) : TICKBUS_ERR_NONE;
    if (detour_is_installed(DETOUR_SITE_INPUT_FEED)) {
        unsigned int feed_rc = detour_remove(DETOUR_SITE_INPUT_FEED);
        if (in_rc == TICKBUS_ERR_NONE)
            in_rc = feed_rc;
    }
    if (detour_is_installed(DETOUR_SITE_HIT)) {
        unsigned int hit_rc = detour_remove(DETOUR_SITE_HIT);
        if (in_rc == TICKBUS_ERR_NONE)
            in_rc = hit_rc;
    }
    g_hitlist_armed = 0u;
    if (detour_is_installed(DETOUR_SITE_SPEED_RESET)) {
        unsigned int sp_rc = detour_remove(DETOUR_SITE_SPEED_RESET);
        if (in_rc == TICKBUS_ERR_NONE)
            in_rc = sp_rc;
    }
    g_origin_armed = 0u;
    {
        unsigned int or_rc = remove_bullet_origin();
        if (in_rc == TICKBUS_ERR_NONE)
            in_rc = or_rc;
    }
    g_blast_armed = 0u;
    if (detour_is_installed(DETOUR_SITE_ENEMY_BLAST)) {
        unsigned int bl_rc = detour_remove(DETOUR_SITE_ENEMY_BLAST);
        if (in_rc == TICKBUS_ERR_NONE)
            in_rc = bl_rc;
    }
    g_laser_origin_armed = 0u;
    if (detour_is_installed(DETOUR_SITE_LASER_BORN)) {
        unsigned int lo_rc = detour_remove(DETOUR_SITE_LASER_BORN);
        if (in_rc == TICKBUS_ERR_NONE)
            in_rc = lo_rc;
    }

    rc = detour_remove(DETOUR_SITE_CAPTURE);
    if (rc == TICKBUS_ERR_NONE)
        rc = in_rc;
    if (rc != TICKBUS_ERR_NONE) {
        g_hook_state = TICKBUS_STATE_ERROR;
        g_last_error = rc;
        publish_state();
        return rc;
    }
    g_last_error = TICKBUS_ERR_NONE;
    publish_state();
    return TICKBUS_ERR_NONE;
}

__declspec(dllexport) DWORD WINAPI TickHookStatus(LPVOID param)
{
    (void)param;
    publish_state();
    return TICKBUS_STATUS_PACK(g_hook_state, g_last_error,
                               g_tickbus ? 1u : 0u, g_tick_count);
}

__declspec(dllexport) DWORD WINAPI TickHookSetState(LPVOID param)
{
    DWORD want = (DWORD)(UINT_PTR)param;

    if (!detour_is_installed(DETOUR_SITE_CAPTURE)) {
        g_last_error = TICKBUS_ERR_NOT_ATTACHED;
        return TICKBUS_ERR_NOT_ATTACHED;
    }
    if (want == TICKBUS_STATE_ARMED) {
        g_hook_state = TICKBUS_STATE_ARMED;
        publish_state();
        return TICKBUS_ERR_NONE;
    }
    if (want == TICKBUS_STATE_RUNNING) {
        unsigned int bus_rc = open_tickbus();
        if (bus_rc != TICKBUS_ERR_NONE) {
            g_last_error = bus_rc;
            return bus_rc;
        }
        g_coord_error = open_coordbus();
        g_tickbus->writer_tid = 0u;
        g_last_error = TICKBUS_ERR_NONE;
        g_hook_state = TICKBUS_STATE_RUNNING;
        publish_state();
        return TICKBUS_ERR_NONE;
    }
    return TICKBUS_ERR_NOT_ATTACHED;
}

__declspec(dllexport) DWORD WINAPI TickHookCoordOpen(LPVOID param)
{
    (void)param;
    g_coord_error = open_coordbus();
    publish_coord_state();
    return g_coord_error;
}

__declspec(dllexport) DWORD WINAPI TickHookVpatchSkip(LPVOID param)
{
    return (DWORD)vpatch_skip((unsigned int)(DWORD)(UINT_PTR)param);
}


BOOL WINAPI DllMain(HINSTANCE inst, DWORD reason, LPVOID reserved)
{
    (void)reserved;

    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(inst);

        g_hook_state = TICKBUS_STATE_IDLE;
        g_last_error = TICKBUS_ERR_NONE;

        g_mutex = CreateMutexW(NULL, FALSE, TICKBUS_MUTEX_NAME_W);
        if (g_mutex && GetLastError() == ERROR_ALREADY_EXISTS)
            g_already = 1;
    }
    return TRUE;
}
