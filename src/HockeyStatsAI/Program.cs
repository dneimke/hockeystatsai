using HockeyStatsAI.Configuration;
using HockeyStatsAI.Core.Validation;
using HockeyStatsAI.Infrastructure.AI;
using HockeyStatsAI.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets("483c4b56-8150-4e01-8c92-04ca08cd2129");

builder.Logging.AddConsole();

builder.Services.AddHockeyStatsServices(builder.Configuration);

using var host = builder.Build();

using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;
var loggerFactory = services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();
var logger = loggerFactory.CreateLogger("HockeyStatsAI");
var sqlExecutor = services.GetRequiredService<SqlExecutor>();
var databaseTools = services.GetRequiredService<IDatabaseTools>();
var geminiTranslator = services.GetRequiredService<GeminiTranslator>();

Console.WriteLine("Welcome to the HockeyStats Natural Language Query Tool!");

Console.WriteLine("Here are some example questions you can ask:");
Console.WriteLine("- What tables are in the database?");
Console.WriteLine("- How many teams played in M1M in the 2023 season?");
Console.WriteLine("- List all clubs.");
Console.WriteLine("- What are the names of the competitions?");
Console.WriteLine("Commands: dryrun on|off, limit N, format table|csv|json, explain, rerun");

var currentFormat = SqlExecutor.OutputFormat.Table;
bool dryRun = false;
int? limitOverride = null;
string? lastSql = null;

while (true)
{
    Console.Write("\nEnter your question (or type 'exit' to quit): ");

    string? question = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(question))
    {
        continue;
    }

    if (question.Equals("exit", StringComparison.OrdinalIgnoreCase) || question.Equals("quit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    // Commands
    var parts = question.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var cmd = parts[0].ToLowerInvariant();
    if (cmd == "dryrun")
    {
        if (parts.Length > 1 && parts[1].Equals("on", StringComparison.OrdinalIgnoreCase)) dryRun = true;
        else if (parts.Length > 1 && parts[1].Equals("off", StringComparison.OrdinalIgnoreCase)) dryRun = false;
        Console.WriteLine($"dryrun = {dryRun}");
        continue;
    }
    if (cmd == "limit")
    {
        if (parts.Length > 1 && int.TryParse(parts[1], out var n) && n > 0) limitOverride = n; else limitOverride = null;
        Console.WriteLine($"limit = {(limitOverride?.ToString() ?? "default")}");
        continue;
    }
    if (cmd == "format")
    {
        if (parts.Length > 1)
        {
            var fmt = parts[1].ToLowerInvariant();
            currentFormat = fmt switch
            {
                "csv" => SqlExecutor.OutputFormat.Csv,
                "json" => SqlExecutor.OutputFormat.Json,
                _ => SqlExecutor.OutputFormat.Table
            };
        }
        Console.WriteLine($"format = {currentFormat}");
        continue;
    }
    if (cmd == "rerun")
    {
        if (!string.IsNullOrWhiteSpace(lastSql))
        {
            if (!SqlSafetyValidator.IsSafeSelect(lastSql, out var violation))
            {
                Console.WriteLine($"Blocked unsafe SQL: {violation}");
                continue;
            }
            if (dryRun)
            {
                Console.WriteLine($"SQL Query (dryrun): {lastSql}");
                continue;
            }
            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            sqlExecutor.ExecuteQuery(lastSql, currentFormat, limitOverride);
            sw2.Stop();
            logger.LogInformation("ExecutionMs: {Elapsed}", sw2.ElapsedMilliseconds);
        }
        else
        {
            Console.WriteLine("No previous query to rerun.");
        }
        continue;
    }
    if (cmd == "explain")
    {
        var sqlToExplain = parts.Length > 1 ? parts[1] : lastSql;
        if (string.IsNullOrWhiteSpace(sqlToExplain))
        {
            Console.WriteLine("Nothing to explain.");
            continue;
        }
        var explainPrompt = $"Explain in plain English what this SQL does, succinctly.\n\n```sql\n{sqlToExplain}\n```";
        var explanation = await geminiTranslator.GetSummary(explainPrompt);
        Console.WriteLine(explanation);
        continue;
    }

    logger.LogInformation("Question: {Question}", question);
    string? query = await geminiTranslator.TranslateToSqlTargeted(question);
    logger.LogInformation("SQL: {Sql}", query);

    if (query != null)
    {
        if (!SqlSafetyValidator.IsSafeSelect(query, out var violation))
        {
            Console.WriteLine($"Blocked unsafe SQL: {violation}");
            continue;
        }

        lastSql = query;
        if (dryRun)
        {
            Console.WriteLine($"SQL Query (dryrun): {query}");
            continue;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        sqlExecutor.ExecuteQuery(query, currentFormat, limitOverride);
        sw.Stop();
        logger.LogInformation("ExecutionMs: {Elapsed}", sw.ElapsedMilliseconds);
    }
    else
    {
        Console.WriteLine("Could not translate question to SQL.");
    }
}

Console.WriteLine("Thank you for using the HockeyStats Natural Language Query Tool!");
