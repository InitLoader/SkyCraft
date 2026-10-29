@echo off
setlocal
if not defined UE4SS_SOURCE (echo Set UE4SS_SOURCE to the pinned source checkout.& exit /b 1)
if not defined FMT_INCLUDE (echo Set FMT_INCLUDE to fmt 11.2.0 include.& exit /b 1)
where cl.exe >nul 2>&1
if errorlevel 1 (echo Run in an x64 Visual Studio developer command prompt.& exit /b 1)
cd /d "%~dp0"
lib /nologo /def:UE4SS.def /machine:x64 /out:UE4SS.lib
if errorlevel 1 exit /b 1
cl /nologo /std:c++latest /utf-8 /EHsc /MD /O2 /LD /W4 /I "%UE4SS_SOURCE%\deps\first\LuaMadeSimple\include" /I "%UE4SS_SOURCE%\deps\first\LuaRaw\include" /I "%FMT_INCLUDE%" PBZNative.cpp UE4SS.lib /link /out:PBZNative.dll
exit /b %errorlevel%
