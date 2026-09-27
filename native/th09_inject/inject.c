
#include <windows.h>
#include <tlhelp32.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <wchar.h>

#define DEFAULT_EXE      L"th09.exe"
#define DEFAULT_DLL      L"th09_tickhook.dll"
#define REMOTE_WAIT_MS   10000

#define ACT_NONE    0
#define ACT_ATTACH  1
#define ACT_DETACH  2
#define ACT_STATUS  3
#define ACT_ARM     4
#define ACT_RUN     5
#define ACT_VPATCH_ON    6
#define ACT_VPATCH_OFF   7
#define ACT_VPATCH_STATE 8

#define TICKBUS_STATUS_STATE(v)   (((v) >> 28) & 0xFu)
#define TICKBUS_STATUS_ERROR(v)   (((v) >> 20) & 0xFFu)
#define TICKBUS_STATUS_MAPPED(v)  (((v) >> 19) & 0x1u)
#define TICKBUS_STATUS_TICKS(v)   ((v) & 0x7FFFFu)

#define TICKBUS_VPATCH_ERROR(v)   ((v) & 0xFFu)
#define TICKBUS_VPATCH_STATE(v)   (((v) >> 8) & 0xFu)
#define TICKBUS_VPATCH_RVA(v)     (((v) >> 12) & 0xFFFFFu)

static const char *state_name(unsigned int s)
{
    switch (s) {
    case 0:  return "IDLE";
    case 1:  return "ARMED";
    case 2:  return "RUNNING";
    case 3:  return "DETACHED";
    case 15: return "ERROR";
    default: return "?";
    }
}

static const char *error_name(unsigned int e)
{
    switch (e) {
    case 0:  return "none";
    case 1:  return "already (double injection; the patch is applied once)";
    case 2:  return "signature mismatch at 0x0042C700 - ABORTED, nothing patched";
    case 3:  return "trampoline VirtualAlloc failed";
    case 4:  return "eip_in_patch_range (a thread stayed inside the patch)";
    case 5:  return "VirtualProtect failed";
    case 6:  return "no thread could be suspended";
    case 7:  return "CreateToolhelp32Snapshot failed";
    case 8:  return "not attached";
    case 9:  return "foreign patch - refused to restore";
    case 10: return "tick bus missing (Python side has not created it)";
    case 11: return "tick bus layout mismatch (version/record_size/capacity)";
    case 12: return "multithread: the hook ran on more than one thread (SPSC violated)";
    case 13: return "hook address not readable (wrong process?)";
    case 14: return "too many threads";
    case 15: return "bad site (an out-of-range hook site number)";
    case 16: return "vpatch_th09.dll is not loaded in the game";
    default: return "?";
    }
}

static const char *vpatch_state_name(unsigned int s, unsigned int rva)
{
    static char buf[160];
    switch (s) {
    case 0:
        snprintf(buf, sizeof(buf),
                  "OFF (stock: vpatch reads InputManager[0] 0x004ACE18, found at RVA 0x%X)",
                  rva);
        buf[sizeof(buf) - 1] = '\0';
        return buf;
    case 1:
        snprintf(buf, sizeof(buf),
                  "ON (patched: vpatch reads InputManager[2] 0x004ACF34, found at RVA 0x%X)",
                  rva);
        buf[sizeof(buf) - 1] = '\0';
        return buf;
    case 2:  return "FOREIGN (0, or more than 1, candidates in the executable section - "
                     "a different vpatch build, or another tool - untouched)";
    case 3:  return "ABSENT (vpatch_th09.dll is not loaded, or its PE headers could not be read)";
    default: return "?";
    }
}

static void print_vpatch(DWORD packed)
{
    unsigned int st = TICKBUS_VPATCH_STATE(packed);
    unsigned int er = TICKBUS_VPATCH_ERROR(packed);
    unsigned int rva = TICKBUS_VPATCH_RVA(packed);
    printf("  vpatch_skip : %u (%s)\n", st, vpatch_state_name(st, rva));
    printf("  last_error  : %u (%s)\n", er, error_name(er));
}


