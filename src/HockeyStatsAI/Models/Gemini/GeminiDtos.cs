using System.Text.Json;
using System.Text.Json.Serialization;

namespace HockeyStatsAI.Models.Gemini;

// Main request body sent to the Gemini API
public class GeminiRequest
{
    [JsonPropertyName("contents")]
    public List<Content> Contents { get; set; } = [];

    [JsonPropertyName("tools")]
    public List<Tool> Tools { get; set; } = [];
}

// Main response body from the Gemini API
public class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public List<Candidate> Candidates { get; set; } = [];
}

// Represents a candidate response from the model
public class Candidate
{
    [JsonPropertyName("content")]
    public Content? Content { get; set; }
}

// Represents a piece of content in the conversation history
public class Content
{
    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; set; }

    [JsonPropertyName("parts")]
    public List<Part> Parts { get; set; } = [];
}

// Represents a part of a content block, which can be text, a function call, or a function response
public class Part
{
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("functionCall")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FunctionCall? FunctionCall { get; set; }

    [JsonPropertyName("functionResponse")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FunctionResponse? FunctionResponse { get; set; }
}

// Represents a function call requested by the model
public class FunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("args")]
    public JsonElement Args { get; set; }
}

// Represents the response from a tool execution
public class FunctionResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("response")]
    public JsonElement Response { get; set; }
}

// Represents the tools provided to the model
public class Tool
{
    [JsonPropertyName("function_declarations")]
    public List<FunctionDeclaration> FunctionDeclarations { get; set; } = [];
}

// Represents the declaration of a single function tool
public class FunctionDeclaration
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("parameters")]
    public FunctionParameters Parameters { get; set; } = new();
}

// Represents the parameters for a function
public class FunctionParameters
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, object> Properties { get; set; } = [];

    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Required { get; set; }
}

// Represents a parameter property
public class ParameterProperty
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }
}

