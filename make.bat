@echo off

dotnet build memoryOptimizer.sln /t:Rebuild /p:DebugType=None /p:Configuration=Release

if errorlevel 1 goto error

goto exit
:error
pause
:exit
