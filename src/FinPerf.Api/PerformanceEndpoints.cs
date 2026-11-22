using System.Globalization;
using FinPerf.Performance;

namespace FinPerf.Api;

internal static class PerformanceEndpoints
{
    public static async Task<IResult> GetRange(
        int id, string? from, string? to, IPerformanceRepository repository, CancellationToken cancellationToken)
    {
        if (!TryGetDate(from, out var start) || !TryGetDate(to, out var end))
            return Results.Problem("Indica from y to con el formato AAAA-MM-DD.", statusCode: 400);

        if (start > end)
            return Results.Problem("La fecha from debe ser anterior o igual a to.", statusCode: 400);

        var coverage = await repository.GetCoverageAsync(id, cancellationToken);
        if (coverage is null)
            return Results.Problem("El portafolio solicitado no existe.", statusCode: 404);

        if (coverage.FirstDate is null || coverage.LastDate is null)
            return Results.Problem("El portafolio todavía no tiene posiciones.", statusCode: 400);

        if (start < coverage.FirstDate || end > coverage.LastDate)
            return Results.Problem("Las fechas deben estar dentro del histórico del portafolio.", statusCode: 400);

        var days = await repository.GetDailyTotalsAsync(id, start, end, cancellationToken);
        var result = ReturnCalculator.Calculate(days);

        return Results.Ok(new
        {
            portfolioId = id,
            from = start,
            to = end,
            tradingDays = days.Count,
            pnl = result?.PnL,
            cumulativeReturn = result?.CumulativeReturn
        });
    }

    private static bool TryGetDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
