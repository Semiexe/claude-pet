@echo off
rem Startet die .exe, wenn sie gebaut wurde (Claude-Pet-bauen.ps1), sonst das PowerShell-Skript.
if exist "%~dp0ClaudePet.exe" (
  start "" "%~dp0ClaudePet.exe"
) else (
  start "" powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0ClaudePet.ps1"
)
