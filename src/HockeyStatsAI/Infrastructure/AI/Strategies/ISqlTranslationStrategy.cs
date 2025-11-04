namespace HockeyStatsAI.Infrastructure.AI.Strategies;

/// <summary>
/// Strategy for translating natural language questions to SQL queries.
/// </summary>
public interface ISqlTranslationStrategy
{
    /// <summary>
    /// Translates a natural language question into a SQL query.
    /// </summary>
    /// <param name="question">The natural language question to translate.</param>
    /// <returns>The SQL query, or null if translation failed.</returns>
    Task<string?> TranslateAsync(string question);
}

