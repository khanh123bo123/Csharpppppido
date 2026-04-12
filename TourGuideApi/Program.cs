using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TourGuideApi.Data;
using TourGuideApi.Services;

var builder = WebApplication.CreateBuilder(args);

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

// Register Text-to-Speech Service (4-tier hybrid system)
var ttsProvider = builder.Configuration["TextToSpeech:Provider"] ?? "Azure";
switch (ttsProvider.Trim().ToLowerInvariant())
{
    case "google":
        builder.Services.AddScoped<ITextToSpeechService, GoogleTextToSpeechService>();
        break;
    case "edgetts":
    case "edge-tts":
    case "edge_tts":
        builder.Services.AddScoped<ITextToSpeechService, EdgeTtsTextToSpeechService>();
        break;
    default:
        builder.Services.AddScoped<ITextToSpeechService, AzureTextToSpeechService>();
        break;
}

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
builder.Services.AddSwaggerGen();

var app = builder.Build();

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
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection(); // Commented out to allow Android emulator to use HTTP on port 5214
app.UseStaticFiles();
app.UseCors("AllowAdminWeb");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
