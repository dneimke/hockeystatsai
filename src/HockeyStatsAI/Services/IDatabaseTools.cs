namespace HockeyStatsAI.Services;

public interface IDatabaseTools
{
    IEnumerable<string> ListAllTables();
    IEnumerable<dynamic> GetTableSchema(string tableName);
    IEnumerable<dynamic> GetForeignKeys(string tableName);
}
