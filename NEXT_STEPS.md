# 🚀 Next Steps - Development and Deployment Guide

## Phase 1: Environment Setup (15 minutes)

### Step 1.1: Install Required NuGet Packages
```powershell
cd "c:\Users\WIN\Downloads\doanc#2\Csharpppppido\TourGuideApi"

# Run all four commands
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.0
dotnet add package System.IdentityModel.Tokens.Jwt --version 7.0.0
dotnet add package Microsoft.IdentityModel.Tokens --version 7.0.0
dotnet add package BCrypt.Net-Next --version 4.0.3

# Verify
dotnet restore
```

### Step 1.2: Configure appsettings.json
**File**: `TourGuideApi/appsettings.json`

Replace the JWT and TTS sections:
```json
{
  "Jwt": {
    "Key": "MyS3cr3tK3y_Ch4ng3Th1s_ToS0m3th1ng_L0ng3rThan32Chars!!!",
    "Issuer": "http://localhost:5214",
    "Audience": "TourGuideApi"
  },
  "AzureSpeech": {
    "SubscriptionKey": "YOUR_AZURE_KEY_HERE",
    "Region": "eastasia"
  },
  "GoogleCloud": {
    "TextToSpeechApiKey": "YOUR_GOOGLE_KEY_HERE"
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000", "http://10.0.2.2:5214"]
  }
}
```

⚠️ **IMPORTANT**: Change `Jwt:Key` to a unique string longer than 32 characters in production!

---

## Phase 2: Backend Development (30 minutes)

### Step 2.1: Create Database Schema
```bash
cd TourGuideApi

# Add migration for new features
dotnet ef migrations add AddLocalizationAndRbac

# Apply migration to create database
dotnet ef database update

# Verify database created
ls -la tourguide.db  # Should see ~100KB file
```

### Step 2.2: Clean Build
```bash
dotnet clean
dotnet build

# Expected output:
# Build succeeded. 0 Warning(s)
```

### Step 2.3: Run Backend Server
```bash
dotnet run

# Expected output:
# info: Microsoft.Hosting.Lifetime[14]
#      Now listening on: http://localhost:5214
# info: Microsoft.Hosting.Lifetime[0]
#      Application started. Press Ctrl+C to exit.
```

### Step 2.4: Test Backend APIs
Open browser: **http://localhost:5214/swagger**

You should see Swagger UI with these endpoints:
- `POST /api/auth/login`
- `POST /api/auth/register`
- `GET /api/auth/verify`
- `GET /api/locations`
- `POST /api/localizations`
- `GET /api/localizations/by-location/{id}`
- `POST /api/localizations/{id}/generate-audio`

Test with default admin:
- Email: `admin@tourguide.com`
- Password: `Admin@123`

---

## Phase 3: Mobile App Development (45 minutes)

### Step 3.1: Update API Base URL
**File**: `TouristGuideApp/Services/ApiService.cs`

For Android Emulator testing:
```csharp
private const string BaseAddress = "http://10.0.2.2:5214/api/";  // ← Use this for Emulator
// For physical device or localhost:
// private const string BaseAddress = "http://192.168.1.X:5214/api/";
```

