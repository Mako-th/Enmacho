
#include <windows.h>
#include <tlhelp32.h>

#include "detour.h"

typedef struct DetourSite {
    unsigned int  addr;
    unsigned int  len;
    unsigned char push_ops[DETOUR_MAX_PUSH_OPS];
    unsigned char expect[DETOUR_MAX_PATCH_LEN];
} DetourSite;

static const DetourSite k_sites[DETOUR_SITE_COUNT] = {
    { 0x0042C700u, 7u, { 0x00, 0x00, 0x00 },
      { 0x55, 0x8B, 0xEC, 0x51, 0x89, 0x4D, 0xFC, 0x00 } },
    { 0x0042AD3Fu, 5u, { 0x51, 0x00, 0x00 },
      { 0x33, 0xF6, 0x66, 0x8B, 0x31, 0x00, 0x00, 0x00 } },
    { 0x0042BE0Bu, 5u, { 0x57, 0x00, 0x00 },
      { 0x33, 0xC0, 0x66, 0x8B, 0x07, 0x00, 0x00, 0x00 } },
    { 0x0041E8DAu, 5u, { 0x50, 0x00, 0x00 },
      { 0x8B, 0x40, 0x2C, 0x85, 0xC0, 0x00, 0x00, 0x00 } },
    { 0x0041BA80u, 6u, { 0x56, 0x00, 0x00 },
      { 0x55, 0x8B, 0xEC, 0x83, 0xEC, 0x0C, 0x00, 0x00 } },
    { 0x00413164u, 5u, { 0x55, 0x56, 0x50 },
      { 0x8B, 0xF8, 0x8B, 0x45, 0xFC, 0x00, 0x00, 0x00 } },
    { 0x00413234u, 5u, { 0x55, 0x56, 0x50 },
      { 0x8B, 0xF8, 0x8B, 0x45, 0xFC, 0x00, 0x00, 0x00 } },
    { 0x00412A33u, 5u, { 0x00, 0x00, 0x00 },
      { 0x8B, 0xC3, 0x5B, 0x8B, 0xE5, 0x00, 0x00, 0x00 } },
    { 0x00412B43u, 5u, { 0x00, 0x00, 0x00 },
      { 0x8B, 0xC3, 0x5B, 0x8B, 0xE5, 0x00, 0x00, 0x00 } },
    { 0x0041D110u, 6u, { 0x57, 0x56, 0x00 },
      { 0x55, 0x8B, 0xEC, 0x8B, 0x45, 0x1C, 0x00, 0x00, 0x00, 0x00 } },
    { 0x00413366u, 10u, { 0x57, 0x56, 0x00 },
      { 0xC7, 0x86, 0x84, 0x05, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00 } },
};

static void        *g_trampoline[DETOUR_SITE_COUNT];
static int          g_installed[DETOUR_SITE_COUNT];
static unsigned char g_orig[DETOUR_SITE_COUNT][DETOUR_MAX_PATCH_LEN];
static unsigned char g_patch[DETOUR_SITE_COUNT][DETOUR_MAX_PATCH_LEN];


static void put32(unsigned char *p, unsigned int v)
{
    p[0] = (unsigned char)(v & 0xFFu);
    p[1] = (unsigned char)((v >> 8) & 0xFFu);
    p[2] = (unsigned char)((v >> 16) & 0xFFu);
    p[3] = (unsigned char)((v >> 24) & 0xFFu);
}

static unsigned int rel32(unsigned int target, unsigned int next_ip)
{
    return (unsigned int)(target - next_ip);
}

static int addr_readable(unsigned int addr, unsigned int len)
{
    MEMORY_BASIC_INFORMATION mbi;
    unsigned int end;
    const DWORD readable = PAGE_EXECUTE_READ | PAGE_EXECUTE_READWRITE
                         | PAGE_EXECUTE_WRITECOPY | PAGE_READONLY
                         | PAGE_READWRITE | PAGE_WRITECOPY | PAGE_EXECUTE;

    if (VirtualQuery((LPCVOID)(UINT_PTR)addr, &mbi, sizeof(mbi)) != sizeof(mbi))
        return 0;
    if (mbi.State != MEM_COMMIT)
        return 0;
    if ((mbi.Protect & readable) == 0)
        return 0;
    if (mbi.Protect & PAGE_GUARD)
        return 0;
    end = (unsigned int)(UINT_PTR)mbi.BaseAddress + (unsigned int)mbi.RegionSize;
    return (addr + len) <= end;
}

