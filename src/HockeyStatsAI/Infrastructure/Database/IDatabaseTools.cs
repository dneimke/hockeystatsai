namespace HockeyStatsAI.Infrastructure.Database;

/// <summary>
/// Provides tools for introspecting database schema information.
/// </summary>
/// <remarks>
/// This interface is implemented by <see cref="DatabaseTools"/> and is registered as a singleton
/// in <see cref="Configuration.ServiceCollectionExtensions.AddHockeyStatsServices"/>.
/// It provides methods for listing tables, retrieving table schemas, and getting foreign key relationships.
/// Used by <see cref="DatabaseTools.LoadDatabaseSchemaAsync"/> to build the complete database schema.
/// </remarks>
public interface IDatabaseTools
{
    /// <summary>
    /// Lists all base tables in the database.
    /// </summary>
    /// <returns>An enumerable of table names (without schema prefix).</returns>
    /// <remarks>
    /// Queries INFORMATION_SCHEMA.TABLES to find all base tables in the 'hockeystats' database.
    /// </remarks>
    IEnumerable<string> ListAllTables();

    /// <summary>
    /// Gets the schema information for a specific table, including column names, data types, and primary key status.
    /// </summary>
    /// <param name="tableName">The name of the table (without schema prefix).</param>
    /// <returns>
    /// An enumerable of dynamic objects with ColumnName, DataType, and IsPrimaryKey properties.
    /// </returns>
    /// <remarks>
    /// Retrieves column information from INFORMATION_SCHEMA.COLUMNS and identifies primary key columns
    /// from INFORMATION_SCHEMA.KEY_COLUMN_USAGE.
    /// </remarks>
    IEnumerable<dynamic> GetTableSchema(string tableName);

    /// <summary>
    /// Gets foreign key relationships for a specific table.
    /// </summary>
    /// <param name="tableName">The name of the table (without schema prefix).</param>
    /// <returns>
    /// An enumerable of dynamic objects with FkTable, FkColumn, PkTable, and PkColumn properties.
    /// </returns>
    /// <remarks>
    /// Retrieves foreign key relationships from INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS.
    /// Returns both foreign keys where the table is the referencing table (FK) and where it's
    /// the referenced table (PK).
    /// </remarks>
    IEnumerable<dynamic> GetForeignKeys(string tableName);
}

