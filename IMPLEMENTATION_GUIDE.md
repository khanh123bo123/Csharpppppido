# Culinary Tourism System - District 4 Development Guide

## 🎯 Project Overview

This is a **PWA Full-Stack Offline-First** culinary tourism system for District 4 that combines:
- **Backend**: ASP.NET Core + SQLite + 4-Tier Hybrid Audio System
- **Mobile**: MAUI (C#) with offline-first database sync
- **Architecture**: Geofencing + Multi-language support + AI integration ready

---

## 📦 What's Been Implemented

### 1. **Localization Infrastructure** ✅
- **Backend Models**: `Localization` entity supporting 5 languages (vi-VN, en-US, zh-CN, ja-JP, ko-KR)
- **Database**: New `Localizations` table with unique index on (LocationId, LanguageCode)
- **API Endpoints**: Full CRUD operations for localized content

#### Supported Languages
```
vi-VN - Tiếng Việt (Vietnamese)
en-US - English
zh-CN - 简体中文 (Chinese Simplified)
ja-JP - 日本語 (Japanese)
ko-KR - 한국어 (Korean)
```

### 2. **Hybrid Audio System** ✅
The system automatically falls back through audio tiers to ensure content is always available:

```
TIER 1: Cached MP3 (Local device storage)
        ↓ (if not cached)
TIER 2: Edge-TTS CLI (free, no API key; requires internet)
  ↓ (if unavailable)
TIER 3: Device TTS (MAUI TextToSpeech.Default)
```

**Implementation**:
- `EdgeTtsTextToSpeechService`: Generates MP3 via the open-source `edge-tts` CLI
- Implements `ITextToSpeechService`

**Voice Setup**:
```csharp
// Default voices for each language
"vi-VN" => "vi-VN-HoaiMyNeural"
"en-US" => "en-US-AriaNeural"
"zh-CN" => "zh-CN-XiaoxiaoNeural"
"ja-JP" => "ja-JP-NanamiNeural"
"ko-KR" => "ko-KR-SunHiNeural"
```

### 3. **Admin RBAC System** ✅
Complete role-based access control with JWT authentication:

**Roles**:
- `Admin`: Full access, can manage users and content
- `Editor`: Can create/edit POIs and manage localizations
- `Viewer`: Read-only access

**Features**:
- JWT token generation with 24-hour expiration (configurable)
- Password hashing using BCrypt
- Token verification and refresh logic
- User management endpoints

### 4. **Offline-First Sync Service** ✅
Mobile app automatically syncs POIs and localizations when online:

```csharp
// Full sync from API to local SQLite
await syncService.SyncAllAsync();

// Check connectivity and retry automatically
await syncService.IsOnlineAsync();
```

**Sync Result**:
```
- SyncedCount: Number of POIs synced
- IsSuccess: Operation result
- Message: Status details
- SyncTime: Timestamp of sync
```

### 5. **Geofencing with Multi-Language** ✅
Enhanced geofencing service that:
- Detects user proximity to POIs (default 30m radius)
- Plays language-specific narration automatically
- Enforces 5-minute cooldown between audio plays per POI
- Supports on-demand audio playback with `ignoreCooldown` flag

---

## 🚀 Getting Started

### Backend Setup

#### 1. Install Required NuGet Packages
```bash
dotnet add package BCrypt.Net-Next
dotnet add package System.IdentityModel.Tokens.Jwt
dotnet add package Microsoft.IdentityModel.Tokens
```

#### 2. Configure appsettings.json
```json
{
  "Jwt": {
    "Key": "your-super-secret-jwt-key-at-least-32-characters-long",
    "Issuer": "TourGuideApi",
    "Audience": "TourGuideApp",
    "ExpirationMinutes": 1440
  },
  "TextToSpeech": {
    "Provider": "EdgeTts"
  },
  "EdgeTts": {
    "ExecutablePath": "",
    "TimeoutSeconds": 90,
    "SpeechRate": 0.25
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "qwen2.5:3b"
  }
}
```

#### 3. Create Database Migration
```bash
cd TourGuideApi
dotnet ef migrations add AddLocalizationAndRbac
dotnet ef database update
```

#### 4. Run Backend
```bash
dotnet run
```

Swagger API docs available at: `http://localhost:5214/swagger`

### Mobile App Setup

#### 1. Register SyncService in MauiProgram
```csharp
builder.Services.AddSingleton<ISyncService, SyncService>();
```

#### 2. Initialize Sync on App Launch
```csharp
// In App.xaml.cs or MainPage.xaml.cs
var syncService = ServiceHelper.GetService<ISyncService>();
var result = await syncService.SyncAllAsync();
```

---

## 📋 API Endpoints

### Localizations

#### Get all localizations for a location
```
GET /api/localizations/by-location/{locationId}
Response: LocalizationDto[]
```

#### Get localization for specific language
```
GET /api/localizations/{locationId}/{languageCode}
Response: LocalizationDto

Example:
GET /api/localizations/123/vi-VN
```

#### Create/Update localization
```
POST /api/localizations
Body: {
  "locationId": 1,
  "languageCode": "en-US",
  "localizedName": "Pho Hoa",
  "localizedDescription": "Famous pho restaurant...",
  "ttsVoiceCode": "en-US-AriaNeural"
}
Response: LocalizationDto
```

#### Download cached audio (TIER 1)
```
GET /api/localizations/{localizationId}/audio
Response: MP3 audio file
```

#### Manually generate audio
```
POST /api/localizations/generate-audio
Body: {
  "localizationId": 123
}
Response: {
  "status": "generated|pending|failed",
  "message": "Status message"
}
```

### Authentication

#### Admin login
```
POST /api/auth/login
Body: {
  "email": "admin@example.com",
  "password": "password123"
}
Response: {
  "token": "eyJhbGc...",
  "user": {
    "id": 1,
    "email": "admin@example.com",
    "fullName": "Administrator",
    "role": "Admin"
  }
}
```

#### Register new admin
```
POST /api/auth/register
Body: {
  "email": "editor@example.com",
  "password": "password123",
  "fullName": "Editor Name",
  "role": "Editor"
}
```

#### Verify token
```
GET /api/auth/verify
Headers: Authorization: Bearer {token}
Response: {
  "isValid": true,
  "userId": 1,
  "role": "Admin"
}
```

---

## 🔧 Configuration

### Text-to-Speech Provider

This project uses **Edge-TTS** (free, no API key). It requires Python + `edge-tts`.

```json
{
  "TextToSpeech": {
    "Provider": "EdgeTts"
  },
  "EdgeTts": {
    "ExecutablePath": "",
    "TimeoutSeconds": 90,
    "SpeechRate": 0.25
  }
}
```

### Auto-translation (Vietnamese → other languages)

Uses a local/self-hosted **Ollama** server:

```json
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "qwen2.5:3b"
  }
}
```

### JWT Token Expiration
```json
{
  "Jwt": {
    "ExpirationMinutes": 1440
  }
}
```

---

## 💡 Usage Examples

### Mobile App: Select Language & Play Audio

```csharp
// On MapPage.xaml.cs
private async Task OnLanguageChanged(string languageCode)
{
    // Update geofence service language
    await _geofenceService.SetLanguageAsync(languageCode);
    
    // Sync localizations for current language
    var pois = _geofenceService.GetPOIs();
    foreach (var poi in pois)
    {
        poi.LanguageCode = languageCode;
        await _databaseService.SavePOIAsync(poi);
    }
}

// When user enters 30m radius
private async Task OnUserProximityChanged(Location userLocation)
{
    await _geofenceService.CheckProximity(userLocation);
    
    if (_geofenceService.ActivePOI != null)
    {
        // Audio plays automatically via GeofenceService
        System.Diagnostics.Debug.WriteLine($"Playing audio for: {_geofenceService.ActivePOI.Name}");
    }
}
```

### Backend: Create POI with Localizations

```csharp
// Step 1: Create location
var location = new Location
{
    Name = "Quán Hủ Tiếu Nam Vang",
    Description = "Hủ tiếu nước nổi tiếng Sài Gòn",
    Latitude = 10.7769,
    Longitude = 106.6970,
    QrCodeData = "hui-tieu-nam-vang"
};
_context.Locations.Add(location);
await _context.SaveChangesAsync();

// Step 2: Create localizations for all 5 languages
var languages = new[] { "vi-VN", "en-US", "zh-CN", "ja-JP", "ko-KR" };

foreach (var lang in languages)
{
    var localization = new Localization
    {
        LocationId = location.Id,
        LanguageCode = lang,
        LocalizedName = TranslateToLanguage(location.Name, lang),
        LocalizedDescription = TranslateToLanguage(location.Description, lang),
        AudioGenerationStatus = "pending"
    };
    _context.Localizations.Add(localization);
}
await _context.SaveChangesAsync();

// Step 3: Trigger warmup process (generate audio for all languages)
await _textToSpeechService.WarmupLocalizationsAsync(location.Id);
```

---

## 🗂️ Project Structure

### Backend
```
TourGuideApi/
├── Models/
│   ├── Location.cs
│   ├── Localization.cs          [NEW]
│   ├── User.cs                  [NEW]
│   └── POI.cs
├── Controllers/
│   ├── LocationsController.cs
│   ├── LocalizationsController.cs [NEW]
│   └── AuthController.cs         [NEW]
├── Services/
│   ├── ITextToSpeechService.cs   [UPDATED]
│   ├── EdgeTtsTextToSpeechService.cs
│   ├── ILocalizationTranslationService.cs
│   └── OllamaLocalizationTranslationService.cs
├── Data/
│   └── AppDbContext.cs           [UPDATED]
└── Program.cs                    [UPDATED]
```

### Mobile App
```
TouristGuideApp/
├── Models/
│   ├── POI.cs
│   ├── Location.cs
│   ├── Localization.cs           [NEW]
│   └── SupportedLanguages.cs     [NEW]
├── Services/
│   ├── AudioService.cs           [ENHANCED 4-Tier]
│   ├── GeofenceService.cs        [ENHANCED]
│   ├── ApiService.cs             [ENHANCED]
│   ├── DatabaseService.cs
│   ├── SyncService.cs            [NEW]
│   └── LocationService.cs
└── Views/
```

---

## ⚠️ Next Steps (Not Yet Implemented)

### 1. **Local AI Advisor (Optional)** 🚧
- Optional AI advisor using a local/self-hosted model (e.g., Ollama)
- Service to suggest dishes based on user preferences or mood
- Endpoint: `POST /api/advisor/recommend`

### 2. **Audio Caching & Prefetching** 🚧
- Batch download all localizations when user switches language
- Store MP3 files in device storage for TIER 1 playback
- Implement progressive downloading with progress UI

### 3. **Admin Web Dashboard** 🚧
- React/Next.js interface for content management
- Upload POI images and descriptions
- Manage localizations and voice preferences
- Monitor audio generation status

### 4. **PMTiles Maps for Offline** 🚧
- Generate MBTiles for District 4 area
- Implement offline map display in MAUI
- Reduce dependency on online map services

### 5. **Database Encryption** 🚧
- Encrypt sensitive POI owner information (PII)
- Use SQL Cipher for SQLite on mobile
- Implement field-level encryption on backend

---

## 📱 Testing Workflow

### 1. Create a Test POI
```bash
curl -X POST http://localhost:5214/api/locations \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Pho Hoa",
    "description": "Authentic Vietnamese pho...",
    "latitude": 10.7769,
    "longitude": 106.6970
  }'
```

### 2. Create Localizations
```bash
curl -X POST http://localhost:5214/api/localizations \
  -H "Content-Type: application/json" \
  -d '{
    "locationId": 1,
    "languageCode": "en-US",
    "localizedName": "Pho Hoa",
    "localizedDescription": "Authentic Vietnamese pho restaurant"
  }'
```

### 3. Mobile: Sync & Test
- Open MAUI app
- Select language
- Navigate to map location
- Trigger geofence proximity event
- Verify audio plays in correct language

---

## 🔐 Security Notes

1. **JWT Token**: Protect the signing key in production (use environment variables / a secret manager)
2. **Password**: Always hash with BCrypt (never store plaintext)
3. **CORS**: Update AllowedOrigins for production domains
4. **PII**: Encrypt restaurant owner information in database
5. **Secrets**: Store sensitive values in environment variables, not config files

---

## 📊 Database Schema

### Locations Table
```sql
CREATE TABLE Locations (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    Description TEXT NOT NULL,
    AudioUrl TEXT,
    Latitude REAL NOT NULL,
    Longitude REAL NOT NULL,
    QrCodeData TEXT,
    CreatedAt DATETIME DEFAULT GETUTCDATE()
)
```

### Localizations Table
```sql
CREATE TABLE Localizations (
    Id INTEGER PRIMARY KEY,
    LocationId INTEGER NOT NULL,
    LanguageCode TEXT NOT NULL,
    LocalizedName TEXT NOT NULL,
    LocalizedDescription TEXT NOT NULL,
    CachedAudioBase64 TEXT,
    CachedAudioUrl TEXT,
    TextToSpeechEndpoint TEXT,
    AudioGenerationStatus TEXT,
    TtsVoiceCode TEXT,
    CreatedAt DATETIME,
    UpdatedAt DATETIME,
    IsWarmupProcessed BIT,
    QrCodeData TEXT,
    FOREIGN KEY (LocationId) REFERENCES Locations(Id) ON DELETE CASCADE,
    UNIQUE (LocationId, LanguageCode)
)
```

### Users Table
```sql
CREATE TABLE Users (
    Id INTEGER PRIMARY KEY,
    Email TEXT NOT NULL UNIQUE,
    PasswordHash TEXT NOT NULL,
    FullName TEXT NOT NULL,
    Role TEXT,
    IsActive BIT,
    LastTokenIssuedAt DATETIME,
    CreatedAt DATETIME,
    UpdatedAt DATETIME
)
```

---

## 🎓 Learning Resources

- **Entity Framework Core**: https://docs.microsoft.com/ef/core/
- **JWT Authentication**: https://jwt.io/
- **Edge-TTS**: https://github.com/rany2/edge-tts
- **Ollama**: https://ollama.com/
- **MAUI Geolocation**: https://learn.microsoft.com/maui/platform-integration/device/geolocation/
- **AsyncIO Best Practices**: https://docs.microsoft.com/dotnet/csharp/async

---

## 📞 Support

For issues or questions:
1. Check the existing code comments
2. Verify configuration in appsettings.json
3. Review API Swagger documentation
4. Check mobile app debug logs

Good luck! 🚀