static unsigned int patch_bytes_safely(unsigned int addr, const unsigned char *data,
                                       unsigned int len)
{
    const DWORD my_tid = GetCurrentThreadId();
    const DWORD my_pid = GetCurrentProcessId();
    unsigned int last_rc = TICKBUS_ERR_EIP_IN_RANGE;
    int attempt;

    for (attempt = 0; attempt < DETOUR_SUSPEND_RETRIES; attempt++) {
        HANDLE handles[DETOUR_MAX_THREADS];
        int count = 0;
        int suspended = 0;
        int blocked = 0;
        int overflow = 0;
        int i;
        HANDLE snap;
        THREADENTRY32 te;
        unsigned int rc = TICKBUS_ERR_EIP_IN_RANGE;

        if (attempt > 0)
            Sleep(DETOUR_RETRY_WAIT_MS);

        snap = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
        if (snap == INVALID_HANDLE_VALUE)
            return TICKBUS_ERR_SNAPSHOT;
        te.dwSize = sizeof(te);
        if (Thread32First(snap, &te)) {
            do {
                HANDLE h;
                if (te.th32OwnerProcessID != my_pid)
                    continue;
                if (te.th32ThreadID == my_tid)
                    continue;
                if (count >= DETOUR_MAX_THREADS) {
                    overflow = 1;
                    break;
                }
                h = OpenThread(THREAD_SUSPEND_RESUME | THREAD_GET_CONTEXT
                               | THREAD_QUERY_INFORMATION, FALSE, te.th32ThreadID);
                if (h)
                    handles[count++] = h;
            } while (Thread32Next(snap, &te));
        }
        CloseHandle(snap);

        if (overflow) {
            for (i = 0; i < count; i++)
                CloseHandle(handles[i]);
            return TICKBUS_ERR_TOO_MANY_THREADS;
        }
        if (count == 0)
            return TICKBUS_ERR_NO_THREADS;

        for (i = 0; i < count; i++) {
            if (SuspendThread(handles[i]) != (DWORD)-1) {
                suspended++;
            } else {
                CloseHandle(handles[i]);
                handles[i] = NULL;
            }
        }

        if (suspended == 0)
            return TICKBUS_ERR_NO_THREADS;

        for (i = 0; i < count; i++) {
            CONTEXT ctx;
            if (!handles[i])
                continue;
            ctx.ContextFlags = CONTEXT_CONTROL;
            if (!GetThreadContext(handles[i], &ctx))
                continue;
            if (ctx.Eip >= addr && ctx.Eip < addr + len)
                blocked = 1;
        }

        if (!blocked) {
            DWORD old = 0, tmp = 0;
            if (!VirtualProtect((LPVOID)(UINT_PTR)addr, len, PAGE_EXECUTE_READWRITE, &old)) {
                rc = TICKBUS_ERR_PROTECT;
            } else {
                volatile unsigned char *dst = (volatile unsigned char *)(UINT_PTR)addr;
                unsigned int k;
                for (k = 0; k < len; k++)
                    dst[k] = data[k];
                FlushInstructionCache(GetCurrentProcess(), (LPCVOID)(UINT_PTR)addr, len);
                VirtualProtect((LPVOID)(UINT_PTR)addr, len, old, &tmp);
                rc = TICKBUS_ERR_NONE;
            }
        }

        for (i = 0; i < count; i++) {
            if (!handles[i])
                continue;
            ResumeThread(handles[i]);
            CloseHandle(handles[i]);
        }

        if (!blocked)
            return rc;
        last_rc = TICKBUS_ERR_EIP_IN_RANGE;
    }
    return last_rc;
}


