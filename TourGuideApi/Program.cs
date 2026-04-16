using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TourGuideApi.Data;
using TourGuideApi.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

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
    client.Timeout = TimeSpan.FromSeconds(120);
});

builder.Services.AddScoped<ILocalizationTranslationService, OllamaLocalizationTranslationService>();

builder.Services.AddControllers();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAdminWeb", policy =>
    {
        var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() 
            ?? new[] { "https://localhost:7001", "http://localhost:3000" };
        policy.WithOrigins(origins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Vĩnh Khánh API", Version = "v1", Description = "Hệ thống API quản lý Phố Ẩm Thực Vĩnh Khánh" });
});

var app = builder.Build();

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
    dbContext.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Vĩnh Khánh API v1");
    });
}

// app.UseHttpsRedirection(); // Commented out to allow Android emulator to use HTTP on port 5214
app.UseStaticFiles();
app.UseCors("AllowAdminWeb");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
