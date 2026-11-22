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

public sealed class SummaryEndpointTests : IDisposable
{
    private readonly SampleRepository repository = new();
    private readonly WebApplicationFactory<Program> factory;

    // Configura el servidor de prueba con un repositorio que registra las lecturas diarias.
    public SummaryEndpointTests()
    {
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPerformanceRepository>();
                services.AddSingleton<IPerformanceRepository>(repository);
            }));
    }

    // Verifica las cuatro ventanas de agosto y comprueba que comparten una sola lectura del histórico.
    [Fact]
    public async Task AugustCutoffBuildsFourPeriodsFromOneDailyRead()
    {
        using var client = factory.CreateClient();
        var json = await client.GetStringAsync("/api/portfolios/7/performance/summary?asOf=2023-08-15");
        using var document = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(9m, document.RootElement.GetProperty("mtd").GetProperty("pnl").GetDecimal());
        var body = System.Text.Json.JsonSerializer.Deserialize<Summary>(json, System.Text.Json.JsonSerializerOptions.Web);
        Assert.NotNull(body);
        var end = new DateOnly(2023, 8, 15);
        Assert.Equal(end, body.AsOf);
        Assert.Equal(new Slice(new(2023, 8, 1), end, 2, 9m, 1.04m * 1.05m - 1m), body.Mtd);
        Assert.Equal(new Slice(new(2023, 7, 1), end, 3, 12m, 1.03m * 1.04m * 1.05m - 1m), body.Qtd);
        Assert.Equal(new Slice(new(2023, 1, 1), end, 4, 14m, 1.02m * 1.03m * 1.04m * 1.05m - 1m), body.Ytd);
        Assert.Equal(new Slice(new(2022, 10, 15), end, 5, 15m, 1.01m * 1.02m * 1.03m * 1.04m * 1.05m - 1m), body.Itd);
        Assert.Equal((new DateOnly(2022, 10, 15), end), Assert.Single(repository.Reads));
    }

    // Verifica qué períodos quedan vacíos cuando el año o el trimestre empieza sin posiciones.
    [Theory]
    [InlineData("2023-04-01", 1)]
    [InlineData("2023-01-01", 0)]
    public async Task CalendarBoundaryWithoutPositionsKeepsEmptyPeriods(string asOf, int expectedYearDays)
    {
        using var client = factory.CreateClient();
        var body = await client.GetFromJsonAsync<Summary>($"/api/portfolios/7/performance/summary?asOf={asOf}");
        Assert.NotNull(body);
        Assert.Equal(new Slice(body.AsOf, body.AsOf, 0, null, null), body.Mtd);
        Assert.Equal(body.Mtd, body.Qtd);
        Assert.Equal(expectedYearDays, body.Ytd.TradingDays);
        Assert.Equal(expectedYearDays + 1, body.Itd.TradingDays);
    }

    // Comprueba que ningún período empiece antes de la fecha de inicio del portafolio.
    [Fact]
    public async Task FirstTradingDayUsesInceptionAsEarliestBoundary()
    {
        using var client = factory.CreateClient();
        var body = await client.GetFromJsonAsync<Summary>("/api/portfolios/7/performance/summary?asOf=2022-10-17");
        Assert.NotNull(body);
        var expected = new Slice(new(2022, 10, 15), new(2022, 10, 17), 1, 1m, 0.01m);
        Assert.Equal(expected, body.Mtd);
        Assert.Equal(expected, body.Qtd);
        Assert.Equal(expected, body.Ytd);
        Assert.Equal(expected, body.Itd);
    }

    // Comprueba que un corte inválido o un portafolio inexistente se rechacen antes de leer posiciones.
    [Theory]
    [InlineData("7/performance/summary", 400)]
    [InlineData("7/performance/summary?asOf=2023-02-30", 400)]
    [InlineData("7/performance/summary?asOf=2024-01-01", 400)]
    [InlineData("7/performance/summary?asOf=2022-10-01", 400)]
    [InlineData("999/performance/summary?asOf=2023-08-15", 404)]
    public async Task InvalidSummaryReturnsProblemDetailsWithoutReadingPositions(string path, int status)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/portfolios/" + path);
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(repository.Reads);
    }

    // Cierra el servidor de prueba al terminar cada caso.
    public void Dispose() => factory.Dispose();

    private sealed record Slice(DateOnly From, DateOnly To, int TradingDays, decimal? PnL, decimal? CumulativeReturn);
    private sealed record Summary(DateOnly AsOf, Slice Mtd, Slice Qtd, Slice Ytd, Slice Itd);

    private sealed class SampleRepository : IPerformanceRepository
    {
        public List<(DateOnly From, DateOnly To)> Reads { get; } = [];
        // Simula un portafolio cuya primera posición es posterior a su fecha de inicio.
        public Task<PortfolioCoverage?> GetCoverageAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult<PortfolioCoverage?>(id == 7
                ? new(new(2022, 10, 15), new(2022, 10, 17), new(2023, 8, 16))
                : null);

        // El resumen debe usar los totales diarios, por lo que una lectura mensual falla la prueba.
        public Task<IReadOnlyList<MonthlyPerformance>> GetMonthlyAsync(
            int id, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("El resumen no necesita la consulta mensual.");

        // Registra el rango pedido y devuelve sus filas de prueba para verificar la lectura compartida.
        public Task<IReadOnlyList<DailyTotal>> GetDailyTotalsAsync(
            int id, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            Reads.Add((from, to));
            DailyTotal[] days = [
                new(new(2022, 10, 17), 1m, 100m), new(new(2023, 1, 2), 2m, 100m),
                new(new(2023, 7, 3), 3m, 100m), new(new(2023, 8, 1), 4m, 100m),
                new(new(2023, 8, 15), 5m, 100m), new(new(2023, 8, 16), 6m, 100m)
            ];
            return Task.FromResult<IReadOnlyList<DailyTotal>>(
                days.Where(day => from <= day.PositionDate && day.PositionDate <= to).ToArray());
        }
    }
}
