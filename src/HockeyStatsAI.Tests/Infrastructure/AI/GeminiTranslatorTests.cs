using System.Net;
using System.Text.Json;
using HockeyStatsAI.Infrastructure.AI;
using HockeyStatsAI.Infrastructure.AI.Strategies;
using HockeyStatsAI.Infrastructure.Database;
using Moq;
using Xunit;

namespace HockeyStatsAI.Tests.Infrastructure.AI;

public class GeminiTranslatorTests
{
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

