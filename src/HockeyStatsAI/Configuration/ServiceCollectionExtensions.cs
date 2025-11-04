using HockeyStatsAI.Core.Schema;
using HockeyStatsAI.Infrastructure.AI;
using HockeyStatsAI.Infrastructure.AI.Strategies;
using HockeyStatsAI.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using System.Net.Http;

namespace HockeyStatsAI.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHockeyStatsServices(this IServiceCollection services, IConfiguration configuration)
    {
        string apiKey = configuration["GEMINI_API_KEY"] ?? throw new InvalidOperationException("API key not found. Please make sure you have set the GEMINI_API_KEY user secret.");
        string connectionString = configuration["ConnectionStrings:HockeyStatsDb"] ?? throw new InvalidOperationException("Connection string 'HockeyStatsDb' not found in user secrets. Please make sure you have set it.");

        // Database services
        services.AddSingleton<IDatabaseTools>(_ => new DatabaseTools(connectionString));
        int sqlMaxTop = configuration.GetValue<int>("SqlSafety:MaxTop", 200);
        int sqlTimeout = configuration.GetValue<int>("SqlSafety:CommandTimeoutSeconds", 30);
        services.AddSingleton(_ => new SqlExecutor(connectionString, sqlMaxTop, sqlTimeout));

        // Schema services
        int retrievalMaxTables = configuration.GetValue<int>("Retrieval:MaxTables", 4);
        int retrievalMaxColumns = configuration.GetValue<int>("Retrieval:MaxColumnsPerTable", 8);
        int retrievalMaxTokens = configuration.GetValue<int>("Retrieval:MaxTokens", 1800);
        string registryPath = Path.Combine(AppContext.BaseDirectory, "schema-registry.json");

        services.AddSingleton(new SchemaRegistry(registryPath));
        services.AddSingleton<SchemaRetriever>();
        services.AddSingleton<JoinPathFinder>();

        // AI services
        services.AddSingleton(new PromptBuilder(retrievalMaxTokens, retrievalMaxTables, retrievalMaxColumns));

        // HTTP client with retry and timeout policies
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => (int)msg.StatusCode == 429)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromMilliseconds(250 * retryAttempt));

        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30));

        services
            .AddHttpClient("Gemini")
            .AddPolicyHandler(retryPolicy)
            .AddPolicyHandler(timeoutPolicy);

        // Register translation strategy (TargetedTranslationStrategy is the default)
        services.AddSingleton<ISqlTranslationStrategy>(sp =>
        {
            var db = sp.GetRequiredService<IDatabaseTools>();
            var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Gemini");
            var registry = sp.GetRequiredService<SchemaRegistry>();
            var retriever = sp.GetRequiredService<SchemaRetriever>();
            var joinFinder = sp.GetRequiredService<JoinPathFinder>();
            var promptBuilder = sp.GetRequiredService<PromptBuilder>();
            return new TargetedTranslationStrategy(
                apiKey,
                client,
                db,
                registry,
                retriever,
                joinFinder,
                promptBuilder,
                retrievalMaxTables,
                retrievalMaxColumns);
        });

        // Register GeminiTranslator (uses the strategy)
        services.AddSingleton<GeminiTranslator>(sp =>
        {
            var strategy = sp.GetRequiredService<ISqlTranslationStrategy>();
            var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Gemini");
            return new GeminiTranslator(strategy, apiKey, client);
        });

        return services;
    }
}

