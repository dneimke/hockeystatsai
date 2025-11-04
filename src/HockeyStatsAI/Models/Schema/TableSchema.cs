namespace HockeyStatsAI.Models.Schema;

/// <summary>
/// Represents the schema metadata for a database table.
/// This model is central to the application's schema understanding and is used by
/// <see cref="Core.Schema.SchemaRetriever"/> for finding relevant tables based on user queries,
/// and by <see cref="Core.Schema.JoinPathFinder"/> for determining join paths between tables.
/// </summary>
public sealed class TableSchema
{
	/// <summary>
	/// Gets or sets the database schema name (e.g., "dbo", "production"). Defaults to "dbo".
	/// </summary>
	public string SchemaName { get; set; } = "dbo";

	/// <summary>
	/// Gets or sets the name of the table as it appears in the database.
	/// </summary>
	public string TableName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a brief summary description of the table's purpose and contents.
	/// This is used by <see cref="Core.Schema.SchemaRetriever"/> to match tables against user queries
	/// and help the AI understand what data is stored in each table.
	/// </summary>
	public string MicroSummary { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the list of columns contained in this table.
	/// Each column includes its name, data type, and relationship metadata.
	/// </summary>
	public List<ColumnSchema> Columns { get; set; } = new List<ColumnSchema>();

	/// <summary>
	/// Gets or sets the list of column names that form the primary key for this table.
	/// Used for understanding table relationships and generating proper JOIN conditions.
	/// </summary>
	public List<string> PrimaryKeyColumnNames { get; set; } = new List<string>();

	/// <summary>
	/// Gets or sets the list of foreign key relationships where this table is the source (from table).
	/// These relationships are used by <see cref="Core.Schema.JoinPathFinder"/> to build join paths
	/// between tables when generating SQL queries.
	/// </summary>
	public List<ForeignKeySchema> ForeignKeys { get; set; } = new List<ForeignKeySchema>();

	/// <summary>
	/// Gets the fully qualified table name in the format "SchemaName.TableName".
	/// If SchemaName is empty or whitespace, returns only the TableName.
	/// </summary>
	public string FullName => string.IsNullOrWhiteSpace(SchemaName) ? TableName : SchemaName + "." + TableName;
}

