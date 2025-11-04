namespace HockeyStatsAI.Models.Schema;

/// <summary>
/// Represents the schema metadata for a single database column.
/// This model is used throughout the application to understand column structure,
/// data types, and relationships for SQL query generation and schema retrieval.
/// </summary>
public sealed class ColumnSchema
{
	/// <summary>
	/// Gets or sets the name of the column as it appears in the database.
	/// </summary>
	public string ColumnName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the SQL data type of the column (e.g., "int", "varchar(50)", "datetime").
	/// </summary>
	public string DataType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether the column allows NULL values.
	/// </summary>
	public bool IsNullable { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this column is part of the primary key.
	/// </summary>
	public bool IsPrimaryKey { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this column participates in a foreign key relationship.
	/// </summary>
	public bool IsForeignKey { get; set; }

	/// <summary>
	/// Gets or sets an optional summary description of the column's purpose and content.
	/// This is used by <see cref="Core.Schema.SchemaRetriever"/> to match columns against user queries.
	/// </summary>
	public string? ColumnSummary { get; set; }
}

