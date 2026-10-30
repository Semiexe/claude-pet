@echo off
rem Beendet den Waechter und das Pet. (Im Menue des Pets geht es auch: "Beenden".)
powershell -NoProfile -Command "$p = @(Get-CimInstance Win32_Process -Filter \"Name='ClaudePet.exe'\"); $p | Where-Object { $_.CommandLine -notmatch '--run' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }; $p | Where-Object { $_.CommandLine -match '--run' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }"
echo Das Pet wurde beendet.
