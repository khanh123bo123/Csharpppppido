using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TourGuideApi.Data;
using TourGuideApi.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Railway provides PORT. Bind to it when present.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port) && int.TryParse(port, out var portNumber))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{portNumber}");
}

// When running behind a reverse proxy (Railway), respect forwarded headers.
if (!builder.Environment.IsDevelopment())
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear(); // Fix obsolete warning
        options.KnownProxies.Clear();
    });
}

// Local (gitignored) overrides for machine-specific settings.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"];
if (!string.IsNullOrEmpty(jwtKey))
{
    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "TourGuideApi";
    var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "TourGuideApp";

    try
    {
        // Add JWT Bearer authentication if package is installed
        builder.Services.AddAuthentication("Bearer")
            .AddJwtBearer("Bearer", options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };
            });
    }
    catch
    {
        // JWT Bearer not available, skipping JWT configuration
        builder.Services.AddAuthentication();
    }
}
else
{
    builder.Services.AddAuthentication();
}

// Register Text-to-Speech Service (free, no paid cloud providers)
builder.Services.AddScoped<ITextToSpeechService, EdgeTtsTextToSpeechService>();

// Sequential audio generation queue (avoids running edge-tts concurrently)
builder.Services.AddSingleton<IAudioGenerationQueue, InMemoryAudioGenerationQueue>();
builder.Services.AddScoped<LocalizationAudioGenerator>();
builder.Services.AddHostedService<AudioGenerationWorker>();

// Translation service (Vietnamese -> 4 languages) for localization pack generation
builder.Services.AddHttpClient("OllamaTranslation", client =>
{
    client.Timeout = TimeSpan.FromSeconds(300); // Increased to 5 mins for 14b models
});

builder.Services.AddHttpClient("GeminiTranslation", client =>
{
    client.Timeout = TimeSpan.FromSeconds(120);
});

// Translation provider selection:
// - Preferred: set Translation:Provider = Gemini and provide Gemini:ApiKey
// - Offline/dev: Translation:Provider = Ollama and provide Ollama:BaseUrl + Ollama:Model
// - Disabled: Translation:Provider = Disabled
var translationProvider = (builder.Configuration["Translation:Provider"] ?? string.Empty).Trim();
var hasGeminiKey = !string.IsNullOrWhiteSpace(builder.Configuration["Gemini:ApiKey"]);
var hasOllamaBaseUrl = !string.IsNullOrWhiteSpace(builder.Configuration["Ollama:BaseUrl"]);

if (translationProvider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) || (string.IsNullOrWhiteSpace(translationProvider) && hasGeminiKey))
{
    builder.Services.AddScoped<ILocalizationTranslationService, GeminiLocalizationTranslationService>();
}
else if (translationProvider.Equals("Ollama", StringComparison.OrdinalIgnoreCase) || (string.IsNullOrWhiteSpace(translationProvider) && hasOllamaBaseUrl))
{
    builder.Services.AddScoped<ILocalizationTranslationService, OllamaLocalizationTranslationService>();
}
else
{
    builder.Services.AddScoped<ILocalizationTranslationService, DisabledLocalizationTranslationService>();
}
builder.Services.AddScoped<LocalizationPackGenerator>();

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IOnlineTracker, OnlineTracker>();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAdminWeb", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Vĩnh Khánh API", Version = "v1", Description = "Hệ thống API quản lý Phố Ẩm Thực Vĩnh Khánh" });
});

var app = builder.Build();

app.UseForwardedHeaders();

