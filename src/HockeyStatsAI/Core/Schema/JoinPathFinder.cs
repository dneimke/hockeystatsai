using HockeyStatsAI.Models.Schema;

namespace HockeyStatsAI.Core.Schema;

/// <summary>
/// Finds optimal join paths between database tables using breadth-first search (BFS) algorithm.
/// Used by <see cref="TargetedTranslationStrategy"/> to determine how to connect multiple tables
/// in SQL queries based on foreign key relationships.
/// </summary>
/// <remarks>
/// This class is registered as a singleton in <see cref="Configuration.ServiceCollectionExtensions.AddHockeyStatsServices"/>.
/// It analyzes the database schema to build an adjacency map of table relationships and uses BFS
/// to find the shortest path connecting all selected tables through foreign key relationships.
/// </remarks>
public sealed class JoinPathFinder
{
	private readonly SchemaRegistry _registry;

	/// <summary>
	/// Initializes a new instance of the <see cref="JoinPathFinder"/> class.
	/// </summary>
	/// <param name="registry">The schema registry containing the database schema with table and foreign key information.</param>
	public JoinPathFinder(SchemaRegistry registry)
	{
		_registry = registry;
	}

	/// <summary>
	/// Finds an optimal join plan to connect the specified tables using foreign key relationships.
	/// </summary>
	/// <param name="selectedTables">The list of tables that need to be joined together.</param>
	/// <returns>
	/// A <see cref="JoinPlan"/> containing the selected tables and the join edges (foreign keys)
	/// needed to connect them. If only one table is provided, returns a plan with no joins.
	/// If a table is unreachable, it may be excluded from the join plan.
	/// </returns>
	/// <remarks>
	/// This method uses a breadth-first search algorithm starting from the first table in the list.
	/// It builds an adjacency map from the schema's foreign keys and finds the shortest path
	/// to connect all selected tables. The algorithm ensures all tables are reachable from
	/// the starting table through foreign key relationships.
	/// </remarks>
	public JoinPlan FindJoinPlan(IReadOnlyList<TableSchema> selectedTables)
	{
		if (selectedTables.Count <= 1)
		{
			return new JoinPlan
			{
				Tables = selectedTables.ToList(),
				Joins = new List<JoinEdge>()
			};
		}

		// Build adjacency map
		var tableByName = (_registry.Schema?.Tables ?? []).ToDictionary(t => t.FullName, StringComparer.OrdinalIgnoreCase);
		var neighbors = new Dictionary<string, List<JoinEdge>>(StringComparer.OrdinalIgnoreCase);
		foreach (var t in tableByName.Values)
		{
			var list = neighbors.GetValueOrDefault(t.FullName) ?? new List<JoinEdge>();
			foreach (var fk in t.ForeignKeys)
			{
				var toFull = fk.ToFullName;
				if (tableByName.ContainsKey(toFull))
				{
					list.Add(new JoinEdge
					{
						From = t,
						To = tableByName[toFull],
						ForeignKey = fk
					});
				}
			}
			neighbors[t.FullName] = list;
		}

		// BFS to cover all selected tables starting from the first
		var start = selectedTables[0].FullName;
		var need = new HashSet<string>(selectedTables.Select(t => t.FullName), StringComparer.OrdinalIgnoreCase);
		var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start };
		var queue = new Queue<string>();
		var parent = new Dictionary<string, (string parent, JoinEdge edge)>(StringComparer.OrdinalIgnoreCase);
		queue.Enqueue(start);

		while (queue.Count > 0 && need.Except(visited).Any())
		{
			var current = queue.Dequeue();
			foreach (var edge in neighbors.GetValueOrDefault(current) ?? new List<JoinEdge>())
			{
				var next = edge.To.FullName;
				if (visited.Contains(next)) continue;
				visited.Add(next);
				parent[next] = (current, edge);
				queue.Enqueue(next);
			}
		}

		// Reconstruct edges to connect all needed tables
		var joinEdges = new List<JoinEdge>();
		foreach (var target in need.Where(t => !t.Equals(start, StringComparison.OrdinalIgnoreCase)))
		{
			if (!parent.ContainsKey(target))
			{
				continue; // Unreachable in MVP; caller may fall back to fewer tables
			}
			var cur = target;
			while (!cur.Equals(start, StringComparison.OrdinalIgnoreCase))
			{
				var (p, e) = parent[cur];
				joinEdges.Add(e);
				cur = p;
			}
		}

		// Remove duplicates (if any) and preserve order
		joinEdges = joinEdges.Distinct().ToList();

		return new JoinPlan
		{
			Tables = selectedTables.ToList(),
			Joins = joinEdges
		};
	}
}

