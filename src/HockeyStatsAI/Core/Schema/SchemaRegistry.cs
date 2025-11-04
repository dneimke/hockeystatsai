using System.Text.Json;
using HockeyStatsAI.Models.Schema;

namespace HockeyStatsAI.Core.Schema;

/// <summary>
/// Manages the persistence and retrieval of database schema information.
/// Provides caching of schema data to disk to avoid rebuilding the schema on every application start.
/// </summary>
/// <remarks>
/// This class is registered as a singleton in <see cref="Configuration.ServiceCollectionExtensions.AddHockeyStatsServices"/>.
/// It is used by <see cref="SchemaRetriever"/>, <see cref="JoinPathFinder"/>, and <see cref="TargetedTranslationStrategy"/>
/// to access the database schema. The schema is loaded from a JSON file on disk, or built from the database
/// if the file doesn't exist or is invalid.
/// </remarks>
public sealed class SchemaRegistry
{
	private readonly string _registryPath;
	private readonly JsonSerializerOptions _jsonOptions = new()
	{
		WriteIndented = true
	};

	/// <summary>
	/// Gets the loaded database schema, or null if not yet loaded.
	/// </summary>
	public DatabaseSchema? Schema { get; private set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="SchemaRegistry"/> class.
	/// </summary>
	/// <param name="registryPath">The file path where the schema JSON will be stored and loaded from.</param>
	public SchemaRegistry(string registryPath)
	{
		_registryPath = registryPath;
	}

	/// <summary>
	/// Loads the schema from disk if it exists, otherwise builds it using the provided factory function.
	/// </summary>
	/// <param name="buildSchema">An async function that builds the database schema from the database when needed.</param>
	/// <returns>The loaded or newly built <see cref="DatabaseSchema"/>.</returns>
	/// <remarks>
	/// This method first checks if a schema file exists at <see cref="_registryPath"/>. If it exists,
	/// it deserializes and returns it. Otherwise, it calls <paramref name="buildSchema"/> to build
	/// the schema, saves it to disk, and returns it. This caching mechanism speeds up application startup
	/// by avoiding expensive schema introspection operations.
	/// </remarks>
	public async Task<DatabaseSchema> LoadOrBuildAsync(Func<Task<DatabaseSchema>> buildSchema)
	{
		if (File.Exists(_registryPath))
		{
			await using var fs = File.OpenRead(_registryPath);
			var existing = await JsonSerializer.DeserializeAsync<DatabaseSchema>(fs, _jsonOptions);
			if (existing != null)
			{
				Console.WriteLine($"Loaded schema from {_registryPath}");
				Schema = existing;
				return existing;
			}
		}

		Console.WriteLine($"Building schema from database");
		var built = await buildSchema();
		Schema = built;
		await SaveAsync(built);
		return built;
	}

	/// <summary>
	/// Saves the database schema to disk as JSON.
	/// </summary>
	/// <param name="schema">The schema to save.</param>
	/// <remarks>
	/// Creates the directory if it doesn't exist, then serializes the schema to the registry file path.
	/// This is called automatically by <see cref="LoadOrBuildAsync"/> when a new schema is built.
	/// </remarks>
	public async Task SaveAsync(DatabaseSchema schema)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_registryPath)!);
		await using var fs = File.Create(_registryPath);
		await JsonSerializer.SerializeAsync(fs, schema, _jsonOptions);
	}

	/// <summary>
	/// Gets a table schema by name, searching by table name or full name (schema.table).
	/// </summary>
	/// <param name="tableName">The table name (e.g., "Player") or full name (e.g., "dbo.Player").</param>
	/// <returns>The matching <see cref="TableSchema"/>, or null if not found or schema not loaded.</returns>
	public TableSchema? GetTable(string tableName)
	{
		if (Schema == null) return null;
		return Schema.Tables.FirstOrDefault(t => t.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase)
			|| (t.SchemaName + "." + t.TableName).Equals(tableName, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Gets all tables in the schema.
	/// </summary>
	/// <returns>An enumerable of all <see cref="TableSchema"/> objects, or an empty enumerable if schema not loaded.</returns>
	public IEnumerable<TableSchema> GetTables() => Schema?.Tables ?? Enumerable.Empty<TableSchema>();
}

