using System.Text.Json;
using System.Text.Json.Nodes;

namespace HockeyStatsAI.Services;

public class GeminiTranslator(string apiKey, IDatabaseTools databaseTools, HttpClient? httpClient = null)
{
    private readonly IDatabaseTools _databaseTools = databaseTools;
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();

    public async Task<string?> TranslateToSql(string question)
    {
        var prompt =
            $"You are a SQL expert. Your task is to translate a natural language question into a SQL query.\n\n**Your first step is to always call the `ListAllTables()` tool to see the available tables.**\n\nThen, you can use the following tools to help you understand the database schema:\n- GetTableSchema(tableName): Gets the schema for a given table.\n- GetForeignKeys(tableName): Gets the foreign keys for a given table.\n\nUse these tools to explore the schema and then generate the SQL query.\n\nWhen interpreting competition names, please consider both the 'Name' and 'ShortName' columns in the 'Competition' table. If a short, ambiguous term is used (e.g., 'M1M'), assume it refers to the 'ShortName'.\n\nTranslate the following natural language question into a SQL query:\n\nQuestion: {question}\n\nSQL Query:";

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

        var requestContent = new JsonObject
        {
            ["contents"] = new JsonArray(
                new JsonObject
                {
                    ["parts"] = new JsonArray(
                        new JsonObject
                        {
                            ["text"] = prompt
                        })
                }),
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

        while (responseNode?["candidates"]?[0]?["content"]?["parts"]?[0]?["functionCall"]?["name"] != null)
        {
            var functionCall = responseNode?["candidates"]?[0]?["content"]?["parts"]?[0]?["functionCall"];
            var functionName = functionCall?["name"]?.GetValue<string>();
            var functionArgs = functionCall?["args"];



            JsonNode toolOutput;

            Console.WriteLine($"Calling function: {functionName}");
            switch (functionName)
            {
                case "ListAllTables":
                    {
                        var tables = _databaseTools.ListAllTables();
                        toolOutput = new JsonObject { ["tables"] = JsonSerializer.SerializeToNode(tables) };
                        break;
                    }
                case "GetTableSchema":
                    {
                        var tableName = functionArgs?["tableName"]?.GetValue<string>();
                        var tableSchema = _databaseTools.GetTableSchema(tableName!);
                        toolOutput = new JsonObject { ["schema"] = JsonSerializer.SerializeToNode(tableSchema) };
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

            Console.WriteLine($"Function output: {toolOutput.ToJsonString()}");

            var toolOutputContent = new JsonObject
            {
                ["contents"] = new JsonArray(
                    new JsonObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JsonArray(new JsonObject { ["text"] = prompt })
                    },
                    JsonNode.Parse(responseNode?["candidates"]?[0]?["content"]?.ToJsonString()!),
                    new JsonObject
                    {
                        ["role"] = "tool",
                        ["parts"] = new JsonArray(new JsonObject
                        {
                            ["functionResponse"] = new JsonObject
                            {
                                ["name"] = functionName,
                                ["response"] = toolOutput
                            }
                        })
                    }
                ),
                ["tools"] = new JsonArray(JsonNode.Parse(tools.ToJsonString())!)
            };

            var toolRequest = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}")
            {
                Content = new StringContent(toolOutputContent.ToJsonString(), System.Text.Encoding.UTF8, "application/json")
            };

            var toolResponse = await client.SendAsync(toolRequest);
            toolResponse.EnsureSuccessStatusCode();
            responseBody = await toolResponse.Content.ReadAsStringAsync();
            responseNode = JsonNode.Parse(responseBody);
        }

        return GetSqlFromResponse(responseNode);
    }

    private static string? GetSqlFromResponse(JsonNode? responseNode)
    {
        var sqlQuery = responseNode?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();

        if (sqlQuery?.StartsWith("```sql") ?? false)
        {
            sqlQuery = sqlQuery.Replace("```sql", "").Replace("```", "").Trim();
        }

        return sqlQuery;
    }
}