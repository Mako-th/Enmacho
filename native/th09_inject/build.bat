@echo off
rem ---------------------------------------------------------------------------
rem TH09 injector - build (public source release). ASCII only.
rem ---------------------------------------------------------------------------
setlocal
cd /d "%~dp0"

call ..\toolchain.bat
if errorlevel 1 exit /b 1

if not exist build mkdir build

set CFLAGS=-O2 -Wall -Wextra -std=c99 -fno-ident -D_WIN32_WINNT=0x0601
set LDFLAGS=-static-libgcc -lkernel32
set TARGET=build\th09_inject.exe

echo [1/3] compiling with %CC32_TARGET% ...
"%CC32%" %CC32_B% %CFLAGS% inject.c -o "%TARGET%" %LDFLAGS%
if errorlevel 1 (
    echo [FAIL] compile
    exit /b 1
)

echo [2/3] verifying PE32 / i386 ...
"%OBJDUMP32%" -f "%TARGET%" > build\fileinfo.txt
findstr /C:"pei-i386" build\fileinfo.txt >nul
if errorlevel 1 (
    echo [FAIL] not a PE32/i386 image.
    type build\fileinfo.txt
    exit /b 1
)

echo [3/3] running --help (no game involved) ...
"%TARGET%" >nul 2>&1
if not errorlevel 2 (
    echo [FAIL] the injector should print usage and exit with 2 when given no action
    exit /b 1
)

echo.
echo [OK] %TARGET%
exit /b 0
