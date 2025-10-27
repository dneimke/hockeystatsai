using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace HockeyStatsAI.Services;

public partial class GeminiTranslator(string apiKey, IDatabaseTools databaseTools, HttpClient? httpClient = null)
{
    private readonly IDatabaseTools _databaseTools = databaseTools;
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();

    public async Task<string?> TranslateToSql(string question)
    {
        var prompt =
            $"You are a SQL expert. Your task is to translate a natural language question into a SQL query.\n\n**Your first step is to always call the `ListAllTables()` tool to see the available tables.**\n\nAfter listing all tables, for each table that seems relevant to the user's question, you *must* call `GetTableSchema(tableName)` to understand its columns and `GetForeignKeys(tableName)` to understand its relationships with other tables. This comprehensive schema exploration is crucial before attempting to generate any SQL query.\n\nUse these tools to explore the schema and then generate the SQL query.\n\nWhen interpreting competition names, please consider both the 'Name' and 'ShortName' columns in the 'Competition' table. If a short, ambiguous term is used (e.g., 'M1M'), assume it refers to the 'ShortName'.\n\nAfter you have gathered all necessary information using the tools, your final response *must contain only the SQL query and nothing else*. Do not include any conversational text, explanations, or tool outputs. If you cannot generate a SQL query, respond with 'I cannot generate a SQL query for this question.'\n\nExample of a complete SQL query: SELECT Id, Name FROM Competition WHERE Name = 'M1M';\n\nExample of joining tables: To find clubs in a specific competition, you might join the 'Club' and 'CompetitionTeam' tables.\nExample of filtering: To find games for 'SHC', you would filter on the 'Name' or 'ShortName' column in the 'Club' table.\n\nTranslate the following natural language question into a SQL query:\n\nQuestion: {question}\n\nSQL Query:";

        var tools = new JsonObject
        {
            ["function_declarations"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "ListAllTables",
                    ["description"] = "Lists all tables in the database.",
                    ["parameters"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject()
                    }
                },
                new JsonObject
                {
                    ["name"] = "GetTableSchema",
                    ["description"] = "Gets the schema for a given table.",
                    ["parameters"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["tableName"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["description"] = "The name of the table."
                            }
                        },
                        ["required"] = new JsonArray("tableName")
                    }
                },
                new JsonObject
                {
                    ["name"] = "GetForeignKeys",
                    ["description"] = "Gets the foreign keys for a given table.",
                    ["parameters"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["tableName"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["description"] = "The name of the table."
                            }
                        },
                        ["required"] = new JsonArray("tableName")
                    }
                }
            }
        };

        var client = _httpClient;
        var request = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}");

        var contents = new List<JsonObject>
        {
            new() {
                ["parts"] = new JsonArray(
                new JsonObject
                {
                    ["text"] = prompt
                })
            }
        };

        var requestContentsArray = new JsonArray();

        foreach (var item in contents)
        {
            requestContentsArray.Add(JsonNode.Parse(item.ToJsonString()!)!); // Deep clone each item
        }

        var requestContent = new JsonObject
        {
            ["contents"] = requestContentsArray,
            ["tools"] = new JsonArray(tools)
        };

        request.Content = new StringContent(requestContent.ToJsonString(), System.Text.Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Error: {response.StatusCode}");
            Console.WriteLine(await response.Content.ReadAsStringAsync());
            return null;
        }

        string responseBody = await response.Content.ReadAsStringAsync();
        var responseNode = JsonNode.Parse(responseBody);
        // Console.WriteLine($"LLM Response (Initial): {responseNode.ToJsonString()}");

        while (true)
        {
            var functionCalls = responseNode?["candidates"]?[0]?["content"]?["parts"]?.AsArray()
                .Where(p => p?["functionCall"] != null)
                .Select(p => p!["functionCall"]!)
                .ToList();

            if (functionCalls == null || functionCalls.Count == 0)
            {
                break; // No more function calls, exit loop
            }

            // Add the model's response (containing function calls) to the history
            var modelContent = responseNode?["candidates"]?[0]?["content"];
            if (modelContent != null)
            {
                var newModelContent = new JsonObject();
                foreach (var property in modelContent.AsObject())
                {
                    newModelContent[property.Key] = (JsonNode)JsonNode.Parse(property.Value!.ToJsonString()!)!;
                }
                contents.Add(newModelContent);
            }

            // Execute tools and add their responses to the history
            foreach (var functionCall in functionCalls)
            {
                var functionName = functionCall?["name"]?.GetValue<string>();
                var functionArgs = functionCall?["args"];

                JsonNode toolOutput;

                Console.WriteLine($"Calling function: {functionName}");
                switch (functionName)
                {
                    case "ListAllTables":
                        {
                            var tables = _databaseTools.ListAllTables();
                            toolOutput = new JsonObject { ["tables"] = JsonSerializer.SerializeToNode(tables)! };
                            break;
                        }
                    case "GetTableSchema":
                        {
                            var tableName = functionArgs?["tableName"]?.GetValue<string>();
                            var tableSchema = _databaseTools.GetTableSchema(tableName!);
                            toolOutput = new JsonObject { ["schema"] = JsonSerializer.SerializeToNode(tableSchema)! };
                            break;
                        }
                    case "GetForeignKeys":
                        {
                            var tableName = functionArgs?["tableName"]?.GetValue<string>();
                            var foreignKeys = _databaseTools.GetForeignKeys(tableName!);
                            toolOutput = new JsonObject { ["foreignKeys"] = JsonSerializer.SerializeToNode(foreignKeys) };
                            break;
                        }
                    default:
                        throw new InvalidOperationException($"Unknown function call: {functionName}");
                }

                contents.Add(new JsonObject
                {
                    ["role"] = "tool",
                    ["parts"] = new JsonArray(new JsonObject
                    {
                        ["functionResponse"] = new JsonObject
                        {
                            ["name"] = functionName,
                            ["response"] = JsonNode.Parse(toolOutput.ToJsonString()) // Deep clone toolOutput
                        }
                    })
                });
            }

            // Send the updated history back to the LLM
            var toolRequest = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}")
            {
                Content = new StringContent(PrepareLLMRequest(tools, contents).ToJsonString(), System.Text.Encoding.UTF8, "application/json")
            };

            var toolResponse = await client.SendAsync(toolRequest);
            toolResponse.EnsureSuccessStatusCode();
            responseBody = await toolResponse.Content.ReadAsStringAsync();
            responseNode = JsonNode.Parse(responseBody);
        }

        return GetSqlFromResponse(responseNode);
    }

    private static JsonObject PrepareLLMRequest(JsonObject tools, List<JsonObject> contents)
    {
        var toolOutputContentsArray = new JsonArray();
        foreach (var item in contents)
        {
            toolOutputContentsArray.Add(JsonNode.Parse(item.ToJsonString()!)!); // Deep clone each item
        }
        var toolOutputContent = new JsonObject
        {
            ["contents"] = toolOutputContentsArray,
            ["tools"] = new JsonArray(JsonNode.Parse(tools.ToJsonString())!)
        };
        return toolOutputContent;
    }

    private static string? GetSqlFromResponse(JsonNode? responseNode)
    {
        var fullText = responseNode?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();

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
        // This regex looks for common SQL keywords at the beginning of a line, followed by any characters until a semicolon or end of string.
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