static DWORD find_pid(const wchar_t *exe)
{
    HANDLE snap;
    PROCESSENTRY32W pe;
    DWORD pid = 0;

    snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snap == INVALID_HANDLE_VALUE)
        return 0;
    pe.dwSize = sizeof(pe);
    if (Process32FirstW(snap, &pe)) {
        do {
            if (_wcsicmp(pe.szExeFile, exe) == 0) {
                pid = pe.th32ProcessID;
                break;
            }
        } while (Process32NextW(snap, &pe));
    }
    CloseHandle(snap);
    return pid;
}

static void *find_module(DWORD pid, const wchar_t *name)
{
    HANDLE snap;
    MODULEENTRY32W me;
    void *base = NULL;
    int tries;

    for (tries = 0; tries < 5 && !base; tries++) {
        snap = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, pid);
        if (snap == INVALID_HANDLE_VALUE) {
            Sleep(50);
            continue;
        }
        me.dwSize = sizeof(me);
        if (Module32FirstW(snap, &me)) {
            do {
                if (_wcsicmp(me.szModule, name) == 0) {
                    base = me.modBaseAddr;
                    break;
                }
            } while (Module32NextW(snap, &me));
        }
        CloseHandle(snap);
        if (!base)
            break;
    }
    return base;
}


static int call_remote(HANDLE proc, void *fn, void *param, DWORD *out)
{
    HANDLE th;
    DWORD code = 0;

    th = CreateRemoteThread(proc, NULL, 0, (LPTHREAD_START_ROUTINE)fn, param, 0, NULL);
    if (!th) {
        printf("[FAIL] CreateRemoteThread failed (error %lu)\n", GetLastError());
        return 0;
    }
    if (WaitForSingleObject(th, REMOTE_WAIT_MS) != WAIT_OBJECT_0) {
        printf("[FAIL] the remote thread did not finish within %d ms\n", REMOTE_WAIT_MS);
        CloseHandle(th);
        return 0;
    }
    GetExitCodeThread(th, &code);
    CloseHandle(th);
    if (out)
        *out = code;
    return 1;
}

static void *remote_load_library(HANDLE proc, DWORD pid, const wchar_t *dll_path,
                                 const wchar_t *dll_name)
{
    void *remote_str;
    void *base;
    SIZE_T bytes = (wcslen(dll_path) + 1) * sizeof(wchar_t);
    DWORD code = 0;
    HMODULE k32;
    void *load_fn;

    base = find_module(pid, dll_name);
    if (base) {
        printf("  module already loaded at %p (skipping LoadLibrary)\n", base);
        return base;
    }

    k32 = GetModuleHandleW(L"kernel32.dll");
    load_fn = k32 ? (void *)GetProcAddress(k32, "LoadLibraryW") : NULL;
    if (!load_fn) {
        printf("[FAIL] could not resolve kernel32!LoadLibraryW\n");
        return NULL;
    }

    remote_str = VirtualAllocEx(proc, NULL, bytes, MEM_COMMIT | MEM_RESERVE,
                                PAGE_READWRITE);
    if (!remote_str) {
        printf("[FAIL] VirtualAllocEx failed (error %lu)\n", GetLastError());
        return NULL;
    }
    if (!WriteProcessMemory(proc, remote_str, dll_path, bytes, NULL)) {
        printf("[FAIL] WriteProcessMemory failed (error %lu)\n", GetLastError());
        VirtualFreeEx(proc, remote_str, 0, MEM_RELEASE);
        return NULL;
    }

    if (!call_remote(proc, load_fn, remote_str, &code)) {
        VirtualFreeEx(proc, remote_str, 0, MEM_RELEASE);
        return NULL;
    }
    VirtualFreeEx(proc, remote_str, 0, MEM_RELEASE);

    if (code == 0) {
        printf("[FAIL] LoadLibraryW returned NULL in the target.\n");
        printf("       Is the DLL 32bit and are its imports resolvable?\n");
        return NULL;
    }
    base = (void *)(UINT_PTR)code;
    printf("  loaded at %p\n", base);
    return base;
}

