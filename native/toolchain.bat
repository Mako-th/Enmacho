@echo off
rem ---------------------------------------------------------------------------
rem TH09 tick hook - 32bit toolchain locator
rem
rem ASCII only. cmd.exe parses .bat in the OEM code page (CP932 here), so
rem UTF-8 Japanese text in a .bat corrupts the parser. See native\README.md
rem for the Japanese explanation.
rem
rem th09.exe is PE32 (i686), so the injected DLL and the injector must be
rem built as 32bit. The existing C:\toolkit\w64devkit is x86_64 with
rem --disable-multilib, so -m32 does not work there.
rem
rem Setup: https://github.com/skeeto/w64devkit/releases
rem        extract w64devkit-x86-<ver>.7z.exe into C:\toolkit\w64devkit-i686
rem
rem Override the location with the TH09_I686_BIN environment variable.
rem ---------------------------------------------------------------------------

if not "%TH09_I686_BIN%"=="" goto have_dir
set "TH09_I686_BIN=C:\toolkit\w64devkit-i686\bin"
:have_dir

set "CC32=%TH09_I686_BIN%\gcc.exe"
set "OBJDUMP32=%TH09_I686_BIN%\objdump.exe"

if exist "%CC32%" goto have_cc
echo [ERROR] 32bit gcc not found: %CC32%
echo.
echo   1. Download w64devkit-x86-^<ver^>.7z.exe from
echo      https://github.com/skeeto/w64devkit/releases
echo   2. Extract it to C:\toolkit\_x86tmp
echo      Do NOT extract into C:\toolkit directly: it would overwrite the
echo      existing x86_64 kit at C:\toolkit\w64devkit.
echo   3. Move C:\toolkit\_x86tmp\w64devkit to C:\toolkit\w64devkit-i686
echo.
echo   If you installed it elsewhere, set TH09_I686_BIN to its bin directory.
exit /b 1
:have_cc

rem Make sure this really is the i686 toolchain and not the x86_64 one.
set "CC32_TARGET="
for /f "delims=" %%T in ('""%CC32%" -dumpmachine"') do set "CC32_TARGET=%%T"
if "%CC32_TARGET%"=="i686-w64-mingw32" goto target_ok
echo [ERROR] toolchain target is "%CC32_TARGET%", expected "i686-w64-mingw32"
echo         This looks like the x86_64 kit: %CC32%
exit /b 1
:target_ok

rem gcc finds as.exe / ld.exe through PATH. If the x86_64 kit's bin comes first,
rem the i686 gcc assembles x86-64 objects and the link fails with
rem "i386:x86-64 architecture ... is incompatible with i386 output".
rem Put the i686 bin first, and point gcc at it explicitly with -B.
rem The caller uses setlocal, so this PATH change is scoped to the build.
set "PATH=%TH09_I686_BIN%;%PATH%"
set "CC32_B=-B%TH09_I686_BIN%"

exit /b 0
