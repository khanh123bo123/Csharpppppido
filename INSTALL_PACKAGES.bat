@echo off
REM Windows batch script to install required NuGet packages

echo Installing required NuGet packages for TourGuideApi...
echo.

cd /d "c:\Users\WIN\Downloads\doanc#2\Csharpppppido\TourGuideApi" || exit /b 1

echo Installing JWT and Authentication packages...
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.0
if %ERRORLEVEL% NEQ 0 (
    echo Error installing Microsoft.AspNetCore.Authentication.JwtBearer
    exit /b 1
)

dotnet add package System.IdentityModel.Tokens.Jwt --version 7.0.0
if %ERRORLEVEL% NEQ 0 (
    echo Error installing System.IdentityModel.Tokens.Jwt
    exit /b 1
)

dotnet add package Microsoft.IdentityModel.Tokens --version 7.0.0
if %ERRORLEVEL% NEQ 0 (
    echo Error installing Microsoft.IdentityModel.Tokens
    exit /b 1
)

dotnet add package BCrypt.Net-Next --version 4.0.3
if %ERRORLEVEL% NEQ 0 (
    echo Error installing BCrypt.Net-Next
    exit /b 1
)

echo.
echo ============================================
echo Packages installed successfully!
echo ============================================
echo.
echo Next steps:
echo 1. Edit appsettings.json with your JWT key and TTS settings
echo 2. Run: dotnet ef migrations add AddLocalizationAndRbac
echo 3. Run: dotnet ef database update
echo 4. Run: dotnet run
echo.
pause
