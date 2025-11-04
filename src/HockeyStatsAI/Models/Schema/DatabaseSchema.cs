using System;
using System.Collections.Generic;

namespace HockeyStatsAI.Models.Schema;

/// <summary>
/// Represents the complete schema metadata for an entire database.
/// This is the root model that contains all tables and synonyms for the database.
/// It is persisted by <see cref="Core.Schema.SchemaRegistry"/> and used throughout
/// the application for schema-aware query generation and retrieval.
/// </summary>
public sealed class DatabaseSchema
{
	/// <summary>
	/// Gets or sets the name of the SQL Server instance where this database resides.
	/// </summary>
	public string ServerName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the name of the database.
	/// </summary>
	public string DatabaseName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the list of all tables in the database.
	/// Each table contains its columns, primary keys, and foreign key relationships.
	/// </summary>
	public List<TableSchema> Tables { get; set; } = new List<TableSchema>();

	/// <summary>
	/// Gets or sets a dictionary of synonyms that map alternative names to table names.
	/// This allows users to refer to tables by common names (e.g., "players" instead of "tblPlayer")
	/// and helps <see cref="Core.Schema.SchemaRetriever"/> match user queries to the correct tables.
	/// The dictionary uses case-insensitive key comparison.
	/// </summary>
	public Dictionary<string, string> Synonyms { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