For physical Android device (replace X with your computer's IP):
```csharp
private const string BaseAddress = "http://192.168.1.100:5214/api/";  // ← Example IP
```

### Step 3.2: Build Mobile App
```bash
cd TouristGuideApp

# Build for Android
dotnet maui build -t debug -f net8.0-android

# Build for iOS (macOS only)
dotnet maui build -t debug -f net8.0-ios

# Build for Windows
dotnet maui build -t debug -f net8.0-windows
```

### Step 3.3: Deploy to Emulator
```bash
# Start Android Emulator first
# Then run:
dotnet maui run -f net8.0-android

# Wait for app to appear on emulator (~30 seconds)
```

### Step 3.4: Test App Features
1. **Launch App**: Should show map page
2. **Test Offline Sync**: 
   - Go offline (disable WiFi/data)
   - Navigate to a location
   - Trigger geofence (if in test range)
   - Audio should play from cache
3. **Test Multi-Language**:
   - Go to Settings page
   - Change language
   - Audio narration should change
4. **Test Authentication**:
   - Settings page should show login option
   - Admin can create new POIs

---

## Phase 4: Integration Testing (60 minutes)

### Test 4.1: Complete Flow (Backend + Mobile)
```
1. Backend running on http://localhost:5214
2. Mobile app running on Android Emulator
3. Navigate to a location
4. Geofence triggers at 30m radius
5. Audio plays in selected language
6. Works both online and offline
```

### Test 4.2: API Load Testing
```bash
# In a new terminal, run 100 requests to verify performance
for i in {1..100}; do 
  curl -s http://localhost:5214/api/locations > /dev/null
  echo "$i requests sent"
done
```

### Test 4.3: Audio Generation Testing
```bash
# Test 4-tier audio system
curl -X POST http://localhost:5214/api/localizations/1/generate-audio \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json"

# Response should show:
# "audioGenerationStatus": "Cached" or "Generated" or "EdgeTts"
```

---

## Phase 5: Deployment Preparation (30 minutes)

### Step 5.1: Create Production Configuration
**File**: `TourGuideApi/appsettings.Production.json`
```json
{
  "Jwt": {
    "Key": "USE_A_VERY_LONG_RANDOM_STRING_HERE_MINIMUM_64_CHARS",
    "Issuer": "https://yourdomain.com",
    "Audience": "TourGuideApi"
  },
  "AzureSpeech": {
    "SubscriptionKey": "PROD_AZURE_KEY",
    "Region": "eastasia"
  },
  "Cors": {
    "AllowedOrigins": ["https://yourdomain.com"]
  }
}
```

### Step 5.2: Build Release Binary
```bash
cd TourGuideApi
dotnet publish -c Release -o ./published

# Output directory: ./published/TourGuideApi.dll
```

### Step 5.3: Create Docker Image (Optional)
**File**: `TourGuideApi/Dockerfile`
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY published/ .
EXPOSE 5214
ENTRYPOINT ["dotnet", "TourGuideApi.dll"]
```

Build and run:
```bash
docker build -t tourguide-api:1.0 .
docker run -p 5214:5214 tourguide-api:1.0
```

### Step 5.4: Create Deployment Checklist
- [ ] All tests passing
- [ ] Database backup created
- [ ] JWT key updated to production value
- [ ] API keys configured (Azure, Google)
- [ ] CORS origins updated
- [ ] SSL certificate configured
- [ ] Load balancer configured
- [ ] Logging enabled
- [ ] Monitoring configured
- [ ] Rollback plan documented

---

## Phase 6: Continuous Development (Ongoing)

### Remaining Features (Not Yet Implemented)
1. **Gemini AI Advisor**
   - Template: `IAiAdvisorService.cs`
   - Endpoint: `POST /api/advisor/recommend`
   - Status: Ready for integration

2. **Full Text Search**
   - For finding POIs by keyword/cuisine type
   - Status: Database ready, controller method pending

3. **Rating and Reviews**
   - User ratings for POIs and restaurants
   - Status: Model exists, API pending

4. **Photo Gallery**
   - Upload and manage POI photos
   - Status: Scaffolding ready, image processing pending

5. **Reservation System**
   - Book tables at restaurants
   - Status: Domain model needs creation

### Development Workflow
```bash
# 1. Create new branch
git checkout -b feature/new-feature

# 2. Implement feature
# 3. Write tests

# 4. Commit
git add .
git commit -m "feat: add new feature"

# 5. Push
git push origin feature/new-feature

# 6. Create PR and merge to main
```

---

## 📋 Quick Command Reference

| Task | Command |
|------|---------|
| Install packages | `dotnet add package <name>` |
| Create migration | `dotnet ef migrations add <name>` |
| Update database | `dotnet ef database update` |
| Build solution | `dotnet build` |
| Run backend | `dotnet run` |
| Build mobile | `dotnet maui build -f net8.0-android` |
| Run mobile | `dotnet maui run -f net8.0-android` |
| Publish release | `dotnet publish -c Release` |
| Clear cache | `dotnet clean` |
| View Swagger | http://localhost:5214/swagger |

---

## 🐛 Troubleshooting

### Build fails with "package not found"
```bash
# Clean and restore
dotnet clean
dotnet restore
dotnet build
```

### Database migration fails
```bash
# Check current migration
dotnet ef migrations list

# Remove last migration if needed
dotnet ef migrations remove

# Reapply
dotnet ef migrations add <name>
dotnet ef database update
```

### Mobile app can't connect to backend
- Check if backend is running: `http://localhost:5214/swagger`
- For emulator: Use `http://10.0.2.2:5214` (not localhost)
- For physical device: Use your computer's IP address
- Check firewall settings

### Audio not playing
- Verify audio service is registered in `MauiProgram.cs`
- Check language code is supported (vi-VN, en-US, zh-CN, ja-JP, ko-KR)
- Verify TTS service has API keys configured

### Authentication fails
- Check JWT key is 32+ characters
- Verify token expiration time
- Check CORS configuration allows your domain

---

## 🎉 Success Criteria

Your project is ready when:

✅ Backend compiles without errors  
✅ Backend runs and serves Swagger docs  
✅ All API endpoints respond  
✅ Mobile app builds without errors  
✅ Mobile app connects to backend  
✅ Audio narration works in all 5 languages  
✅ Offline mode syncs when back online  
✅ Geofencing triggers narration at 30m  
✅ Admin login works with JWT token  
✅ All tests pass  

---

## 📞 Support Resources

- [ASP.NET Core Docs](https://learn.microsoft.com/en-us/aspnet/core/)
- [MAUI Docs](https://learn.microsoft.com/en-us/dotnet/maui/)
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- [Azure Cognitive Services](https://learn.microsoft.com/en-us/azure/cognitive-services/)
- [Google Cloud Text-to-Speech](https://cloud.google.com/text-to-speech)

---

**You've got this! 💪 Start with Phase 1 and work through systematically.**
