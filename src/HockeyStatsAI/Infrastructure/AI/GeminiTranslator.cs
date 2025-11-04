using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HockeyStatsAI.Infrastructure.AI.Strategies;
using HockeyStatsAI.Models.Gemini;

namespace HockeyStatsAI.Infrastructure.AI;

/// <summary>
/// Translates natural language questions to SQL queries using Gemini AI.
/// Uses a strategy pattern to support different translation approaches.
/// </summary>
/// <remarks>
/// This class is registered as a singleton in <see cref="Configuration.ServiceCollectionExtensions.AddHockeyStatsServices"/>.
/// It acts as a facade over <see cref="ISqlTranslationStrategy"/> implementations (currently <see cref="TargetedTranslationStrategy"/>).
/// It is used in <see cref="Program"/> to translate user questions to SQL queries that are then validated
/// by <see cref="SqlSafetyValidator"/> and executed by <see cref="SqlExecutor"/>.
/// The class also provides a <see cref="GetSummary"/> method used by <see cref="Program"/> to explain SQL queries.
/// </remarks>
public partial class GeminiTranslator
{
    private readonly ISqlTranslationStrategy _translationStrategy;
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiTranslator"/> class.
    /// </summary>
    /// <param name="translationStrategy">The translation strategy to use (e.g., <see cref="TargetedTranslationStrategy"/>).</param>
    /// <param name="apiKey">The Gemini API key for making requests.</param>
    /// <param name="httpClient">The HTTP client for making API requests (should be configured with retry policies).</param>
    public GeminiTranslator(ISqlTranslationStrategy translationStrategy, string apiKey, HttpClient httpClient)
    {
        _translationStrategy = translationStrategy;
        _apiKey = apiKey;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Translates a natural language question to SQL using the configured translation strategy.
    /// </summary>
    /// <param name="question">The natural language question to translate.</param>
    /// <returns>
    /// The generated SQL query, or null if translation failed.
    /// </returns>
    /// <remarks>
    /// This method delegates to the configured <see cref="ISqlTranslationStrategy"/> implementation.
    /// The generated SQL is typically validated by <see cref="SqlSafetyValidator"/> before execution.
    /// </remarks>
    public Task<string?> TranslateToSqlTargeted(string question) => _translationStrategy.TranslateAsync(question);

    /// <summary>
    /// Gets a summary or explanation from Gemini for a given prompt.
    /// </summary>
    /// <param name="prompt">The prompt to send to Gemini for summary generation.</param>
    /// <returns>
    /// The generated summary text, or null if the request failed.
    /// </returns>
    /// <remarks>
    /// This method is used by <see cref="Program"/> "explain" command to explain SQL queries in plain English.
    /// It sends a simple text prompt to the Gemini API without tools or schema information.
    /// </remarks>
    public async Task<string?> GetSummary(string prompt)
    {
        var conversationHistory = new List<Content>
        {
            new() { Parts = [new() { Text = prompt }] }
        };

        var response = await SendRequestAsync(conversationHistory, new List<Tool>());
        return response?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
    }

    /// <summary>
    /// Sends a request to the Gemini API with the specified contents and tools.
    /// </summary>
    /// <param name="contents">The conversation contents to send.</param>
    /// <param name="tools">The tools to include in the request (currently unused).</param>
    /// <returns>
    /// The Gemini API response, or null if the request failed.
    /// </returns>
    /// <remarks>
    /// This method sends requests to the Gemini 2.5 Flash model API endpoint.
    /// It handles HTTP errors and logs them to the console.
    /// Used by both <see cref="GetSummary"/> and internally by <see cref="ISqlTranslationStrategy"/> implementations.
    /// </remarks>
    private async Task<GeminiResponse?> SendRequestAsync(List<Content> contents, List<Tool> tools)
    {
        var requestUri = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
        var request = new GeminiRequest
        {
            Contents = contents,
            Tools = tools
        };

        var jsonContent = JsonSerializer.Serialize(request, s_jsonOptions);
        var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(requestUri, httpContent);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Error: {response.StatusCode}");
            Console.WriteLine(await response.Content.ReadAsStringAsync());
            return null;
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GeminiResponse>(responseBody, s_jsonOptions);
    }
}
