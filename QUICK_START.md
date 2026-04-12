# ⚡ Quick Start Guide - Deploy & Test

## 5-Minute Setup

### Step 1: Install Required Packages

```bash
cd c:\Users\WIN\Downloads\doanc#2\Csharpppppido\TourGuideApi

# Install dependencies
dotnet add package BCrypt.Net-Next
dotnet add package System.IdentityModel.Tokens.Jwt
dotnet add package Microsoft.IdentityModel.Tokens
```

### Step 1.5 (Recommended): Install FREE Tier-2 TTS (Edge-TTS)

This project supports a **free Tier-2 TTS provider** that does **not** require any API key: `EdgeTts`.

On Windows (PowerShell):

```powershell
winget install -e --id Python.Python.3.12 --scope user --silent --accept-package-agreements --accept-source-agreements

# Install edge-tts (creates edge-tts.exe under your Python Scripts folder)
$py = Join-Path $env:LOCALAPPDATA 'Programs\\Python\\Python312\\python.exe'
& $py -m pip install edge-tts
```

Note: `EdgeTts` needs **internet access** (it uses Microsoft Edge's online voices), but it does **not** require payment or API keys.

### Step 2: Configure appsettings.json

Edit `appsettings.json`:
```json
{
  "Jwt": {
    "Key": "MyS3cr3tK3y_Ch4ng3Th1s_ToS0m3th1ng_L0ng3rThan32Ch4rs",
    "ExpirationMinutes": 1440
  },
  "TextToSpeech": {
    "Provider": "EdgeTts"
  },
  "EdgeTts": {
    "ExecutablePath": "",
    "TimeoutSeconds": 90
  },
  "AzureSpeech": {
    "SubscriptionKey": "",
    "Region": "southeastasia"
  }
}
```

**Note**: Leave API keys empty for testing (will use TIER 4 fallback)

### Step 3: Create Database Migration

```bash
# Create migration
dotnet ef migrations add AddLocalizationAndRbac

# Apply to database
dotnet ef database update
```

### Step 4: Run Backend

```bash
dotnet run
```

**Backend is running at**: http://localhost:5214  
**Swagger API docs**: http://localhost:5214/swagger

### Step 5: Test API

#### Login as Admin
```bash
curl -X POST http://localhost:5214/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@tourguidequan4.com",
    "password": "AdminPassword123!"
  }'
```

Expected response:
```json
{
  "token": "eyJhbGciOiJIUzI1Ni...",
  "user": {
    "id": 1,
    "email": "admin@tourguidequan4.com",
    "fullName": "Administrator",
    "role": "Admin"
  }
}
```

#### Get Locations
```bash
curl http://localhost:5214/api/locations
```

#### Create a Localization
```bash
curl -X POST http://localhost:5214/api/localizations \
  -H "Content-Type: application/json" \
  -d '{
    "locationId": 1,
    "languageCode": "en-US",
    "localizedName": "Famous Pho",
    "localizedDescription": "Traditional Vietnamese noodle soup..."
  }'
```

### Step 6: Mobile App (MAUI) - Android Emulator

```bash
cd ..\TouristGuideApp

# Build and deploy to Android Emulator
dotnet maui build -t debug -f net8.0-android

# Or use Visual Studio UI to run
```

**Mobile will connect to**: http://10.0.2.2:5214/ (Android emulator special IP)

---

## 🧪 Test Workflow (Complete Flow)

### Scenario: Select Language → Get Localized Content → Play Audio

**1. Initialize App**
```csharp
// MauiProgram.cs services are auto-registered
// App launches and calls MainPage.xaml.cs
```

**2. Trigger Sync**
```csharp
// In MapPage.xaml.cs constructor or OnNavigated
var syncService = ServiceHelper.GetService<ISyncService>();
var result = await syncService.SyncAllAsync();
Debug.WriteLine($"Sync result: {result.SyncedCount} POIs synced");
```

**3. Change Language**
```csharp
// In SettingsPage.xaml.cs
private async Task OnLanguageSelected(string languageCode)
{
    await _geofenceService.SetLanguageAsync(languageCode);
    Debug.WriteLine($"Language changed to {languageCode}");
}
```

**4. Simulate GPS Proximity**
```csharp
// In MapPage.xaml.cs
private async Task SimulateLocationUpdate()
{
    // Create a location near a POI (e.g., 10m away)
    var userLocation = new Location
    {
        Latitude = 10.7769,   // POI latitude
        Longitude = 106.6970   // POI longitude
    };
    
    // Check proximity
    await _geofenceService.CheckProximity(userLocation);
    
    // If within 30m → audio plays automatically
    // Audio plays in selected language
}
```

**5. Verify Audio Playback**
```
Debug output should show:
- Language changed to: en-US
- Nearby POI detected: Pho Hoa (10.5m away)
- Audio generated for: Pho Hoa (English)
- IsPlaying: true → ... → false
```

---

## 🔍 Verification Checklist

- [ ] Backend compiles without errors
- [ ] Database created (tourguide.db)
- [ ] Admin user inserted automatically
- [ ] Swagger UI accessible at /swagger
- [ ] Login endpoint returns valid JWT token
- [ ] Mobile app runs without errors
- [ ] Sync completes successfully
- [ ] Language selection changes geofence service language
- [ ] Simulated proximity triggers audio
- [ ] Audio plays in selected language

---

## 🐛 Troubleshooting

### Backend won't start
```bash
# Check if port 5214 is in use
netstat -ano | findstr :5214

# Delete old database
del tourguide.db
dotnet ef database update
```

### Mobile can't reach backend
```
Error: Connection refused to http://10.0.2.2:5214

Solution:
1. Ensure backend is running
2. Check Android emulator settings: Settings → Network → Check proxy
3. Or use physical device with your local IP instead
```

### Compilation errors
```bash
# Clean and rebuild
dotnet clean
dotnet build

# Or in Visual Studio: Build → Clean Solution → Rebuild
```

### Audio not playing
```csharp
// Check debug output
Debug.WriteLine($"Audio status: {_audioService.IsPlaying}");

// Ensure TTS voice code is valid
// Fallback to TIER 4 (device TTS) if API keys missing
```

---

## 📊 Project Structure After Setup

```
Csharpppppido/
├── TourGuideApi/
│   ├── Controllers/
│   │   ├── LocationsController.cs ✅
│   │   ├── LocalizationsController.cs ✅ NEW
│   │   └── AuthController.cs ✅ NEW
│   ├── Models/
│   │   ├── Location.cs ✅
│   │   ├── Localization.cs ✅ NEW
│   │   ├── User.cs ✅ NEW
│   │   └── POI.cs
│   ├── Services/
│   │   ├── ITextToSpeechService.cs ✅ ENHANCED (includes AzureTextToSpeechService)
│   │   ├── EdgeTtsTextToSpeechService.cs ✅ NEW
│   │   └── GoogleTextToSpeechService.cs ✅
│   ├── Data/
│   │   └── AppDbContext.cs ✅ UPDATED
│   ├── tourguide.db ⚙️ (Created by EF Core)
│   └── Program.cs ✅ UPDATED
│
├── TouristGuideApp/
│   ├── Services/
│   │   ├── AudioService.cs ✅ ENHANCED
│   │   ├── GeofenceService.cs ✅ ENHANCED
│   │   ├── ApiService.cs ✅ ENHANCED
│   │   ├── SyncService.cs ✅ NEW
│   │   └── DatabaseService.cs
│   ├── Models/
│   │   ├── POI.cs
│   │   ├── Location.cs
│   │   └── Localization.cs ✅ NEW
│   ├── Views/
│   │   ├── MapPage.xaml
│   │   ├── SettingsPage.xaml
│   │   └── MainPage.xaml
│   └── MauiProgram.cs ✅ UPDATED
│
├── IMPLEMENTATION_GUIDE.md ✅ NEW
├── COMPLETION_SUMMARY.md ✅ NEW
├── DATABASE_SEED.sql ✅ NEW
└── QUICK_START.md ← You are here
```

---

## 🚀 Next Phase: AI Advisor

When ready to implement Gemini AI:

1. Install Gemini SDK
2. Add to Program.cs:
```csharp
builder.Services.AddScoped<IAiAdvisorService, GeminiAiAdvisorService>();
```

3. Implement:
```csharp
// Services/IAiAdvisorService.cs
public class GeminiAiAdvisorService : IAiAdvisorService
{
    public async Task<AiRecommendation> GetRecommendationAsync(
        string userPreference, 
        string languageCode)
    {
        // Call Gemini API with prompt
        // Return food recommendation
    }
}
```

4. Add controller endpoint:
```csharp
[HttpPost]
public async Task<AiRecommendation> GetRecommendation(
    [FromBody] RecommendationRequest request)
{
    return await _aiAdvisor.GetRecommendationAsync(
        request.Preference, 
        request.LanguageCode);
}
```

---

## ✅ You're Ready!

Your culinary tourism system now has:
- ✅ Multi-language support (5 languages)
- ✅ 4-tier hybrid audio fallback
- ✅ Admin authentication with JWT
- ✅ Offline-first data sync
- ✅ Geofencing with proximity detection
- ✅ Database with RBAC

**Start here**: Run `dotnet run` in TourGuideApi folder! 🎯
