@echo off
setlocal
cd /d "%~dp0src\ManualChatbot"
set ASPNETCORE_ENVIRONMENT=Development
echo.
echo CLEAN CHAT UI - release illustrated-r9
echo Open: http://localhost:5091
echo This starts the project in this extracted folder.
echo.
dotnet run --project "%~dp0src\ManualChatbot\ManualChatbot.csproj" --no-launch-profile --urls http://localhost:5091
pause
endlocal