static void *remote_proc_address(const wchar_t *dll_path, void *remote_base,
                                 const char *name)
{
    HMODULE local;
    void *proc;
    UINT_PTR offset;

    local = LoadLibraryExW(dll_path, NULL, DONT_RESOLVE_DLL_REFERENCES);
    if (!local) {
        printf("[FAIL] could not load the DLL locally (error %lu): %ls\n",
               GetLastError(), dll_path);
        return NULL;
    }
    proc = (void *)GetProcAddress(local, name);
    if (!proc) {
        printf("[FAIL] export %s not found. Are the names decorated?\n", name);
        printf("       Check with: objdump -p th09_tickhook.dll\n");
        FreeLibrary(local);
        return NULL;
    }
    offset = (UINT_PTR)proc - (UINT_PTR)local;
    FreeLibrary(local);
    return (void *)((UINT_PTR)remote_base + offset);
}

static void print_status(DWORD packed)
{
    unsigned int st = TICKBUS_STATUS_STATE(packed);
    unsigned int er = TICKBUS_STATUS_ERROR(packed);
    printf("  hook_state : %u (%s)\n", st, state_name(st));
    printf("  last_error : %u (%s)\n", er, error_name(er));
    printf("  tick bus   : %s\n", TICKBUS_STATUS_MAPPED(packed) ? "mapped" : "not mapped");
    printf("  ticks      : %u (low 19 bits of the hook fire count)\n",
           (unsigned int)TICKBUS_STATUS_TICKS(packed));
}


static void usage(void)
{
    printf("TH09 tick hook injector (32bit)\n\n");
    printf("  th09_inject.exe --attach [--pid N] [--dll PATH] [--armed]\n");
    printf("  th09_inject.exe --detach [--pid N]\n");
    printf("  th09_inject.exe --status [--pid N]\n");
    printf("  th09_inject.exe --arm    [--pid N]   keep the patch, stop writing records\n");
    printf("  th09_inject.exe --run    [--pid N]   start writing records again\n");
    printf("  th09_inject.exe --vpatch-on    [--pid N]  fast-forward from InputManager[2]\n");
    printf("  th09_inject.exe --vpatch-off   [--pid N]  back to InputManager[0]\n");
    printf("  th09_inject.exe --vpatch-state [--pid N]  read it, write nothing\n\n");
    printf("  --vpatch-* patch one instruction inside vpatch_th09.dll (plan B), not\n");
    printf("            the game.  Memory only: closing the game undoes it and the\n");
    printf("            file on disk is never written.  Needs the hook DLL loaded.\n");
    printf("  --armed   attach but do not write records (only count ticks).\n");
    printf("            Use this to tell a hook problem from a capture problem.\n");
    printf("  --dll     default: ..\\..\\th09_tickhook\\build\\th09_tickhook.dll,\n");
    printf("            then th09_tickhook.dll next to this exe\n\n");
    printf("Create the Tick Bus on the Python side BEFORE --attach, otherwise the\n");
    printf("hook attaches in ARMED state (it never creates the shared memory itself).\n");
}

