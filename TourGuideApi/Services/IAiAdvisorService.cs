using System;
using System.Threading.Tasks;

namespace TourGuideApi.Services;

/// <summary>
/// AI Advisor Service (optional).
/// This project intentionally avoids paid cloud AI providers.
/// 
/// FEATURE NOT YET IMPLEMENTED - This is a template/scaffold.
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
public class DisabledAiAdvisorService : IAiAdvisorService
{
    private readonly ILogger<DisabledAiAdvisorService>? _logger;

    public DisabledAiAdvisorService(ILogger<DisabledAiAdvisorService>? logger = null)
    {
        _logger = logger;
    }

    public async Task<AiRecommendation> GetRecommendationAsync(string userPreference, string languageCode)
    {
        // TODO: Optionally implement with a local/self-hosted model (e.g., Ollama).
        
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
        // TODO: Optionally generate summaries using a local/self-hosted model.
        await Task.Delay(100);
        return "Restaurant info coming soon";
    }

    public async Task<string> ChatAsync(string userMessage, string languageCode)
    {
        // TODO: Optionally implement conversational AI using a local/self-hosted model.
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
// TODO: Implement a local/self-hosted AI advisor (optional)
