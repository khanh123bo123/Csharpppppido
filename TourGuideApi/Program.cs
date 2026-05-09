using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TourGuideApi.Data;
using TourGuideApi.Services;
using Microsoft.OpenApi.Models;
using TourGuideApi.Hubs;

var builder = WebApplication.CreateBuilder(args);

// PaaS/container platforms may provide PORT. Bind to it when present.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port) && int.TryParse(port, out var portNumber))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{portNumber}");
}

// When running behind a reverse proxy (Azure App Service or similar), respect forwarded headers.
if (!builder.Environment.IsDevelopment())
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_TourGuideApi")));

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
builder.Services.AddHttpClient("GoogleFreeTts", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<ITextToSpeechService, GoogleFreeTextToSpeechService>();

// Sequential audio generation queue (avoids running edge-tts concurrently)
builder.Services.AddSingleton<IAudioGenerationQueue, InMemoryAudioGenerationQueue>();
builder.Services.AddScoped<LocalizationAudioGenerator>();
builder.Services.AddHostedService<AudioGenerationWorker>();

// Translation service (Vietnamese -> 4 languages) for localization pack generation
builder.Services.AddHttpClient("GoogleFreeTranslation", client =>
{
    client.Timeout = TimeSpan.FromSeconds(120);
});

builder.Services.AddScoped<ILocalizationTranslationService, GoogleFreeLocalizationTranslationService>();
builder.Services.AddScoped<LocalizationPackGenerator>();

builder.Services.AddControllers();
builder.Services.AddSignalR();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAdminWeb", policy =>
    {
        var csvOrigins = (builder.Configuration["AllowedOriginsCsv"] ?? string.Empty).Trim();
        var origins = !string.IsNullOrWhiteSpace(csvOrigins)
            ? csvOrigins
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(origin => !string.IsNullOrWhiteSpace(origin))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : (builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                ?? new[] { "https://YOUR-WEB-APP-NAME.azurewebsites.net" });

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

app.UseForwardedHeaders();

// Avoid long startup times on Azure App Service in general, but since we are deploying
// updates frequently, we'll force migrations to run on startup for convenience.
// NOTE: Commented out for production - migrations should be run separately or handled gracefully
// using var scope = app.Services.CreateScope();
// var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
// dbContext.Database.Migrate();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Vĩnh Khánh API v1");
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowAdminWeb");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AppHub>("/apphub");

// Root endpoint is handled by HealthController

app.Run();
