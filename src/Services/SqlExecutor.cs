using Microsoft.Data.SqlClient;

namespace HockeyStatsAI.Services;

public class SqlExecutor(string connectionString)
{
    private readonly string _connectionString = connectionString;

    public void ExecuteQuery(string query)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = new SqlCommand(query, connection);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                Console.Write($"{reader.GetName(i)}: {reader.GetValue(i)} ");
            }
            Console.WriteLine();
        }
    }
}
