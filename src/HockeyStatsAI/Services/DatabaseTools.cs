using Microsoft.Data.SqlClient;
using System.Data;

namespace HockeyStatsAI.Services;

public class DatabaseTools(string connectionString) : IDatabaseTools
{
    private readonly string _connectionString = connectionString;

    public IEnumerable<string> ListAllTables()
    {
        var tables = new List<string>();
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_CATALOG = 'hockeystats'", connection);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }
        return tables;
    }

    public class TableSchema
    {
        public string? ColumnName { get; set; }
        public string? DataType { get; set; }
        public bool IsPrimaryKey { get; set; }
    }

    public IEnumerable<dynamic> GetTableSchema(string tableName)
    {
        var schema = new List<TableSchema>();
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        var columns = connection.GetSchema("Columns", new[] { null, null, tableName });
        foreach (DataRow row in columns.Rows)
        {
            schema.Add(new TableSchema
            {
                ColumnName = row["COLUMN_NAME"].ToString(),
                DataType = row["DATA_TYPE"].ToString(),
                IsPrimaryKey = false
            });
        }

        using var pkCommand = new SqlCommand($@"
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
            WHERE OBJECTPROPERTY(OBJECT_ID(CONSTRAINT_SCHEMA + '.' + QUOTENAME(CONSTRAINT_NAME)), 'IsPrimaryKey') = 1
            AND TABLE_NAME = '{tableName}'", connection);

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

    public class ForeignKey
    {
        public string? FkTable { get; set; }
        public string? FkColumn { get; set; }
        public string? PkTable { get; set; }
        public string? PkColumn { get; set; }
    }

    public IEnumerable<dynamic> GetForeignKeys(string tableName)
    {
        var foreignKeys = new List<ForeignKey>();
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = new SqlCommand($@"
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
                fk.TABLE_NAME = '{tableName}' OR pk.TABLE_NAME = '{tableName}'
        ", connection);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            foreignKeys.Add(new ForeignKey
            {
                FkTable = reader["FkTable"].ToString(),
                FkColumn = reader["FkColumn"].ToString(),
                PkTable = reader["PkTable"].ToString(),
                PkColumn = reader["PkColumn"].ToString()
            });
        }

        return foreignKeys;
    }
}
