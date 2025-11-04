using System;

namespace HockeyStatsAI.Models.Schema;

/// <summary>
/// Represents an edge in a join graph, connecting two tables via a foreign key relationship.
/// This model is used by <see cref="Core.Schema.JoinPathFinder"/> to build paths between tables
/// and by <see cref="JoinPlan"/> to generate SQL JOIN clauses. The class implements
/// <see cref="IEquatable{T}"/> to support efficient graph traversal and duplicate detection.
/// </summary>
public sealed class JoinEdge : IEquatable<JoinEdge>
{
	/// <summary>
	/// Gets or sets the source (from) table that contains the foreign key columns.
	/// </summary>
	public TableSchema From { get; set; } = new TableSchema();

	/// <summary>
	/// Gets or sets the target (to) table that contains the referenced primary/unique key columns.
	/// </summary>
	public TableSchema To { get; set; } = new TableSchema();

	/// <summary>
	/// Gets or sets the foreign key relationship that defines how the two tables are joined.
	/// This contains the specific columns and constraint information needed to generate JOIN conditions.
	/// </summary>
	public ForeignKeySchema ForeignKey { get; set; } = new ForeignKeySchema();

	/// <summary>
	/// Determines whether the current instance is equal to another <see cref="JoinEdge"/> instance.
	/// Two edges are considered equal if they connect the same tables using the same foreign key.
	/// Comparison is case-insensitive.
	/// </summary>
	/// <param name="other">The other <see cref="JoinEdge"/> to compare with.</param>
	/// <returns><c>true</c> if the edges are equal; otherwise, <c>false</c>.</returns>
	public bool Equals(JoinEdge? other)
	{
		if (other == null) return false;
		return From.FullName.Equals(other.From.FullName, StringComparison.OrdinalIgnoreCase)
			&& To.FullName.Equals(other.To.FullName, StringComparison.OrdinalIgnoreCase)
			&& ForeignKey.Name.Equals(other.ForeignKey.Name, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Determines whether the current instance is equal to another object.
	/// </summary>
	/// <param name="obj">The object to compare with.</param>
	/// <returns><c>true</c> if the objects are equal; otherwise, <c>false</c>.</returns>
	public override bool Equals(object? obj) => Equals(obj as JoinEdge);

	/// <summary>
	/// Returns a hash code for the current instance.
	/// The hash code is based on the from table, to table, and foreign key name.
	/// </summary>
	/// <returns>A hash code for the current instance.</returns>
	public override int GetHashCode() => HashCode.Combine(From.FullName.ToLowerInvariant(), To.FullName.ToLowerInvariant(), ForeignKey.Name.ToLowerInvariant());
}

