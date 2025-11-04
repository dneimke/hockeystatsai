using FluentAssertions;
using HockeyStatsAI.Core.Validation;
using Xunit;

namespace HockeyStatsAI.Tests.Core.Validation;

public class SqlSafetyValidatorTests
{
	[Theory]
	[InlineData("SELECT * FROM dbo.Club")]
	[InlineData("select top 10 Id, Name from dbo.Competition")] 
	public void Allows_Simple_Selects(string sql)
	{
		SqlSafetyValidator.IsSafeSelect(sql, out var reason).Should().BeTrue(reason);
	}

	[Theory]
	[InlineData("DELETE FROM dbo.Club")]
	[InlineData("SELECT * FROM dbo.Club; SELECT * FROM dbo.Competition")] 
	[InlineData("SELECT * INTO #t FROM dbo.Club")] 
	[InlineData("SELECT * FROM #temp")] 
	[InlineData("EXEC sp_who2")] 
	public void Blocks_Unsafe_Statements(string sql)
	{
		SqlSafetyValidator.IsSafeSelect(sql, out var _).Should().BeFalse();
	}
}

