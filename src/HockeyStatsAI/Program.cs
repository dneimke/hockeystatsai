using HockeyStatsAI.Services;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .Build();

string apiKey = configuration["GEMINI_API_KEY"] ?? throw new InvalidOperationException("API key not found. Please make sure you have set the GEMINI_API_KEY user secret.");
string connectionString = configuration["ConnectionStrings:HockeyStatsDb"] ?? throw new InvalidOperationException("Connection string 'HockeyStatsDb' not found in user secrets. Please make sure you have set it.");

Console.WriteLine("Using connection string: " + connectionString);
Console.WriteLine("Using API Key: " + new string('*', apiKey.Length));

var sqlExecutor = new SqlExecutor(connectionString);
var databaseTools = new DatabaseTools(connectionString);
var geminiTranslator = new GeminiTranslator(apiKey, databaseTools);

Console.WriteLine("Welcome to the HockeyStats Natural Language Query Tool!");

Console.WriteLine("Here are some example questions you can ask:");
Console.WriteLine("- What tables are in the database?");
Console.WriteLine("- How many teams played in M1M in the 2023 season?");
Console.WriteLine("- List all clubs.");
Console.WriteLine("- What are the names of the competitions?");

while (true)
{
    Console.Write("\nEnter your question (or type 'exit' to quit): ");

    string? question = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(question) || question.Equals("exit", StringComparison.OrdinalIgnoreCase) || question.Equals("quit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    string? query = await geminiTranslator.TranslateToSql(question);
    Console.WriteLine($"SQL Query: {query}");

    if (query != null)
    {
        sqlExecutor.ExecuteQuery(query);
    }
    else
    {
        Console.WriteLine("Could not translate question to SQL.");
    }
}

Console.WriteLine("Thank you for using the HockeyStats Natural Language Query Tool!");
