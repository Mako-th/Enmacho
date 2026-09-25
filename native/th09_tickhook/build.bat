@echo off
rem ---------------------------------------------------------------------------
rem TH09 tick hook DLL - build (public source release).
rem ASCII only: cmd.exe reads .bat files in the OEM code page.
rem This is a reduced form of the source build.bat: it builds the DLL and
rem checks its shape (PE32/i386, no SSE/MMX, no mem* calls, undecorated
rem exports). It has no dependency on any scripting runtime, and does not
rem run the smoke test or the test tools that are not part of the shipped
rem source.
rem ---------------------------------------------------------------------------
setlocal
cd /d "%~dp0"

call ..\toolchain.bat
if errorlevel 1 exit /b 1

if not exist build mkdir build

set HOOKFLAGS=-O2 -Wall -Wextra -std=c99 -mno-sse -mno-mmx -mfpmath=387 -fno-stack-protector -fno-asynchronous-unwind-tables -fno-ident -D_WIN32_WINNT=0x0601 -mincoming-stack-boundary=2 -mpreferred-stack-boundary=2
set LDFLAGS=-shared -static-libgcc -Wl,--kill-at -Wl,--nxcompat -lkernel32
set TARGET=build\th09_tickhook.dll

echo [1/5] compiling with %CC32_TARGET% ...
"%CC32%" %CC32_B% %HOOKFLAGS% -c capture.c   -o build\capture.o
if errorlevel 1 goto fail
"%CC32%" %CC32_B% %HOOKFLAGS% -c detour.c    -o build\detour.o
if errorlevel 1 goto fail
"%CC32%" %CC32_B% %HOOKFLAGS% -c hook_main.c -o build\hook_main.o
if errorlevel 1 goto fail
"%CC32%" %CC32_B% build\capture.o build\detour.o build\hook_main.o -o "%TARGET%" %LDFLAGS%
if errorlevel 1 goto fail

echo [2/5] verifying PE32 / i386 ...
"%OBJDUMP32%" -f "%TARGET%" > build\fileinfo.txt
findstr /C:"pei-i386" build\fileinfo.txt >nul
if errorlevel 1 (
    echo [FAIL] not a PE32/i386 image.
    type build\fileinfo.txt
    exit /b 1
)

echo [3/5] verifying no SSE/MMX instructions ...
"%OBJDUMP32%" -d "%TARGET%" > build\disasm.txt
findstr /C:"xmm" /C:"movdq" /C:"punpck" build\disasm.txt >nul
if not errorlevel 1 (
    echo [FAIL] SSE/MMX instruction found - see build\disasm.txt
    exit /b 1
)

echo [4/5] verifying capture.o calls no memcpy/memset/memmove ...
"%OBJDUMP32%" -t build\capture.o > build\capture_syms.txt
findstr /C:"*UND*" build\capture_syms.txt > build\capture_und.txt
findstr /I /C:"memcpy" /C:"memset" /C:"memmove" build\capture_und.txt >nul
if not errorlevel 1 (
    echo [FAIL] capture.o references a mem* routine - see build\capture_und.txt
    exit /b 1
)

echo [5/5] verifying undecorated exports ...
"%OBJDUMP32%" -p "%TARGET%" > build\exports.txt
for %%E in (TickHookAttach TickHookDetach TickHookStatus TickHookSetState TickHookCoordOpen TickHookVpatchSkip) do (
    findstr /C:"%%E" build\exports.txt >nul
    if errorlevel 1 (
        echo [FAIL] export %%E not found - see build\exports.txt
        exit /b 1
    )
)
findstr /C:"TickHookAttach@" build\exports.txt >nul
if not errorlevel 1 (
    echo [FAIL] exports are stdcall-decorated. -Wl,--kill-at is missing.
    exit /b 1
)

echo.
echo [OK] %TARGET%
exit /b 0

:fail
echo [FAIL] compile/link the hook DLL
exit /b 1
