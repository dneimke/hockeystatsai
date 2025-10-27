using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HockeyStatsAI.Models;

namespace HockeyStatsAI.Services;

public partial class GeminiTranslator(string apiKey, IDatabaseTools databaseTools, HttpClient? httpClient = null)
{
    private readonly IDatabaseTools _databaseTools = databaseTools;
    private readonly HttpClient _httpClient = httpClient ?? new();
    private readonly string _apiKey = apiKey;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const string Prompt = """
        You are a SQL expert. Your task is to translate a natural language question into a SQL query.

        **Your first step is to always call the `ListAllTables()` tool to see the available tables.**

        After listing all tables, for each table that seems relevant to the user's question, you *must* call `GetTableSchema(tableName)` to understand its columns and `GetForeignKeys(tableName)` to understand its relationships with other tables. This comprehensive schema exploration is crucial before attempting to generate any SQL query.

        Use these tools to explore the schema and then generate the SQL query.

        When interpreting competition names, please consider both the 'Name' and 'ShortName' columns in the 'Competition' table. If a short, ambiguous term is used (e.g., 'M1M'), assume it refers to the 'ShortName'.

        After you have gathered all necessary information using the tools, your final response *must contain only the SQL query and nothing else*. Do not include any conversational text, explanations, or tool outputs. If you cannot generate a SQL query, respond with 'I cannot generate a SQL query for this question.'

        Example of a complete SQL query: SELECT Id, Name FROM Competition WHERE Name = 'M1M';

        Example of joining tables: To find clubs in a specific competition, you might join the 'Club' and 'CompetitionTeam' tables.
        Example of filtering: To find games for 'SHC', you would filter on the 'Name' or 'ShortName' column in the 'Club' table.

        Translate the following natural language question into a SQL query:

        Question: {question}

        SQL Query:
        """;

    public async Task<string?> TranslateToSql(string question)
    {
        var tools = GetTools();
        var conversationHistory = new List<Content>
        {
            new() { Parts = [new() { Text = Prompt.Replace("{question}", question) }] }
        };

        GeminiResponse? response;
        while (true)
        {
            response = await SendRequestAsync(conversationHistory, tools);

            var functionCalls = response?.Candidates.FirstOrDefault()?.Content?.Parts
                .Where(p => p.FunctionCall != null)
                .Select(p => p.FunctionCall!)
                .ToList();

            if (functionCalls == null || functionCalls.Count == 0)
            {
                break; // No more function calls, exit loop
            }

            // Add the model's response (containing the function calls) to the history
            var modelResponseContent = response?.Candidates.FirstOrDefault()?.Content;
            if (modelResponseContent != null)
            {
                conversationHistory.Add(modelResponseContent);
            }

            // Execute all function calls and add their responses to the history
            var toolResponses = await ExecuteFunctionCallsAsync(functionCalls);
            conversationHistory.Add(new Content { Role = "tool", Parts = toolResponses });
        }

        return GetSqlFromResponse(response);
    }

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

    private async Task<List<Part>> ExecuteFunctionCallsAsync(List<FunctionCall> functionCalls)
    {
        var toolResponses = new List<Part>();

        foreach (var functionCall in functionCalls)
        {
            Console.WriteLine($"Executing tool: {functionCall.Name}");
            JsonElement toolOutput;

            switch (functionCall.Name)
            {
                case "ListAllTables":
                    var tables = _databaseTools.ListAllTables();
                    toolOutput = JsonSerializer.SerializeToElement(new { tables }, s_jsonOptions);
                    break;
                case "GetTableSchema":
                    var tableNameSchema = functionCall.Args.GetProperty("tableName").GetString();
                    var tableSchema = _databaseTools.GetTableSchema(tableNameSchema!);
                    toolOutput = JsonSerializer.SerializeToElement(new { schema = tableSchema }, s_jsonOptions);
                    break;
                case "GetForeignKeys":
                    var tableNameKeys = functionCall.Args.GetProperty("tableName").GetString();
                    var foreignKeys = _databaseTools.GetForeignKeys(tableNameKeys!);
                    toolOutput = JsonSerializer.SerializeToElement(new { foreignKeys }, s_jsonOptions);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown function call: {functionCall.Name}");
            }

            toolResponses.Add(new Part
            {
                FunctionResponse = new FunctionResponse
                {
                    Name = functionCall.Name,
                    Response = toolOutput
                }
            });
        }

        // The model can call multiple tools in parallel, so we use Task.WhenAll although our execution is sequential for now.
        // This is a placeholder for potential future parallel execution of tools.
        await Task.WhenAll();

        return toolResponses;
    }

    private static List<Tool> GetTools() =>
    [
        new()
        {
            FunctionDeclarations =
            [
                new()
                {
                    Name = "ListAllTables",
                    Description = "Lists all tables in the database.",
                    Parameters = new FunctionParameters { Properties = new() }
                },
                new()
                {
                    Name = "GetTableSchema",
                    Description = "Gets the schema for a given table.",
                    Parameters = new FunctionParameters
                    {
                        Required = ["tableName"],
                        Properties = new()
                        {
                            ["tableName"] = new ParameterProperty { Type = "string", Description = "The name of the table." }
                        }
                    }
                },
                new()
                {
                    Name = "GetForeignKeys",
                    Description = "Gets the foreign keys for a given table.",
                    Parameters = new FunctionParameters
                    {
                        Required = ["tableName"],
                        Properties = new()
                        {
                            ["tableName"] = new ParameterProperty { Type = "string", Description = "The name of the table." }
                        }
                    }
                }
            ]
        }
    ];

    private static string? GetSqlFromResponse(GeminiResponse? response)
    {
        var fullText = response?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

        if (string.IsNullOrEmpty(fullText))
        {
            return null;
        }

        // Try to find SQL wrapped in ```sql
        var sqlBlockMatch = BlockSqlRegex().Match(fullText);
        if (sqlBlockMatch.Success)
        {
            return sqlBlockMatch.Groups[1].Value.Trim();
        }

        // If not found, try to find a standalone SQL query
        var sqlQueryMatch = StandaloneSqlRegex().Match(fullText);
        if (sqlQueryMatch.Success)
        {
            return sqlQueryMatch.Value.Trim();
        }

        return null;
    }

    [GeneratedRegex(@"```sql\n(.*?)```", RegexOptions.Singleline)]
    private static partial Regex BlockSqlRegex();
    [GeneratedRegex(@"^\s*(SELECT|INSERT|UPDATE|DELETE|CREATE|ALTER|DROP)\s+.*?(;|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline, "en-AU")]
    private static partial Regex StandaloneSqlRegex();
}