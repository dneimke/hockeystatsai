using System.Net;
using System.Text.Json;
using HockeyStatsAI.Services;
using Moq;
using Xunit;

namespace HockeyStatsAI.Tests.Services;

public class GeminiTranslatorTests
{
    [Fact]
    public async Task TranslateToSql_Should_Return_Correct_Sql_Query()
    {
        // Arrange
        var apiKey = "test-api-key";
        var databaseToolsMock = new Mock<IDatabaseTools>();
        databaseToolsMock.Setup(x => x.ListAllTables()).Returns(new List<string> { "Club" });
        databaseToolsMock.Setup(x => x.GetTableSchema("Club")).Returns(new List<object> { new { ColumnName = "Name", DataType = "varchar" } });

        var mockHttpMessageHandler = new MockHttpMessageHandler();
        mockHttpMessageHandler.AddResponse(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = new[]
                            {
                                new
                                {
                                    functionCall = new
                                    {
                                        name = "ListAllTables",
                                        args = new { }
                                    }
                                }
                            }
                        }
                    }
                }
            }))
        });
        mockHttpMessageHandler.AddResponse(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = new[]
                            {
                                new
                                {
                                    text = "```sql\nSELECT Name FROM Club\n```"
                                }
                            }
                        }
                    }
                }
            }))
        });

        var httpClient = new HttpClient(mockHttpMessageHandler);
        var geminiTranslator = new GeminiTranslator(apiKey, databaseToolsMock.Object, httpClient);

        // Act
        var result = await geminiTranslator.TranslateToSql("list all clubs");

        // Assert
        Assert.Equal("SELECT Name FROM Club", result);
    }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    public void AddResponse(HttpResponseMessage response)
    {
        _responses.Enqueue(response);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_responses.Dequeue());
    }
}