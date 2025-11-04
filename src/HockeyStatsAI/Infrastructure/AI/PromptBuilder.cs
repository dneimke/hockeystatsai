using System.Text;
using HockeyStatsAI.Models.Schema;

namespace HockeyStatsAI.Infrastructure.AI;

/// <summary>
/// Builds optimized prompts for AI SQL translation by including only relevant schema information.
/// Constructs prompts that include table schemas, column information, and join paths in a format
/// suitable for AI models like Gemini.
/// </summary>
/// <remarks>
/// This class is registered as a singleton in <see cref="Configuration.ServiceCollectionExtensions.AddHockeyStatsServices"/>.
/// It is used by <see cref="TargetedTranslationStrategy"/> to build prompts that contain only
/// the schema information relevant to the user's question, reducing token usage and improving
/// translation accuracy. The class respects limits on the number of tables, columns per table,
/// and total tokens to ensure prompts stay within model limits.
/// </remarks>
public sealed class PromptBuilder
{
	private readonly int _maxTokens;
	private readonly int _maxTables;
	private readonly int _maxColumnsPerTable;

	/// <summary>
	/// Initializes a new instance of the <see cref="PromptBuilder"/> class.
	/// </summary>
	/// <param name="maxTokens">The maximum number of tokens allowed in the generated prompt (used for truncation).</param>
	/// <param name="maxTables">The maximum number of tables to include in the prompt.</param>
	/// <param name="maxColumnsPerTable">The maximum number of columns to include per table.</param>
	public PromptBuilder(int maxTokens, int maxTables, int maxColumnsPerTable)
	{
		_maxTokens = maxTokens;
		_maxTables = maxTables;
		_maxColumnsPerTable = maxColumnsPerTable;
	}

	/// <summary>
	/// Builds a prompt for AI SQL translation with relevant schema information.
	/// </summary>
	/// <param name="question">The natural language question to translate.</param>
	/// <param name="tables">The relevant tables to include in the prompt.</param>
	/// <param name="columnsByTable">A dictionary mapping table full names to their relevant columns.</param>
	/// <param name="joinPlan">The join plan showing how tables should be connected.</param>
	/// <returns>
	/// A formatted prompt string containing context, table schemas, join paths, rules, and the user question.
	/// The prompt is truncated if it exceeds the maximum token limit.
	/// </returns>
	/// <remarks>
	/// The generated prompt includes:
	/// - Instructions for the AI to act as a SQL Server expert
	/// - Context section with table names, summaries, and column information
	/// - Join path section showing how to connect tables (if multiple tables)
	/// - Rules for SQL generation (aliases, TOP clauses, safety, etc.)
	/// - The user's question
	/// This prompt is sent to the Gemini API via <see cref="TargetedTranslationStrategy"/> to generate SQL queries.
	/// </remarks>
	public string BuildPrompt(
		string question,
		IReadOnlyList<TableSchema> tables,
		Dictionary<string, IReadOnlyList<ColumnSchema>> columnsByTable,
		JoinPlan joinPlan)
	{
		var sb = new StringBuilder();
		sb.AppendLine("You are a SQL Server expert. Produce a single safe T-SQL SELECT query and nothing else.");
		sb.AppendLine();
		sb.AppendLine("Context:");
		for (int i = 0; i < Math.Min(_maxTables, tables.Count); i++)
		{
			var t = tables[i];
			sb.AppendLine("- Table: " + t.FullName + " — " + t.MicroSummary);
			if (columnsByTable.TryGetValue(t.FullName, out var cols))
			{
				foreach (var c in cols.Take(_maxColumnsPerTable))
				{
					sb.AppendLine("  - " + c.ColumnName + ": " + c.DataType + (c.ColumnSummary != null ? " — " + c.ColumnSummary : string.Empty));
				}
			}
		}

		if (joinPlan.Joins.Count > 0)
		{
			sb.AppendLine();
			sb.AppendLine("Join path:");
			var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < tables.Count; i++)
			{
				aliases[tables[i].FullName] = "t" + i;
			}
			for (int i = 1; i < tables.Count; i++)
			{
				sb.AppendLine("- JOIN " + tables[i].FullName + " AS " + aliases[tables[i].FullName] + " ON " + string.Join(" AND ", joinPlan.ToOnClauses().Take(i)));
			}
		}

		sb.AppendLine();
		sb.AppendLine("Rules:");
		sb.AppendLine("- Use explicit table aliases (t0, t1, ...).");
		sb.AppendLine("- Avoid SELECT *; include only necessary columns.");
		sb.AppendLine("- Use TOP 200 unless a smaller TOP is specified by the user.");
		sb.AppendLine("- Prefer TRY_CONVERT/CAST for date filters and use safe defaults.");
		sb.AppendLine("- Do not modify data; SELECT only.");
		sb.AppendLine("- When filtering Club or Competition tables by name (e.g., WHERE clause with a name value), check both Name and ShortName columns using OR (e.g., WHERE (t0.Name = N'value' OR t0.ShortName = N'value')).");

		sb.AppendLine();
		sb.AppendLine("User question:");
		sb.AppendLine(question);

		// Truncate if very long (simple cap in MVP)
		var result = sb.ToString();
		if (result.Length > _maxTokens * 4)
		{
			result = result.Substring(0, _maxTokens * 4);
		}
		return result;
	}
}

