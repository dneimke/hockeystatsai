using FluentAssertions;
using HockeyStatsAI.Core.Schema;
using HockeyStatsAI.Models.Schema;
using Xunit;

namespace HockeyStatsAI.Tests.Core.Schema;

public class SchemaRegistryTests
{
	[Fact]
	public async Task SaveAndLoad_Works()
	{
		var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
		var registry = new SchemaRegistry(path);
		var db = new DatabaseSchema
		{
			ServerName = "local",
			DatabaseName = "hockeystats",
			Tables =
			[
				new TableSchema { SchemaName = "dbo", TableName = "Club", Columns = [ new ColumnSchema { ColumnName = "Id", DataType = "int" } ] }
			]
		};

		await registry.SaveAsync(db);
		var loaded = await registry.LoadOrBuildAsync(() => Task.FromResult(db));
		loaded.DatabaseName.Should().Be("hockeystats");
		loaded.Tables.Should().ContainSingle(t => t.TableName == "Club");
	}
}

