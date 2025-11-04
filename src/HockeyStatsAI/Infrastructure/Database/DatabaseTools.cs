using Microsoft.Data.SqlClient;
using System.Data;
using HockeyStatsAI.Models.Schema;
using System.Threading.Tasks;

namespace HockeyStatsAI.Infrastructure.Database;

/// <summary>
/// Provides database introspection tools for schema discovery and loading.
/// Implements <see cref="IDatabaseTools"/> and adds additional methods for building complete database schemas.
/// </summary>
/// <remarks>
/// This class is registered as a singleton implementation of <see cref="IDatabaseTools"/> in
/// <see cref="Configuration.ServiceCollectionExtensions.AddHockeyStatsServices"/>.
/// It is used by:
/// - <see cref="SchemaRegistry.LoadOrBuildAsync"/> to build the database schema from scratch
/// - <see cref="TargetedTranslationStrategy"/> to load schema when cache is not available
/// The class queries SQL Server system views (INFORMATION_SCHEMA and sys.*) to introspect
/// the database structure, including tables, columns, primary keys, and foreign keys.
/// </remarks>
public class DatabaseTools(string connectionString) : IDatabaseTools
{
    private readonly string _connectionString = connectionString;

    /// <summary>
    /// Whitelist of allowed table names for the schema registry.
    /// Only tables in this set will be included when building the schema.
    /// </summary>
    private static readonly HashSet<string> AllowedTableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Association",
        "Club",
        "Competition",
        "CompetitionFixture",
        "CompetitionSeason",
        "CompetitionTeam",
        "FixtureRevision",
        "Location",
        "Player",
        "PlayerRegistration",
        "PlayerStatistics",
        "PlayerTransfer",
        "Round",
        "RoundType"
    };

    /// <inheritdoc/>
    public IEnumerable<string> ListAllTables()
    {
        var tables = new List<string>();
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_CATALOG = 'hockeystats'", connection);
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var tableName = reader.GetString(0);
            // Console.WriteLine($"Found table: {tableName}");
            tables.Add(tableName);
        }

        return tables;
    }

    /// <summary>
    /// Represents a row of table schema information (column name, data type, primary key status).
    /// </summary>
    public class TableSchemaRow
    {
        /// <summary>
        /// Gets or sets the column name.
        /// </summary>
        public string? ColumnName { get; set; }

        /// <summary>
        /// Gets or sets the SQL Server data type name.
        /// </summary>
        public string? DataType { get; set; }

        /// <summary>
        /// Gets or sets whether this column is part of the primary key.
        /// </summary>
        public bool IsPrimaryKey { get; set; }
    }

    /// <inheritdoc/>
    public IEnumerable<dynamic> GetTableSchema(string tableName)
    {
        var schema = new List<TableSchemaRow>();
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        var columns = connection.GetSchema("Columns", new[] { null, null, tableName });
        foreach (DataRow row in columns.Rows)
        {
            schema.Add(new TableSchemaRow
            {
                ColumnName = row["COLUMN_NAME"].ToString(),
                DataType = row["DATA_TYPE"].ToString(),
                IsPrimaryKey = false
            });
        }

        using var pkCommand = new SqlCommand(@"
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
            WHERE OBJECTPROPERTY(OBJECT_ID(CONSTRAINT_SCHEMA + '.' + QUOTENAME(CONSTRAINT_NAME)), 'IsPrimaryKey') = 1
            AND TABLE_NAME = @tableName", connection);
        pkCommand.Parameters.Add(new SqlParameter("@tableName", SqlDbType.NVarChar, 128) { Value = tableName });

        using var pkReader = pkCommand.ExecuteReader();
        while (pkReader.Read())
        {
            var columnName = pkReader.GetString(0);
            var column = schema.FirstOrDefault(c => c.ColumnName == columnName);
            if (column != null)
            {
                column.IsPrimaryKey = true;
            }
        }

        return schema;
    }

    /// <summary>
    /// Represents a foreign key relationship between two tables.
    /// </summary>
    public class ForeignKeyRow
    {
        /// <summary>
        /// Gets or sets the foreign key table name (the table containing the FK column).
        /// </summary>
        public string? FkTable { get; set; }

        /// <summary>
        /// Gets or sets the foreign key column name.
        /// </summary>
        public string? FkColumn { get; set; }

        /// <summary>
        /// Gets or sets the primary key table name (the referenced table).
        /// </summary>
        public string? PkTable { get; set; }

        /// <summary>
        /// Gets or sets the primary key column name (the referenced column).
        /// </summary>
        public string? PkColumn { get; set; }
    }

    /// <inheritdoc/>
    public IEnumerable<dynamic> GetForeignKeys(string tableName)
    {
        var foreignKeys = new List<ForeignKeyRow>();
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = new SqlCommand(@"
            SELECT
                fk.TABLE_NAME AS FkTable,
                fk.COLUMN_NAME AS FkColumn,
                pk.TABLE_NAME AS PkTable,
                pk.COLUMN_NAME AS PkColumn
            FROM
                INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS AS rc
            JOIN
                INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS fk
                ON rc.CONSTRAINT_NAME = fk.CONSTRAINT_NAME
            JOIN
                INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS pk
                ON rc.UNIQUE_CONSTRAINT_NAME = pk.CONSTRAINT_NAME
            WHERE
                fk.TABLE_NAME = @tableName OR pk.TABLE_NAME = @tableName
        ", connection);
        command.Parameters.Add(new SqlParameter("@tableName", SqlDbType.NVarChar, 128) { Value = tableName });

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            foreignKeys.Add(new ForeignKeyRow
            {
                FkTable = reader["FkTable"].ToString(),
                FkColumn = reader["FkColumn"].ToString(),
                PkTable = reader["PkTable"].ToString(),
                PkColumn = reader["PkColumn"].ToString()
            });
        }

        return foreignKeys;
    }

    /// <summary>
    /// Loads the complete database schema including tables, columns, primary keys, foreign keys, and metadata.
    /// </summary>
    /// <param name="databaseName">Optional database name. If null, uses the database from the connection string.</param>
    /// <param name="defaultSchema">The default schema name (default: "dbo").</param>
    /// <returns>
    /// A <see cref="DatabaseSchema"/> object containing all tables, columns, relationships, and generated summaries.
    /// </returns>
    /// <remarks>
    /// This method performs comprehensive database introspection:
    /// 1. Retrieves all tables and columns from INFORMATION_SCHEMA
    /// 2. Identifies primary key columns
    /// 3. Identifies foreign key relationships from sys.foreign_keys
    /// 4. Generates micro-summaries for tables based on heuristics
    /// 5. Generates column summaries
    /// 6. Adds synonym mappings for common table name aliases
    /// The result is cached by <see cref="SchemaRegistry"/> to avoid repeated introspection.
    /// This is the main method used to build the schema when the registry cache is not available.
    /// </remarks>
    public async Task<DatabaseSchema> LoadDatabaseSchemaAsync(string? databaseName = null, string defaultSchema = "dbo")
    {
        var schema = new DatabaseSchema
        {
            DatabaseName = databaseName ?? string.Empty,
            ServerName = string.Empty
        };

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        schema.ServerName = connection.DataSource ?? string.Empty;
        if (string.IsNullOrWhiteSpace(schema.DatabaseName))
        {
            schema.DatabaseName = connection.Database;
        }

        // Tables and columns
        using (var cmd = new SqlCommand(@"
SELECT
    t.TABLE_SCHEMA,
    t.TABLE_NAME,
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.IS_NULLABLE
FROM INFORMATION_SCHEMA.TABLES t
JOIN INFORMATION_SCHEMA.COLUMNS c
  ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME
WHERE t.TABLE_TYPE = 'BASE TABLE'", connection))
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            var tableMap = new Dictionary<string, TableSchema>(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadAsync())
            {
                var schemaName = reader.GetString(0);
                var tableName = reader.GetString(1);
                
                // Filter: only include tables in the whitelist
                if (!AllowedTableNames.Contains(tableName))
                {
                    continue;
                }
                
                var key = schemaName + "." + tableName;
                if (!tableMap.TryGetValue(key, out var table))
                {
                    table = new TableSchema
                    {
                        SchemaName = schemaName,
                        TableName = tableName
                    };
                    tableMap[key] = table;
                    schema.Tables.Add(table);
                }

                table.Columns.Add(new ColumnSchema
                {
                    ColumnName = reader.GetString(2),
                    DataType = reader.GetString(3),
                    IsNullable = string.Equals(reader.GetString(4), "YES", StringComparison.OrdinalIgnoreCase)
                });
            }
        }

        // Primary keys
        using (var cmd = new SqlCommand(@"
SELECT KU.TABLE_SCHEMA, KU.TABLE_NAME, KU.COLUMN_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS TC
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS KU
  ON TC.CONSTRAINT_NAME = KU.CONSTRAINT_NAME AND TC.TABLE_SCHEMA = KU.TABLE_SCHEMA AND TC.TABLE_NAME = KU.TABLE_NAME
WHERE TC.CONSTRAINT_TYPE = 'PRIMARY KEY'", connection))
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var schemaName = reader.GetString(0);
                var tableName = reader.GetString(1);
                var columnName = reader.GetString(2);
                var table = FindTable(schema.Tables, schemaName, tableName);
                if (table != null)
                {
                    table.PrimaryKeyColumnNames.Add(columnName);
                    var col = table.Columns.FirstOrDefault(c => c.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));
                    if (col != null)
                    {
                        col.IsPrimaryKey = true;
                    }
                }
            }
        }

        // Foreign keys
        using (var cmd = new SqlCommand(@"
SELECT 
    fk.name AS FK_Name,
    SCHEMA_NAME(src.schema_id) AS FromSchema,
    src.name AS FromTable,
    csrc.name AS FromColumn,
    SCHEMA_NAME(dest.schema_id) AS ToSchema,
    dest.name AS ToTable,
    cdest.name AS ToColumn
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
JOIN sys.tables src ON fkc.parent_object_id = src.object_id
JOIN sys.columns csrc ON csrc.object_id = src.object_id AND csrc.column_id = fkc.parent_column_id
JOIN sys.tables dest ON fkc.referenced_object_id = dest.object_id
JOIN sys.columns cdest ON cdest.object_id = dest.object_id AND cdest.column_id = fkc.referenced_column_id", connection))
        
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            var fkMap = new Dictionary<string, ForeignKeySchema>(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadAsync())
            {
                var fkName = reader.GetString(0);
                var fromSchema = reader.GetString(1);
                var fromTable = reader.GetString(2);
                var fromColumn = reader.GetString(3);
                var toSchema = reader.GetString(4);
                var toTable = reader.GetString(5);
                var toColumn = reader.GetString(6);

                // Filter: skip foreign keys where either from or to table is not in the whitelist
                if (!AllowedTableNames.Contains(fromTable) || !AllowedTableNames.Contains(toTable))
                {
                    continue;
                }

                var table = FindTable(schema.Tables, fromSchema, fromTable);
                if (table == null) continue;

                if (!fkMap.TryGetValue(fkName + "@" + table.FullName, out var fk))
                {
                    fk = new ForeignKeySchema
                    {
                        Name = fkName,
                        FromSchema = fromSchema,
                        FromTable = fromTable,
                        ToSchema = toSchema,
                        ToTable = toTable
                    };
                    fkMap[fkName + "@" + table.FullName] = fk;
                    table.ForeignKeys.Add(fk);
                }

                fk.FromColumns.Add(fromColumn);
                fk.ToColumns.Add(toColumn);

                var col = table.Columns.FirstOrDefault(c => c.ColumnName.Equals(fromColumn, StringComparison.OrdinalIgnoreCase));
                if (col != null) col.IsForeignKey = true;
            }
        }

        // Basic micro-summaries (MVP): based on table name heuristics
        foreach (var t in schema.Tables)
        {
            t.MicroSummary = GenerateMicroSummary(t);
            foreach (var c in t.Columns)
            {
                c.ColumnSummary = GenerateColumnSummary(c);
            }
        }

        // Minimal synonyms (MVP)
        schema.Synonyms["team"] = "CompetitionTeam";
        schema.Synonyms["match"] = "CompetitionFixture";
        schema.Synonyms["game"] = "CompetitionFixture";
        schema.Synonyms["season"] = "CompetitionSeason";
        schema.Synonyms["round"] = "Round";
        schema.Synonyms["player"] = "Player";
        schema.Synonyms["transfer"] = "PlayerTransfer";
        schema.Synonyms["registration"] = "PlayerRegistration";
        schema.Synonyms["club"] = "Club";

        return schema;
    }

    /// <summary>
    /// Finds a table in the list by schema and table name (case-insensitive).
    /// </summary>
    /// <param name="tables">The list of tables to search.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <returns>The matching table, or null if not found.</returns>
    private static TableSchema? FindTable(List<TableSchema> tables, string schemaName, string tableName)
    {
        return tables.FirstOrDefault(t => t.SchemaName.Equals(schemaName, StringComparison.OrdinalIgnoreCase)
            && t.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Generates a micro-summary for a table based on its name and heuristics.
    /// </summary>
    /// <param name="table">The table to generate a summary for.</param>
    /// <returns>A short description of the table's purpose.</returns>
    /// <remarks>
    /// Uses hardcoded summaries for known table names. For unknown tables, generates
    /// a generic description listing column names. These summaries are used by
    /// <see cref="SchemaRetriever"/> to score table relevance based on keyword matching.
    /// </remarks>
    private static string GenerateMicroSummary(TableSchema table)
    {
        var name = table.TableName;
        if (name.Equals("CompetitionFixture", StringComparison.OrdinalIgnoreCase)) return "Matches with home/away teams, season, round, time, scores, result flags.";
        if (name.Equals("CompetitionSeason", StringComparison.OrdinalIgnoreCase)) return "Seasons by competition with year, current flag, and round info.";
        if (name.Equals("CompetitionTeam", StringComparison.OrdinalIgnoreCase)) return "Teams per club per competition season.";
        if (name.Equals("Player", StringComparison.OrdinalIgnoreCase)) return "Players: names, current club.";
        if (name.Equals("PlayerRegistration", StringComparison.OrdinalIgnoreCase)) return "Player↔club associations with start/end/year.";
        if (name.Equals("PlayerStatistics", StringComparison.OrdinalIgnoreCase)) return "Per-player stats per fixture (goals, cards, position).";
        if (name.Equals("PlayerTransfer", StringComparison.OrdinalIgnoreCase)) return "Transfers: player, from/to club, date, number.";
        if (name.Equals("Round", StringComparison.OrdinalIgnoreCase)) return "Rounds within competition seasons (number, type).";
        if (name.Equals("RoundType", StringComparison.OrdinalIgnoreCase)) return "Round types with final flag and sort order.";
        if (name.Equals("Club", StringComparison.OrdinalIgnoreCase)) return "Clubs with names and association.";
        if (name.Equals("Association", StringComparison.OrdinalIgnoreCase)) return "Associations with names and audit fields.";
        if (name.Equals("Location", StringComparison.OrdinalIgnoreCase)) return "Venues/locations with address and coordinates.";
        if (name.Equals("Competition", StringComparison.OrdinalIgnoreCase)) return "Competitions with association, sort order, names.";
        return "Table with columns: " + string.Join(", ", table.Columns.Select(c => c.ColumnName));
    }

    /// <summary>
    /// Generates a summary for a column based on its name and data type.
    /// </summary>
    /// <param name="column">The column to generate a summary for.</param>
    /// <returns>A short description of the column (name and data type).</returns>
    /// <remarks>
    /// Currently returns a simple format: "ColumnName (DataType)". This is used by
    /// <see cref="SchemaRetriever"/> to score column relevance based on keyword matching.
    /// </remarks>
    private static string GenerateColumnSummary(ColumnSchema column)
    {
        return column.ColumnName + " (" + column.DataType + ")";
    }
}