unsigned int detour_install(unsigned int site, void *fn)
{
    const DetourSite *s;
    unsigned char *t;
    unsigned int tramp = 0;
    unsigned int n = 0;
    unsigned int i;
    unsigned int nargs = 0;
    unsigned int rc;

    if (site >= DETOUR_SITE_COUNT)
        return TICKBUS_ERR_BAD_SITE;
    s = &k_sites[site];

    if (g_installed[site])
        return TICKBUS_ERR_ALREADY;
    if (!fn)
        return TICKBUS_ERR_ALLOC;

    if (!addr_readable(s->addr, s->len))
        return TICKBUS_ERR_UNREADABLE;
    for (i = 0; i < s->len; i++)
        g_orig[site][i] = ((const volatile unsigned char *)(UINT_PTR)s->addr)[i];
    for (i = 0; i < s->len; i++) {
        if (g_orig[site][i] != s->expect[i])
            return TICKBUS_ERR_SIGNATURE;
    }

    g_trampoline[site] = VirtualAlloc(NULL, DETOUR_TRAMPOLINE_SIZE, MEM_COMMIT | MEM_RESERVE,
                                      PAGE_EXECUTE_READWRITE);
    if (!g_trampoline[site])
        return TICKBUS_ERR_ALLOC;
    tramp = (unsigned int)(UINT_PTR)g_trampoline[site];

    t = (unsigned char *)g_trampoline[site];
    for (i = 0; i < DETOUR_TRAMPOLINE_SIZE; i++)
        t[i] = 0xCC;

    while (nargs < DETOUR_MAX_PUSH_OPS && s->push_ops[nargs])
        nargs++;

    t[n++] = 0x60;
    t[n++] = 0x9C;
    t[n++] = 0xFC;
    for (i = 0; i < nargs; i++)
        t[n++] = s->push_ops[i];
    t[n++] = 0xE8;
    put32(&t[n], rel32((unsigned int)(UINT_PTR)fn, tramp + n + 4));
    n += 4;
    if (nargs) {
        t[n++] = 0x83;
        t[n++] = 0xC4;
        t[n++] = (unsigned char)(4u * nargs);
    }
    t[n++] = 0x9D;
    t[n++] = 0x61;
    for (i = 0; i < s->len; i++)
        t[n++] = g_orig[site][i];
    t[n++] = 0xE9;
    put32(&t[n], rel32(s->addr + s->len, tramp + n + 4));
    n += 4;

    FlushInstructionCache(GetCurrentProcess(), g_trampoline[site], DETOUR_TRAMPOLINE_SIZE);

    g_patch[site][0] = 0xE9;
    put32(&g_patch[site][1], rel32(tramp, s->addr + 5));
    for (i = 5; i < s->len; i++)
        g_patch[site][i] = 0x90;

    rc = patch_bytes_safely(s->addr, g_patch[site], s->len);
    if (rc != TICKBUS_ERR_NONE)
        return rc;

    g_installed[site] = 1;
    return TICKBUS_ERR_NONE;
}

unsigned int detour_remove(unsigned int site)
{
    const DetourSite *s;
    unsigned int i;
    unsigned int rc;

    if (site >= DETOUR_SITE_COUNT)
        return TICKBUS_ERR_BAD_SITE;
    s = &k_sites[site];

    if (!g_installed[site])
        return TICKBUS_ERR_NOT_ATTACHED;

    if (!addr_readable(s->addr, s->len))
        return TICKBUS_ERR_UNREADABLE;
    for (i = 0; i < s->len; i++) {
        if (((const volatile unsigned char *)(UINT_PTR)s->addr)[i] != g_patch[site][i])
            return TICKBUS_ERR_FOREIGN_PATCH;
    }

    rc = patch_bytes_safely(s->addr, g_orig[site], s->len);
    if (rc != TICKBUS_ERR_NONE)
        return rc;

    g_installed[site] = 0;
    return TICKBUS_ERR_NONE;
}

int detour_is_installed(unsigned int site)
{
    if (site >= DETOUR_SITE_COUNT)
        return 0;
    return g_installed[site];
}

unsigned int detour_trampoline_addr(unsigned int site)
{
    if (site >= DETOUR_SITE_COUNT)
        return 0u;
    return (unsigned int)(UINT_PTR)g_trampoline[site];
}

unsigned int detour_site_addr(unsigned int site)
{
    if (site >= DETOUR_SITE_COUNT)
        return 0u;
    return k_sites[site].addr;
}


static void vpatch_ref_bytes(unsigned char *out, unsigned int target)
{
    out[0] = 0x0Fu;
    out[1] = 0xB7u;
    out[2] = 0x0Du;
    put32(&out[VPATCH_SKIP_IMM_OFF], target);
}

static void vpatch_text_range(const void *mod, unsigned int *out_start, unsigned int *out_size)
{
    const unsigned char *base = (const unsigned char *)mod;
    const IMAGE_DOS_HEADER *dos;
    const IMAGE_NT_HEADERS *nt;
    const IMAGE_SECTION_HEADER *sec;
    unsigned int i;

    *out_start = 0u;
    *out_size  = 0u;

    if (!addr_readable((unsigned int)(UINT_PTR)base, sizeof(IMAGE_DOS_HEADER)))
        return;
    dos = (const IMAGE_DOS_HEADER *)base;
    if (dos->e_magic != IMAGE_DOS_SIGNATURE)
        return;
    if (!addr_readable((unsigned int)(UINT_PTR)base + (unsigned int)dos->e_lfanew,
                       sizeof(IMAGE_NT_HEADERS)))
        return;
    nt = (const IMAGE_NT_HEADERS *)(base + dos->e_lfanew);
    if (nt->Signature != IMAGE_NT_SIGNATURE)
        return;

    sec = (const IMAGE_SECTION_HEADER *)((const unsigned char *)&nt->OptionalHeader
                                         + nt->FileHeader.SizeOfOptionalHeader);
    for (i = 0u; i < nt->FileHeader.NumberOfSections; i++) {
        if (!addr_readable((unsigned int)(UINT_PTR)&sec[i], sizeof(IMAGE_SECTION_HEADER)))
            return;
        if (sec[i].Characteristics & IMAGE_SCN_MEM_EXECUTE) {
            unsigned int size = sec[i].Misc.VirtualSize;
            if (size == 0u)
                size = sec[i].SizeOfRawData;
            *out_start = (unsigned int)(UINT_PTR)base + sec[i].VirtualAddress;
            *out_size  = size;
            return;
        }
    }
}

