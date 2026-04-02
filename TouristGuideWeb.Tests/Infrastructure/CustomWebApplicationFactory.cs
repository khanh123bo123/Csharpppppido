using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TouristGuideWeb.Data;
using TouristGuideWeb.Models;

namespace TouristGuideWeb.Tests.Infrastructure;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public InMemoryLocationStore Store { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppIdentityDbContext>>();
            services.RemoveAll<AppIdentityDbContext>();
            services.RemoveAll<SqliteConnection>();

            var sqliteConnection = new SqliteConnection("DataSource=:memory:");
            sqliteConnection.Open();
            services.AddSingleton(sqliteConnection);

            services.AddDbContext<AppIdentityDbContext>(options =>
                options.UseSqlite(sqliteConnection));

            services.RemoveAll<IHttpClientFactory>();
            services.AddSingleton(Store);
            services.AddSingleton<IHttpClientFactory, FakeLocationApiHttpClientFactory>();

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });

            services.PostConfigure<MvcOptions>(options =>
            {
                options.Filters.Add(new IgnoreAntiforgeryTokenAttribute());
            });

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            identityDb.Database.EnsureDeleted();
            identityDb.Database.EnsureCreated();
        });
    }
}

internal sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestAuth";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "integration-test-user"),
            new Claim(ClaimTypes.Name, "integration-test-user")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public sealed class InMemoryLocationStore
{
    private readonly List<Location> _locations = new();
    private readonly object _gate = new();
    private int _nextId = 1;

    public void Reset()
    {
        lock (_gate)
        {
            _locations.Clear();
            _nextId = 1;
        }
    }

    public IReadOnlyList<Location> GetAll()
    {
        lock (_gate)
        {
            return _locations.Select(Clone).ToList();
        }
    }

    public Location? GetById(int id)
    {
        lock (_gate)
        {
            var existing = _locations.FirstOrDefault(location => location.Id == id);
            return existing is null ? null : Clone(existing);
        }
    }

    public Location Add(Location location)
    {
        lock (_gate)
        {
            var created = Clone(location);
            created.Id = _nextId++;
            created.QrCodeData = string.IsNullOrWhiteSpace(created.QrCodeData)
                ? $"LOC_{Guid.NewGuid():N}"
                : created.QrCodeData;
            created.CreatedAt = created.CreatedAt == default ? DateTime.UtcNow : created.CreatedAt;
            _locations.Add(created);
            return Clone(created);
        }
    }

    public bool Update(int id, Location location)
    {
        lock (_gate)
        {
            var index = _locations.FindIndex(existing => existing.Id == id);
            if (index < 0)
            {
                return false;
            }

            var updated = Clone(location);
            updated.Id = id;
            if (string.IsNullOrWhiteSpace(updated.QrCodeData))
            {
                updated.QrCodeData = _locations[index].QrCodeData;
            }

            if (updated.CreatedAt == default)
            {
                updated.CreatedAt = _locations[index].CreatedAt;
            }

            _locations[index] = updated;
            return true;
        }
    }

    public bool Delete(int id)
    {
        lock (_gate)
        {
            var index = _locations.FindIndex(location => location.Id == id);
            if (index < 0)
            {
                return false;
            }

            _locations.RemoveAt(index);
            return true;
        }
    }

    private static Location Clone(Location source)
    {
        return new Location
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            AudioUrl = source.AudioUrl,
            Latitude = source.Latitude,
            Longitude = source.Longitude,
            QrCodeData = source.QrCodeData,
            CreatedAt = source.CreatedAt
        };
    }
}

internal sealed class FakeLocationApiHttpClientFactory : IHttpClientFactory
{
    private readonly InMemoryLocationStore _store;

    public FakeLocationApiHttpClientFactory(InMemoryLocationStore store)
    {
        _store = store;
    }

    public HttpClient CreateClient(string name)
    {
        return new HttpClient(new FakeLocationApiMessageHandler(_store))
        {
            BaseAddress = new Uri("http://localhost/")
        };
    }
}

internal sealed class FakeLocationApiMessageHandler : HttpMessageHandler
{
    private readonly InMemoryLocationStore _store;

    public FakeLocationApiMessageHandler(InMemoryLocationStore store)
    {
        _store = store;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath.Trim('/') ?? string.Empty;
        var method = request.Method;

        if (!path.StartsWith("api/locations", StringComparison.OrdinalIgnoreCase))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        if (method == HttpMethod.Get && path.Equals("api/locations", StringComparison.OrdinalIgnoreCase))
        {
            return JsonResponse(_store.GetAll());
        }

        if (TryGetId(path, out var id))
        {
            if (method == HttpMethod.Get)
            {
                var location = _store.GetById(id);
                return location is null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : JsonResponse(location);
            }

            if (method == HttpMethod.Put)
            {
                var body = await request.Content!.ReadFromJsonAsync<Location>(cancellationToken: cancellationToken);
                if (body is null || id != body.Id)
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }

                var updated = _store.Update(id, body);
                return new HttpResponseMessage(updated ? HttpStatusCode.NoContent : HttpStatusCode.NotFound);
            }

            if (method == HttpMethod.Delete)
            {
                var deleted = _store.Delete(id);
                return new HttpResponseMessage(deleted ? HttpStatusCode.NoContent : HttpStatusCode.NotFound);
            }
        }

        if (method == HttpMethod.Post && path.Equals("api/locations", StringComparison.OrdinalIgnoreCase))
        {
            var body = await request.Content!.ReadFromJsonAsync<Location>(cancellationToken: cancellationToken);
            if (body is null)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            var created = _store.Add(body);
            var response = JsonResponse(created);
            response.StatusCode = HttpStatusCode.Created;
            response.Headers.Location = new Uri($"http://localhost/api/locations/{created.Id}");
            return response;
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static bool TryGetId(string path, out int id)
    {
        id = 0;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 3)
        {
            return false;
        }

        return int.TryParse(segments[2], out id);
    }

    private static HttpResponseMessage JsonResponse<T>(T value)
    {
        var json = JsonSerializer.Serialize(value);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}