var ollamaBaseUrl = app.Configuration["Ollama:BaseUrl"];
var ollamaModel = app.Configuration["Ollama:Model"];
app.Logger.LogInformation(
    "Ollama translation configured: {Configured}. BaseUrl: {BaseUrl}. Model: {Model}",
    !string.IsNullOrWhiteSpace(ollamaBaseUrl) && !string.IsNullOrWhiteSpace(ollamaModel),
    ollamaBaseUrl,
    ollamaModel);

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    // EnsureCreated() creates the DB if it doesn't exist, but won't update schema.
    // Since we're in Dev with SQLite and no migrations folder, 
    // we'll manually ensure specific tables exist for a robust "fix triệt để".
    dbContext.Database.EnsureCreated();

    // Verification of critical tables (ScanLogs, Ratings)
    try {
        var conn = dbContext.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) conn.Open();
        using var cmd = conn.CreateCommand();
        
        // Fix for missing ScanLogs table
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='ScanLogs';";
        var existsScanLogs = cmd.ExecuteScalar();
        if (existsScanLogs == null) {
            app.Logger.LogWarning("Table 'ScanLogs' missing. Creating manually...");
            cmd.CommandText = @"CREATE TABLE ""ScanLogs"" (
                ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                ""LocationId"" INTEGER NOT NULL,
                ""LanguageCode"" TEXT NULL,
                ""ScannedAt"" TEXT NOT NULL,
                ""DeviceIdentifier"" TEXT NULL,
                ""UserIp"" TEXT NULL,
                CONSTRAINT ""FK_ScanLogs_Locations_LocationId"" FOREIGN KEY (""LocationId"") REFERENCES ""Locations"" (""Id"") ON DELETE CASCADE
            );";
            cmd.ExecuteNonQuery();
        }

        // Fix for missing Ratings table
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Ratings';";
        var existsRatings = cmd.ExecuteScalar();
        if (existsRatings == null) {
            app.Logger.LogWarning("Table 'Ratings' missing. Creating manually...");
            cmd.CommandText = @"CREATE TABLE ""Ratings"" (
                ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                ""LocationId"" INTEGER NOT NULL,
                ""Stars"" INTEGER NOT NULL,
                ""RatedAt"" TEXT NOT NULL,
                ""UserEmail"" TEXT NULL,
                ""DeviceIdentifier"" TEXT NULL,
                ""UserIp"" TEXT NULL,
                CONSTRAINT ""FK_Ratings_Locations_LocationId"" FOREIGN KEY (""LocationId"") REFERENCES ""Locations"" (""Id"") ON DELETE CASCADE
            );";
            cmd.ExecuteNonQuery();
        }

        // Fix for missing ListenLogs table
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='ListenLogs';";
        var existsListenLogs = cmd.ExecuteScalar();
        if (existsListenLogs == null) {
            app.Logger.LogWarning("Table 'ListenLogs' missing. Creating manually...");
            cmd.CommandText = @"CREATE TABLE ""ListenLogs"" (
                ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                ""LocationId"" INTEGER NOT NULL,
                ""LanguageCode"" TEXT NOT NULL,
                ""ListenedAt"" TEXT NOT NULL,
                ""DeviceId"" TEXT NULL,
                CONSTRAINT ""FK_ListenLogs_Locations_LocationId"" FOREIGN KEY (""LocationId"") REFERENCES ""Locations"" (""Id"") ON DELETE CASCADE
            );";
            cmd.ExecuteNonQuery();
        }
    } catch (Exception ex) {
        app.Logger.LogError(ex, "Error during manual table verification.");
    }
}

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Vĩnh Khánh API v1");
    c.RoutePrefix = "swagger"; 
});

// Redirect root to Swagger for easier access
app.MapGet("/", () => Results.Redirect("/swagger"));

// Shortcut for downloading the latest app
app.MapGet("/tai-app", () => Results.File(
    Path.Combine(app.Environment.ContentRootPath, "wwwroot", "downloads", "app-latest.apk"), 
    "application/vnd.android.package-archive", 
    "app-latest.apk"));

// Enable static files with APK support for QR downloads
var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
if (!provider.Mappings.ContainsKey(".apk"))
{
    provider.Mappings.Add(".apk", "application/vnd.android.package-archive");
}

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});

app.UseCors("AllowAdminWeb");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