int main(int argc, char **argv)
{
    int action = ACT_NONE;
    int armed = 0;
    DWORD pid = 0;
    wchar_t dll_path[MAX_PATH * 2];
    wchar_t dll_name[MAX_PATH];
    wchar_t *file_part = NULL;
    const char *dll_arg = NULL;
    HANDLE proc;
    void *base;
    void *fn;
    DWORD code = 0;
    const char *export_name = NULL;
    void *param = NULL;
    int i;

    for (i = 1; i < argc; i++) {
        if (strcmp(argv[i], "--attach") == 0)        action = ACT_ATTACH;
        else if (strcmp(argv[i], "--detach") == 0)   action = ACT_DETACH;
        else if (strcmp(argv[i], "--status") == 0)   action = ACT_STATUS;
        else if (strcmp(argv[i], "--arm") == 0)      action = ACT_ARM;
        else if (strcmp(argv[i], "--run") == 0)      action = ACT_RUN;
        else if (strcmp(argv[i], "--vpatch-on") == 0)    action = ACT_VPATCH_ON;
        else if (strcmp(argv[i], "--vpatch-off") == 0)   action = ACT_VPATCH_OFF;
        else if (strcmp(argv[i], "--vpatch-state") == 0) action = ACT_VPATCH_STATE;
        else if (strcmp(argv[i], "--armed") == 0)    armed = 1;
        else if (strcmp(argv[i], "--pid") == 0 && i + 1 < argc)
            pid = (DWORD)strtoul(argv[++i], NULL, 0);
        else if (strcmp(argv[i], "--dll") == 0 && i + 1 < argc)
            dll_arg = argv[++i];
        else {
            printf("unknown argument: %s\n\n", argv[i]);
            usage();
            return 2;
        }
    }
    if (action == ACT_NONE) {
        usage();
        return 2;
    }

    if (dll_arg) {
        wchar_t given[MAX_PATH * 2];
        int n = MultiByteToWideChar(CP_ACP, 0, dll_arg, -1, given,
                                    sizeof(given) / sizeof(given[0]));
        if (n <= 0) {
            printf("[FAIL] could not convert --dll to wide characters\n");
            return 2;
        }
        if (!GetFullPathNameW(given, sizeof(dll_path) / sizeof(dll_path[0]),
                              dll_path, &file_part)) {
            printf("[FAIL] GetFullPathNameW failed (error %lu)\n", GetLastError());
            return 2;
        }
    } else {
        static const wchar_t *const candidates[] = {
            L"..\\..\\th09_tickhook\\build\\" DEFAULT_DLL,
            DEFAULT_DLL,
        };
        wchar_t exe_dir[MAX_PATH * 2];
        wchar_t *slash;
        size_t i;
        DWORD n = GetModuleFileNameW(NULL, exe_dir,
                                     sizeof(exe_dir) / sizeof(exe_dir[0]));
        if (n == 0) {
            printf("[FAIL] GetModuleFileNameW failed\n");
            return 2;
        }
        slash = wcsrchr(exe_dir, L'\\');
        if (slash)
            slash[1] = 0;
        else
            exe_dir[0] = 0;

        dll_path[0] = 0;
        for (i = 0; i < sizeof(candidates) / sizeof(candidates[0]); i++) {
            wchar_t joined[MAX_PATH * 2];
            wchar_t full[MAX_PATH * 2];
            wcsncpy(joined, exe_dir, sizeof(joined) / sizeof(joined[0]) - 1);
            joined[sizeof(joined) / sizeof(joined[0]) - 1] = 0;
            wcsncat(joined, candidates[i],
                    sizeof(joined) / sizeof(joined[0]) - wcslen(joined) - 1);
            if (!GetFullPathNameW(joined, sizeof(full) / sizeof(full[0]), full, NULL))
                continue;
            if (dll_path[0] == 0)
                wcsncpy(dll_path, full, sizeof(dll_path) / sizeof(dll_path[0]) - 1);
            if (GetFileAttributesW(full) != INVALID_FILE_ATTRIBUTES) {
                wcsncpy(dll_path, full, sizeof(dll_path) / sizeof(dll_path[0]) - 1);
                break;
            }
        }
        dll_path[sizeof(dll_path) / sizeof(dll_path[0]) - 1] = 0;
        file_part = wcsrchr(dll_path, L'\\');
        file_part = file_part ? file_part + 1 : dll_path;
    }
    if (!file_part)
        file_part = dll_path;
    wcsncpy(dll_name, file_part, sizeof(dll_name) / sizeof(dll_name[0]) - 1);
    dll_name[sizeof(dll_name) / sizeof(dll_name[0]) - 1] = 0;

    if (!pid)
        pid = find_pid(DEFAULT_EXE);
    if (!pid) {
        printf("[FAIL] th09.exe not found. Start the game first, or pass --pid.\n");
        return 1;
    }
    printf("target     : th09.exe pid=%lu\n", pid);
    printf("dll        : %ls\n", dll_path);

    proc = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION
                       | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ,
                       FALSE, pid);
    if (!proc) {
        printf("[FAIL] OpenProcess failed (error %lu). Try running as administrator.\n",
               GetLastError());
        return 1;
    }

    base = find_module(pid, dll_name);

    if (action == ACT_ATTACH) {
        if (GetFileAttributesW(dll_path) == INVALID_FILE_ATTRIBUTES) {
            printf("[FAIL] DLL not found: %ls\n", dll_path);
            printf("       Build it first: native\\th09_tickhook\\build.bat\n");
            CloseHandle(proc);
            return 1;
        }
        base = remote_load_library(proc, pid, dll_path, dll_name);
        if (!base) {
            CloseHandle(proc);
            return 1;
        }
        export_name = "TickHookAttach";
        param = (void *)(UINT_PTR)(armed ? 1u : 0u);
    } else {
        if (!base) {
            printf("[FAIL] %ls is not loaded in the target (nothing to do).\n", dll_name);
            CloseHandle(proc);
            return 1;
        }
        printf("  module at %p\n", base);
        if (action == ACT_DETACH)      { export_name = "TickHookDetach"; param = NULL; }
        else if (action == ACT_STATUS) { export_name = "TickHookStatus"; param = NULL; }
        else if (action == ACT_ARM)    { export_name = "TickHookSetState";
                                         param = (void *)(UINT_PTR)1u; }
        else if (action == ACT_VPATCH_STATE) { export_name = "TickHookVpatchSkip";
                                         param = (void *)(UINT_PTR)0u; }
        else if (action == ACT_VPATCH_ON)    { export_name = "TickHookVpatchSkip";
                                         param = (void *)(UINT_PTR)1u; }
        else if (action == ACT_VPATCH_OFF)   { export_name = "TickHookVpatchSkip";
                                         param = (void *)(UINT_PTR)2u; }
        else                           { export_name = "TickHookSetState";
                                         param = (void *)(UINT_PTR)2u; }
    }

    fn = remote_proc_address(dll_path, base, export_name);
    if (!fn) {
        CloseHandle(proc);
        return 1;
    }
    printf("calling    : %s(%u) at %p\n", export_name, (unsigned)(UINT_PTR)param, fn);

    if (!call_remote(proc, fn, param, &code)) {
        CloseHandle(proc);
        return 1;
    }

    if (action == ACT_STATUS) {
        printf("status:\n");
        print_status(code);
        CloseHandle(proc);
        return 0;
    }

    if (action == ACT_VPATCH_ON || action == ACT_VPATCH_OFF
        || action == ACT_VPATCH_STATE) {
        printf("vpatch:\n");
        print_vpatch(code);
        CloseHandle(proc);
        return TICKBUS_VPATCH_ERROR(code) == 0 ? 0 : 1;
    }

    printf("result     : %lu (%s)\n", code, error_name(code));
    if (code != 0) {
        if (code == 2)
            printf("             Another tool may be hooking 0x0042C700, or a previous\n"
                   "             patch is still in place. Nothing was modified.\n");
        CloseHandle(proc);
        return 1;
    }

    fn = remote_proc_address(dll_path, base, "TickHookStatus");
    if (fn && call_remote(proc, fn, NULL, &code)) {
        printf("status:\n");
        print_status(code);
    }
    CloseHandle(proc);
    return 0;
}
