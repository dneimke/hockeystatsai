using System.Text.Json;
using System.Text.Json.Serialization;
using HockeyStatsAI.Core.Schema;
using HockeyStatsAI.Infrastructure.Database;
using HockeyStatsAI.Models.Gemini;
using HockeyStatsAI.Models.Schema;

namespace HockeyStatsAI.Infrastructure.AI.Strategies;

/// <summary>
/// Strategy that uses targeted schema retrieval to build optimized prompts.
/// Pre-filters relevant tables and columns before sending to the AI model.
/// </summary>
public sealed class TargetedTranslationStrategy : ISqlTranslationStrategy
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    private readonly IDatabaseTools _databaseTools;
    private readonly SchemaRegistry _registry;
    private readonly SchemaRetriever _retriever;
    private readonly JoinPathFinder _joinPathFinder;
    private readonly PromptBuilder _promptBuilder;
    private readonly int _maxTables;
    private readonly int _maxColumnsPerTable;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="TargetedTranslationStrategy"/> class.
    /// </summary>
    /// <param name="apiKey">The Gemini API key for making requests.</param>
    /// <param name="httpClient">The HTTP client for making API requests.</param>
    /// <param name="databaseTools">The database tools for loading schema information.</param>
    /// <param name="registry">The schema registry for accessing cached schema data.</param>
    /// <param name="retriever">The schema retriever for finding relevant tables and columns.</param>
    /// <param name="joinPathFinder">The join path finder for determining table relationships.</param>
    /// <param name="promptBuilder">The prompt builder for constructing optimized prompts.</param>
    /// <param name="maxTables">The maximum number of tables to include in the prompt (default: 4).</param>
    /// <param name="maxColumnsPerTable">The maximum number of columns per table to include (default: 8).</param>
    public TargetedTranslationStrategy(
        string apiKey,
        HttpClient httpClient,
        IDatabaseTools databaseTools,
        SchemaRegistry registry,
        SchemaRetriever retriever,
        JoinPathFinder joinPathFinder,
        PromptBuilder promptBuilder,
        int maxTables = 4,
        int maxColumnsPerTable = 8)
    {
        _apiKey = apiKey;
        _httpClient = httpClient;
        _databaseTools = databaseTools;
        _registry = registry;
        _retriever = retriever;
        _joinPathFinder = joinPathFinder;
        _promptBuilder = promptBuilder;
        _maxTables = maxTables;
        _maxColumnsPerTable = maxColumnsPerTable;
    }

    /// <summary>
    /// Translates a natural language question to SQL using targeted schema retrieval.
    /// </summary>
    /// <param name="question">The natural language question to translate.</param>
    /// <returns>
    /// The generated SQL query, or null if translation failed (e.g., no relevant tables found).
    /// </returns>
    /// <remarks>
    /// This method implements the targeted translation strategy:
    /// 1. Ensures the schema is loaded (from cache or database)
    /// 2. Uses <see cref="SchemaRetriever"/> to find relevant tables and columns
    /// 3. Uses <see cref="JoinPathFinder"/> to determine how to join tables
    /// 4. Uses <see cref="PromptBuilder"/> to build an optimized prompt with only relevant schema
    /// 5. Sends the prompt to Gemini API and extracts the SQL from the response
    /// This strategy reduces token usage and improves accuracy by pre-filtering schema information
    /// before sending it to the AI model.
    /// </remarks>
    public async Task<string?> TranslateAsync(string question)
    {
        // Ensure schema is loaded
        if (_registry.Schema == null)
        {
            await _registry.LoadOrBuildAsync(() => ((DatabaseTools)_databaseTools).LoadDatabaseSchemaAsync());
        }

        // Get relevant tables based on the question
        var tables = _retriever.GetRelevantTables(question, _maxTables);
        if (tables.Count == 0)
        {
            return null;
        }

        // Get relevant columns for each table
        var columnsByTable = new Dictionary<string, IReadOnlyList<ColumnSchema>>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tables)
        {
            var columns = _retriever.GetRelevantColumns(t, question, _maxColumnsPerTable);
            
            // Ensure both Name and ShortName are included for Club and Competition tables
            // when filtering by name, so the AI can check both columns
            if (t.TableName.Equals("Club", StringComparison.OrdinalIgnoreCase) ||
                t.TableName.Equals("Competition", StringComparison.OrdinalIgnoreCase))
            {
                var columnsList = columns.ToList();
                var nameCol = t.Columns.FirstOrDefault(c => c.ColumnName.Equals("Name", StringComparison.OrdinalIgnoreCase));
                var shortNameCol = t.Columns.FirstOrDefault(c => c.ColumnName.Equals("ShortName", StringComparison.OrdinalIgnoreCase));
                
                if (nameCol != null && !columnsList.Any(c => c.ColumnName.Equals("Name", StringComparison.OrdinalIgnoreCase)))
                {
                    columnsList.Add(nameCol);
                }
                if (shortNameCol != null && !columnsList.Any(c => c.ColumnName.Equals("ShortName", StringComparison.OrdinalIgnoreCase)))
                {
                    columnsList.Add(shortNameCol);
                }
                
                columns = columnsList;
            }
            
            columnsByTable[t.FullName] = columns;
        }

        // Find join path between tables
        var joinPlan = _joinPathFinder.FindJoinPlan(tables);

        // Build optimized prompt with only relevant schema information
        var prompt = _promptBuilder.BuildPrompt(question, tables, columnsByTable, joinPlan);

        // Send request to Gemini (no tools needed - we've pre-filtered the schema)
        var conversationHistory = new List<Content>
        {
            new() { Parts = [new() { Text = prompt }] }
        };

        var response = await SendRequestAsync(conversationHistory, new List<Tool>());
        return GetSqlFromResponse(response);
    }

    /// <summary>
    /// Sends a request to the Gemini API with the specified contents and tools.
    /// </summary>
    /// <param name="contents">The conversation contents to send.</param>
    /// <param name="tools">The tools to include in the request (not used in targeted strategy).</param>
    /// <returns>The Gemini API response, or null if the request failed.</returns>
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

    /// <summary>
    /// Extracts SQL query from the Gemini API response.
    /// </summary>
    /// <param name="response">The Gemini API response.</param>
    /// <returns>
    /// The extracted SQL query, or null if no valid SQL was found in the response.
    /// </returns>
    /// <remarks>
    /// This method attempts to extract SQL from the response in multiple ways:
    /// 1. Looks for SQL wrapped in ```sql code blocks
    /// 2. Looks for standalone SELECT statements ending with semicolon
    /// 3. Looks for SELECT statements ending with double newline
    /// 4. Looks for any SELECT statement with a FROM clause
    /// The method validates that extracted SQL contains a FROM clause before returning it.
    /// </remarks>
    private static string? GetSqlFromResponse(GeminiResponse? response)
    {
        var fullText = response?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

        if (string.IsNullOrEmpty(fullText))
        {
            return null;
        }

        // Try to find SQL wrapped in ```sql (with optional language identifier)
        var sqlBlockMatch = System.Text.RegularExpressions.Regex.Match(
            fullText, 
            @"```(?:sql)?\s*\n(.*?)```", 
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (sqlBlockMatch.Success)
        {
            return sqlBlockMatch.Groups[1].Value.Trim();
        }

        // If not found, try to find a standalone SQL query
        // Match from SELECT to the end of the statement, capturing the full query
        // First try to match complete SELECT statements that end with semicolon
        var sqlQueryMatch = System.Text.RegularExpressions.Regex.Match(
            fullText, 
            @"(SELECT\s+.*?;)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        if (sqlQueryMatch.Success)
        {
            return sqlQueryMatch.Groups[1].Value.Trim().TrimEnd(';');
        }

        // If no semicolon, try to match until end of text or double newline
        sqlQueryMatch = System.Text.RegularExpressions.Regex.Match(
            fullText, 
            @"(SELECT\s+.*?)(?:\s*$|\n{2,})", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        if (sqlQueryMatch.Success)
        {
            var sql = sqlQueryMatch.Groups[1].Value.Trim();
            // Basic validation: ensure it has FROM clause
            if (sql.Contains("FROM", StringComparison.OrdinalIgnoreCase))
            {
                return sql;
            }
        }

        // Last resort: try to extract any SQL-like statement that includes FROM
        var anySqlMatch = System.Text.RegularExpressions.Regex.Match(
            fullText,
            @"(SELECT\s+.*?FROM\s+[^;]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        if (anySqlMatch.Success)
        {
            return anySqlMatch.Groups[1].Value.Trim();
        }

        return null;
    }
}

