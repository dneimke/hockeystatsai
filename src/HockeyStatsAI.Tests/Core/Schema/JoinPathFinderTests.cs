using FluentAssertions;
using HockeyStatsAI.Core.Schema;
using HockeyStatsAI.Models.Schema;
using Xunit;

namespace HockeyStatsAI.Tests.Core.Schema;

public class JoinPathFinderTests
{
	[Fact]
	public async Task SingleTable_NoJoins()
	{
		var registry = new SchemaRegistry(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"));
		var schema = new DatabaseSchema
		{
			Tables =
			[
				new TableSchema { SchemaName = "dbo", TableName = "Player" }
			]
		};
		await registry.LoadOrBuildAsync(() => Task.FromResult(schema));
		var finder = new JoinPathFinder(registry);
		var plan = finder.FindJoinPlan([registry.Schema!.Tables[0]]);
		plan.Joins.Should().BeEmpty();
	}
}

