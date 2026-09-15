using System.Net;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Api.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class HealthEndpointTests : IDisposable
{
    private readonly ApiFactory _factory;

    public HealthEndpointTests(DatabaseFixture fixture) => _factory = new ApiFactory(fixture);

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Get_HealthEndpoint_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_HealthEndpoint_ReportsHealthy()
    {
        using var client = _factory.CreateClient();

        var body = await client.GetStringAsync("/health", TestContext.Current.CancellationToken);

        body.Should().Contain("Healthy");
    }
}