static unsigned int vpatch_count_matches(unsigned int start, unsigned int size,
                                         const unsigned char *pat, unsigned int patlen,
                                         unsigned int *found_addr)
{
    unsigned int count = 0u;
    unsigned int i;

    if (size < patlen || !addr_readable(start, size))
        return 0u;

    for (i = 0u; i + patlen <= size; i++) {
        const volatile unsigned char *p =
            (const volatile unsigned char *)(UINT_PTR)(start + i);
        unsigned int k;
        int same = 1;
        for (k = 0u; k < patlen; k++) {
            if (p[k] != pat[k]) { same = 0; break; }
        }
        if (same) {
            count++;
            *found_addr = start + i;
        }
    }
    return count;
}

static int vpatch_locate(const void *mod, unsigned int *out_addr, unsigned int *out_state)
{
    unsigned char plain[VPATCH_SKIP_LEN];
    unsigned char patched[VPATCH_SKIP_LEN];
    unsigned int text_start, text_size;
    unsigned int addr_plain = 0u, addr_patched = 0u;
    unsigned int n_plain, n_patched;

    vpatch_ref_bytes(plain,   capture_input_mask_p1_addr());
    vpatch_ref_bytes(patched, capture_input_mask_addr());

    vpatch_text_range(mod, &text_start, &text_size);
    if (text_size == 0u)
        return 0;

    n_plain   = vpatch_count_matches(text_start, text_size, plain,   VPATCH_SKIP_LEN,
                                     &addr_plain);
    n_patched = vpatch_count_matches(text_start, text_size, patched, VPATCH_SKIP_LEN,
                                     &addr_patched);
    if (n_plain + n_patched != 1u)
        return 0;

    if (n_plain == 1u) {
        *out_addr  = addr_plain;
        *out_state = TICKBUS_VPATCH_OFF;
    } else {
        *out_addr  = addr_patched;
        *out_state = TICKBUS_VPATCH_ON;
    }
    return 1;
}

static unsigned int vpatch_result(unsigned int err, unsigned int state, unsigned int rva)
{
    unsigned int packed = TICKBUS_VPATCH_PACK(err, state);
    packed |= (rva & VPATCH_RVA_MASK) << VPATCH_RVA_SHIFT;
    return packed;
}

unsigned int vpatch_skip(unsigned int op)
{
    HMODULE mod;
    unsigned int addr = 0u;
    unsigned int state = TICKBUS_VPATCH_FOREIGN;
    unsigned int want;
    unsigned int rc;
    unsigned int rva;
    unsigned char plain[VPATCH_SKIP_LEN];
    unsigned char patched[VPATCH_SKIP_LEN];

    mod = GetModuleHandleW(VPATCH_MODULE_NAME);
    if (!mod)
        return vpatch_result(TICKBUS_ERR_NO_VPATCH, TICKBUS_VPATCH_ABSENT, 0u);

    if (!vpatch_locate(mod, &addr, &state)) {
        if (op == TICKBUS_VPATCH_QUERY)
            return vpatch_result(TICKBUS_ERR_NONE, TICKBUS_VPATCH_FOREIGN, 0u);
        return vpatch_result(TICKBUS_ERR_SIGNATURE, TICKBUS_VPATCH_FOREIGN, 0u);
    }
    rva = addr - (unsigned int)(UINT_PTR)mod;

    if (op == TICKBUS_VPATCH_QUERY)
        return vpatch_result(TICKBUS_ERR_NONE, state, rva);
    if (op != TICKBUS_VPATCH_APPLY && op != TICKBUS_VPATCH_RESTORE)
        return vpatch_result(TICKBUS_ERR_BAD_SITE, state, rva);

    want = (op == TICKBUS_VPATCH_APPLY) ? TICKBUS_VPATCH_ON : TICKBUS_VPATCH_OFF;
    if (state == want)
        return vpatch_result(TICKBUS_ERR_NONE, state, rva);

    vpatch_ref_bytes(plain,   capture_input_mask_p1_addr());
    vpatch_ref_bytes(patched, capture_input_mask_addr());
    rc = patch_bytes_safely(addr, (want == TICKBUS_VPATCH_ON) ? patched : plain,
                            VPATCH_SKIP_LEN);
    if (rc != TICKBUS_ERR_NONE)
        return vpatch_result(rc, state, rva);
    return vpatch_result(TICKBUS_ERR_NONE, want, rva);
}
