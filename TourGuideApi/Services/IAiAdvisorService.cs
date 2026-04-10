using System;
using System.Threading.Tasks;

namespace TourGuideApi.Services;

/// <summary>
/// AI Advisor Service using Google's Gemini 2.0 Flash
/// Provides food recommendations based on user preferences
/// 
/// FEATURE NOT YET IMPLEMENTED - This is a template/scaffold
/// </summary>
public interface IAiAdvisorService
{
    /// <summary>
    /// Get food recommendations based on user mood/preference
    /// </summary>
    Task<AiRecommendation> GetRecommendationAsync(string userPreference, string languageCode);

    /// <summary>
    /// Get information about a specific restaurant
    /// </summary>
    Task<string> GetRestaurantInfoAsync(int locationId, string languageCode);

    /// <summary>
    /// Chat with the AI advisor
    /// </summary>
    Task<string> ChatAsync(string userMessage, string languageCode);
}

/// <summary>
/// Placeholder implementation - DO NOT USE IN PRODUCTION
/// </summary>
public class GeminiAiAdvisorService : IAiAdvisorService
{
    // TODO: Implement this service with Gemini 2.0 Flash API
    private readonly string? _geminiApiKey;
    private readonly ILogger<GeminiAiAdvisorService>? _logger;

    public GeminiAiAdvisorService(IConfiguration config, ILogger<GeminiAiAdvisorService>? logger = null)
    {
        _geminiApiKey = config["Gemini:ApiKey"];
        _logger = logger;
    }

    public async Task<AiRecommendation> GetRecommendationAsync(string userPreference, string languageCode)
    {
        // TODO: Call Gemini API with prompt:
        // "Based on this preference: {userPreference}, recommend a dish from District 4's culinary scene
        //  in {languageCode} language with explanation"
        
        await Task.Delay(100);
        return new AiRecommendation
        {
            RecommendedDish = "Coming soon",
            Reason = "AI feature not yet implemented",
            LocationSuggestion = "Check back soon for AI recommendations"
        };
    }

    public async Task<string> GetRestaurantInfoAsync(int locationId, string languageCode)
    {
        // TODO: Use Gemini to generate restaurant summaries from POI data
        await Task.Delay(100);
        return "Restaurant info coming soon";
    }

    public async Task<string> ChatAsync(string userMessage, string languageCode)
    {
        // TODO: Implement conversational AI for tourism guidance
        await Task.Delay(100);
        return "Chat feature coming soon";
    }
}

public class AiRecommendation
{
    public string RecommendedDish { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string LocationSuggestion { get; set; } = string.Empty;
    public int? LocationId { get; set; }
    public double? EstimatedPrice { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
}

// TODO: Add controllers to use this service
// TODO: Add Gemini SDK NuGet package
// TODO: Implement fallback for when Gemini API is unavailable
