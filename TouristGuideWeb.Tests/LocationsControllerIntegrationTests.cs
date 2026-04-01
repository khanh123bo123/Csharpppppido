using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using TouristGuideWeb.Models;
using TouristGuideWeb.Tests.Infrastructure;

namespace TouristGuideWeb.Tests;

public class LocationsControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public LocationsControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_WithValidData_RedirectsAndCreatesLocation()
    {
        _factory.Store.Reset();

        using var client = CreateClient();
        var token = await GetAntiforgeryTokenAsync(client, "/Locations/Create");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Name"] = "Hoan Kiem Lake",
            ["Description"] = "Central lake in Hanoi",
            ["Latitude"] = "21.0285",
            ["Longitude"] = "105.8522"
        });

        var response = await client.PostAsync("/Locations/Create", form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("/Locations/Details/", response.Headers.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);

        var all = _factory.Store.GetAll();
        Assert.Single(all);
        Assert.Equal("Hoan Kiem Lake", all[0].Name);
    }

    [Fact]
    public async Task Details_WithExistingLocation_ReturnsOkAndContainsName()
    {
        _factory.Store.Reset();
        var created = _factory.Store.Add(new Location
        {
            Name = "Temple of Literature",
            Description = "Historic site",
            Latitude = 21.0281,
            Longitude = 105.8357
        });

        using var client = CreateClient();
        var response = await client.GetAsync($"/Locations/Details/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Temple of Literature", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_WithExistingLocation_RedirectsAndRemovesLocation()
    {
        _factory.Store.Reset();
        var created = _factory.Store.Add(new Location
        {
            Name = "West Lake",
            Description = "Large freshwater lake",
            Latitude = 21.0492,
            Longitude = 105.8188
        });

        using var client = CreateClient();
        var token = await GetAntiforgeryTokenAsync(client, "/Locations");
        var response = await client.PostAsync(
            $"/Locations/Delete/{created.Id}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Locations", response.Headers.Location?.OriginalString);

        var deleted = _factory.Store.GetById(created.Id);
        Assert.Null(deleted);
    }

    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        return client;
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase);

        Assert.True(match.Success, "Anti-forgery token was not found in HTML response.");
        return match.Groups[1].Value;
    }
}
