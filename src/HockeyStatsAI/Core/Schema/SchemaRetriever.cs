using System.Text.RegularExpressions;
using HockeyStatsAI.Models.Schema;

namespace HockeyStatsAI.Core.Schema;

/// <summary>
/// Retrieves relevant tables and columns from the database schema based on natural language questions.
/// Uses a scoring algorithm to rank tables and columns by relevance to the question.
/// </summary>
/// <remarks>
/// This class is registered as a singleton in <see cref="Configuration.ServiceCollectionExtensions.AddHockeyStatsServices"/>.
/// It is used by <see cref="TargetedTranslationStrategy"/> to pre-filter the schema before sending
/// it to the AI model, reducing token usage and improving translation accuracy.
/// The scoring algorithm assigns weights to different types of matches:
/// - Exact table name matches: highest weight (5 points)
/// - Synonym matches: medium weight (3 points)
/// - Column name matches: lower weight (1.25 points)
/// - Summary keyword matches: lowest weight (0.25 points)
/// </remarks>
public sealed class SchemaRetriever
{
	private readonly SchemaRegistry _registry;

	/// <summary>
	/// Initializes a new instance of the <see cref="SchemaRetriever"/> class.
	/// </summary>
	/// <param name="registry">The schema registry containing the database schema to search.</param>
	public SchemaRetriever(SchemaRegistry registry)
	{
		_registry = registry;
	}

	/// <summary>
	/// Gets the most relevant tables for a natural language question using a scoring algorithm.
	/// </summary>
	/// <param name="question">The natural language question to analyze.</param>
	/// <param name="maxTables">The maximum number of tables to return.</param>
	/// <returns>
	/// A list of the most relevant tables, sorted by relevance score in descending order.
	/// Returns at least one table if any matches are found.
	/// </returns>
	/// <remarks>
	/// The scoring algorithm:
	/// - +5 points for exact table name matches
	/// - +5 points for full name (schema.table) matches
	/// - +3 points for synonym matches (e.g., "team" matches "CompetitionTeam")
	/// - +1.25 points per matching column name
	/// - +0.25 points per matching keyword in the table's micro-summary
	/// Only tables with a score > 0 are returned, sorted by score descending.
	/// </remarks>
	public IReadOnlyList<TableSchema> GetRelevantTables(string question, int maxTables)
	{
		var schema = _registry.Schema ?? throw new InvalidOperationException("Schema not loaded");
		var tokens = Tokenize(question);

		var scores = new List<(TableSchema table, double score)>();
		foreach (var table in schema.Tables)
		{
			double score = 0;
			// Exact table name hits
			if (Contains(tokens, table.TableName)) score += 5;
			if (Contains(tokens, table.SchemaName + "." + table.TableName)) score += 5;

			// Synonyms
			foreach (var kvp in schema.Synonyms)
			{
				if (Contains(tokens, kvp.Key) && kvp.Value.Equals(table.TableName, StringComparison.OrdinalIgnoreCase))
				{
					score += 3;
				}
			}

			// Column name hints
			foreach (var c in table.Columns)
			{
				if (Contains(tokens, c.ColumnName)) score += 1.25;
			}

			// Summary keyword hits
			if (!string.IsNullOrWhiteSpace(table.MicroSummary))
			{
				foreach (var token in tokens)
				{
					if (table.MicroSummary.Contains(token, StringComparison.OrdinalIgnoreCase)) score += 0.25;
				}
			}

			if (score > 0)
			{
				scores.Add((table, score));
			}
		}

		return scores
			.OrderByDescending(s => s.score)
			.Take(Math.Max(1, maxTables))
			.Select(s => s.table)
			.ToList();
	}

	/// <summary>
	/// Gets the most relevant columns for a table based on a natural language question.
	/// </summary>
	/// <param name="table">The table to get columns from.</param>
	/// <param name="question">The natural language question to analyze.</param>
	/// <param name="maxColumnsPerTable">The maximum number of columns to return per table.</param>
	/// <returns>
	/// A list of relevant columns, including all primary key and foreign key columns (for joinability)
	/// plus the top-scoring columns up to the maximum limit.
	/// </returns>
	/// <remarks>
	/// The scoring algorithm:
	/// - +3 points for exact column name matches
	/// - +0.25 points per matching keyword in the column summary
	/// - +0.1 points for primary key columns (preference for joinability)
	/// - +0.2 points for foreign key columns (preference for joinability)
	/// Primary key and foreign key columns are always included regardless of score to ensure
	/// tables can be properly joined together.
	/// </remarks>
	public IReadOnlyList<ColumnSchema> GetRelevantColumns(TableSchema table, string question, int maxColumnsPerTable)
	{
		var tokens = Tokenize(question);
		var scored = new List<(ColumnSchema column, double score)>();
		foreach (var c in table.Columns)
		{
			double score = 0;
			if (Contains(tokens, c.ColumnName)) score += 3;
			if (!string.IsNullOrWhiteSpace(c.ColumnSummary))
			{
				foreach (var token in tokens)
				{
					if (c.ColumnSummary.Contains(token, StringComparison.OrdinalIgnoreCase)) score += 0.25;
				}
			}
			if (c.IsPrimaryKey) score += 0.1; // slight preference to keep joinability
			if (c.IsForeignKey) score += 0.2;
			scored.Add((c, score));
		}

		// Always include PK/FK columns used for joins
		var essentials = table.Columns.Where(c => c.IsPrimaryKey || c.IsForeignKey).ToList();
		var selected = scored.OrderByDescending(s => s.score)
			.Take(Math.Max(1, maxColumnsPerTable))
			.Select(s => s.column)
			.Union(essentials)
			.Distinct()
			.ToList();

		return selected;
	}

	/// <summary>
	/// Checks if any of the tokens match the given term (case-insensitive).
	/// </summary>
	/// <param name="tokens">The list of tokens to search.</param>
	/// <param name="term">The term to find.</param>
	/// <returns>True if any token matches the term.</returns>
	private static bool Contains(IEnumerable<string> tokens, string term)
	{
		return tokens.Any(t => t.Equals(term, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Tokenizes text by extracting alphanumeric words (length > 1) and replacing underscores with spaces.
	/// </summary>
	/// <param name="text">The text to tokenize.</param>
	/// <returns>A list of tokens extracted from the text.</returns>
	/// <remarks>
	/// This method converts underscores to spaces and extracts words using regex,
	/// filtering out single-character tokens to avoid matching common words like "a" or "I".
	/// </remarks>
	private static List<string> Tokenize(string text)
	{
		text = text.Replace("_", " ");
		var tokens = Regex.Matches(text, "[A-Za-z0-9]+")
			.Select(m => m.Value)
			.Where(s => s.Length > 1)
			.ToList();
		return tokens;
	}
}

