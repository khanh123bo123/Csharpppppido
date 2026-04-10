#!/bin/bash
# Installation script for required NuGet packages

echo "Installing required NuGet packages for TourGuideApi..."

cd c:\\Users\\WIN\\Downloads\\doanc#2\\Csharpppppido\\TourGuideApi

# Install authentication packages
echo "Installing JWT and Authentication packages..."
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.0
dotnet add package System.IdentityModel.Tokens.Jwt --version 7.0.0
dotnet add package Microsoft.IdentityModel.Tokens --version 7.0.0
dotnet add package BCrypt.Net-Next --version 4.0.3

echo "Packages installed successfully!"
echo ""
echo "Next steps:"
echo "1. Edit appsettings.json with your JWT key and TTS settings"
echo "2. Run: dotnet ef migrations add AddLocalizationAndRbac"
echo "3. Run: dotnet ef database update"
echo "4. Run: dotnet run"
