using System.Net;
using System.Net.Http.Json;
using FinPerf.Api;
using FinPerf.Performance;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FinPerf.Api.Tests;

public sealed class RangeEndpointTests : IDisposable
{
    private readonly WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPerformanceRepository>();
            services.AddSingleton<IPerformanceRepository>(new SampleRepository());
        }));

    [Fact]
    public async Task ValidRangeReturnsTheTwoDailyPositionsCompounded()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/portfolios/7/performance?from=2023-01-31&to=2023-02-01");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RangeResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body.TradingDays);
        Assert.Equal(30_280.0165m, body.PnL);
        Assert.Equal(0.053939m, decimal.Round(body.CumulativeReturn!.Value * 100m, 6));
    }

    [Theory]
    [InlineData("7/performance?from=2023-02-01&to=2023-01-31", 400)]
    [InlineData("7/performance?from=2023-02-30&to=2023-03-01", 400)]
    [InlineData("7/performance?from=2023-01-01", 400)]
    [InlineData("7/performance?from=2010-01-01&to=2023-01-01", 400)]
    [InlineData("999/performance?from=2023-01-31&to=2023-02-01", 404)]
    public async Task InvalidRequestReturnsProblemDetails(string path, int expected)
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/portfolios/" + path);
        Assert.Equal(expected, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        Assert.Equal(expected, problem?.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem?.Detail));
    }

    [Fact]
    public async Task WeekendInsideCoverageReturnsAnEmptyPeriod()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/portfolios/7/performance?from=2023-02-04&to=2023-02-05");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new RangeResponse(0, null, null), await response.Content.ReadFromJsonAsync<RangeResponse>());
    }

    public void Dispose() => factory.Dispose();

    private sealed record RangeResponse(int TradingDays, decimal? PnL, decimal? CumulativeReturn);

    private sealed class SampleRepository : IPerformanceRepository
    {
        public Task<PortfolioCoverage?> GetCoverageAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult<PortfolioCoverage?>(id == 7
                ? new PortfolioCoverage(new(2015, 1, 1), new(2015, 1, 1), new(2024, 12, 31)) : null);

        public Task<IReadOnlyList<DailyTotal>> GetDailyTotalsAsync(
            int id, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            DailyTotal[] data = [
                new(new DateOnly(2023, 1, 31), 13_344.1187m, 56_967_369m),
                new(new DateOnly(2023, 2, 1), 16_935.8978m, 55_513_559m)
            ];
            return Task.FromResult<IReadOnlyList<DailyTotal>>(
                data.Where(day => day.PositionDate >= from && day.PositionDate <= to).ToArray());
        }
    }
}
