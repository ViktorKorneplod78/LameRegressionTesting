@echo off
rem ============================================================
rem  Regression test: LAME 3.100 (old) vs LAME 4.0 (new)
rem  10 scenarios = 5 encoding settings x 2 input files.
rem  Usage: run_tests.bat [/noopen]
rem ============================================================
setlocal
cd /d "%~dp0"

set OLD=lame\3.100\lame.exe
set NEW=lame\4.0\lame.exe
set ITERATIONS=5
set THRESHOLD=10
set SPEECH=input\track1_speech.wav
set MUSIC=input\track2_music.wav

for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyy-MM-dd_HH-mm-ss"') do set TS=%%i
set RUN_DIR=reports\%TS%

set APP=build\LameRegression.exe

rem Build from sources if they are present; the release package ships a prebuilt exe instead
if exist src\LameRegression.csproj (
    echo Building LameRegression...
    dotnet build src -c Release -o build --nologo -v q
    if errorlevel 1 (
        echo Build failed. Is .NET 8 SDK installed?
        exit /b 2
    )
)
if not exist "%APP%" (
    echo %APP% not found.
    exit /b 2
)

for %%f in (%SPEECH% %MUSIC%) do if not exist %%f (
    echo Input file %%f not found. See input\README.md.
    exit /b 2
)

set FAILED=0

rem        name                    input      LAME options
call :scenario S01_speech_cbr128     %SPEECH%   "-b 128"
call :scenario S02_music_cbr128      %MUSIC%    "-b 128"
call :scenario S03_speech_cbr320_q0  %SPEECH%   "-b 320 -q 0"
call :scenario S04_music_cbr320_q0   %MUSIC%    "-b 320 -q 0"
call :scenario S05_speech_vbr_v2     %SPEECH%   "-V 2"
call :scenario S06_music_vbr_v2      %MUSIC%    "-V 2"
call :scenario S07_speech_abr160     %SPEECH%   "--abr 160"
call :scenario S08_music_abr160      %MUSIC%    "--abr 160"
call :scenario S09_speech_mono64     %SPEECH%   "-b 64 -m m --resample 22.05"
call :scenario S10_music_mono64      %MUSIC%    "-b 64 -m m --resample 22.05"

"%APP%" report --run-dir "%RUN_DIR%" --threshold %THRESHOLD%
if errorlevel 1 set FAILED=1

echo.
echo Report: %RUN_DIR%\report.html
if /i not "%~1"=="/noopen" start "" "%RUN_DIR%\report.html"
exit /b %FAILED%

:scenario
"%APP%" run --run-dir "%RUN_DIR%" --name %1 --input %2 --options %3 --old "%OLD%" --new "%NEW%" --iterations %ITERATIONS%
if errorlevel 1 set FAILED=1
exit /b 0
