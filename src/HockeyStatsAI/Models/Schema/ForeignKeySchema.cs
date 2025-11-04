namespace HockeyStatsAI.Models.Schema;

/// <summary>
/// Represents a foreign key relationship between two tables.
/// This model defines how tables are related and is used by <see cref="Core.Schema.JoinPathFinder"/>
/// to determine how to join tables together when generating SQL queries. It supports
/// composite foreign keys (multiple columns) and cross-schema relationships.
/// </summary>
public sealed class ForeignKeySchema
{
	/// <summary>
	/// Gets or sets the name of the foreign key constraint as defined in the database.
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the schema name of the source (referencing) table. Defaults to "dbo".
	/// </summary>
	public string FromSchema { get; set; } = "dbo";

	/// <summary>
	/// Gets or sets the name of the source (referencing) table that contains the foreign key columns.
	/// </summary>
	public string FromTable { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the list of column names in the source table that form the foreign key.
	/// Supports composite foreign keys with multiple columns.
	/// </summary>
	public List<string> FromColumns { get; set; } = new List<string>();

	/// <summary>
	/// Gets or sets the schema name of the target (referenced) table. Defaults to "dbo".
	/// </summary>
	public string ToSchema { get; set; } = "dbo";

	/// <summary>
	/// Gets or sets the name of the target (referenced) table that contains the primary/unique key columns.
	/// </summary>
	public string ToTable { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the list of column names in the target table that are referenced by the foreign key.
	/// These typically correspond to primary key or unique key columns. The order should match
	/// the order of <see cref="FromColumns"/>.
	/// </summary>
	public List<string> ToColumns { get; set; } = new List<string>();

	/// <summary>
	/// Gets the fully qualified name of the source table in the format "SchemaName.TableName".
	/// If SchemaName is empty or whitespace, returns only the TableName.
	/// </summary>
	public string FromFullName => string.IsNullOrWhiteSpace(FromSchema) ? FromTable : FromSchema + "." + FromTable;

	/// <summary>
	/// Gets the fully qualified name of the target table in the format "SchemaName.TableName".
	/// If SchemaName is empty or whitespace, returns only the TableName.
	/// </summary>
	public string ToFullName => string.IsNullOrWhiteSpace(ToSchema) ? ToTable : ToSchema + "." + ToTable;
}

