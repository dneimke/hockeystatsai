using System;
using System.Collections.Generic;

namespace HockeyStatsAI.Models.Schema;

/// <summary>
/// Represents a plan for joining multiple tables together in a SQL query.
/// This model is created by <see cref="Core.Schema.JoinPathFinder"/> to define how tables
/// should be joined based on their foreign key relationships. The <see cref="ToOnClauses"/> method
/// generates SQL ON clauses that can be used in SQL JOIN statements.
/// </summary>
public sealed class JoinPlan
{
	/// <summary>
	/// Gets or sets the ordered list of tables to be joined in the query.
	/// The first table is typically the base table, and subsequent tables are joined to it
	/// through the relationships defined in <see cref="Joins"/>.
	/// </summary>
	public List<TableSchema> Tables { get; set; } = new List<TableSchema>();

	/// <summary>
	/// Gets or sets the list of join edges that define how the tables are connected.
	/// Each edge represents a foreign key relationship between two tables in the <see cref="Tables"/> list.
	/// </summary>
	public List<JoinEdge> Joins { get; set; } = new List<JoinEdge>();

	/// <summary>
	/// Converts the join plan into SQL ON clauses that can be used in JOIN statements.
	/// Each join edge is converted to an ON clause with appropriate table aliases.
	/// </summary>
	/// <param name="leftAliasPrefix">The prefix to use for table aliases (e.g., "t" produces "t0", "t1", etc.). Defaults to "t".</param>
	/// <returns>An enumerable collection of SQL ON clause strings, one for each join edge.</returns>
	/// <example>
	/// <code>
	/// // For a join between Players and Teams tables:
	/// // Returns: ["t0.PlayerId = t1.PlayerId"]
	/// </code>
	/// </example>
	public IEnumerable<string> ToOnClauses(string leftAliasPrefix = "t")
	{
		var onClauses = new List<string>();
		var aliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		for (int i = 0; i < Tables.Count; i++)
		{
			aliasMap[Tables[i].FullName] = leftAliasPrefix + i;
		}
		foreach (var e in Joins)
		{
			var leftAlias = aliasMap[e.From.FullName];
			var rightAlias = aliasMap[e.To.FullName];
			var parts = new List<string>();
			for (int i = 0; i < e.ForeignKey.FromColumns.Count; i++)
			{
				parts.Add(leftAlias + "." + e.ForeignKey.FromColumns[i] + " = " + rightAlias + "." + e.ForeignKey.ToColumns[i]);
			}
			onClauses.Add(string.Join(" AND ", parts));
		}
		return onClauses;
	}
}

