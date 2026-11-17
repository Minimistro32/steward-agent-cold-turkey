@echo off
setlocal
cd /d "%~dp0"
Steward.ColdTurkeyAgent.exe menu
set "stewardExit=%errorlevel%"
if not "%stewardExit%"=="0" echo Steward exited with an error. The message is shown above.
echo.
pause
exit /b %stewardExit%
