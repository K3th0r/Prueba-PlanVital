using System.Globalization;
using System.Text.Json.Serialization;
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
        if (ValidateCoverage(coverage, start, end) is { } error)
            return error;

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

    public static async Task<IResult> GetSummary(
        int id, string? asOf, IPerformanceRepository repository, CancellationToken cancellationToken)
    {
        if (!TryGetDate(asOf, out var cutoff))
            return Results.Problem("Indica asOf con el formato AAAA-MM-DD.", statusCode: 400);

        var coverage = await repository.GetCoverageAsync(id, cancellationToken);
        if (ValidateCoverage(coverage, cutoff, cutoff) is { } error)
            return error;

        var inception = coverage!.InceptionDate;
        var days = await repository.GetDailyTotalsAsync(id, inception, cutoff, cancellationToken);
        var monthStart = new DateOnly(cutoff.Year, cutoff.Month, 1);
        var quarterStart = monthStart.AddMonths(-((cutoff.Month - 1) % 3));
        var yearStart = new DateOnly(cutoff.Year, 1, 1);

        PeriodSlice CalculateSince(DateOnly calendarStart)
        {
            var from = calendarStart < inception ? inception : calendarStart;
            var periodDays = days.Where(day => day.PositionDate >= from).ToArray();
            var performance = ReturnCalculator.Calculate(periodDays);
            return new PeriodSlice(from, cutoff, periodDays.Length, performance?.PnL, performance?.CumulativeReturn);
        }

        return Results.Ok(new
        {
            portfolioId = id,
            asOf = cutoff,
            mtd = CalculateSince(monthStart),
            qtd = CalculateSince(quarterStart),
            ytd = CalculateSince(yearStart),
            itd = CalculateSince(inception)
        });
    }

    private sealed record PeriodSlice(DateOnly From, DateOnly To, int TradingDays,
        [property: JsonPropertyName("pnl")] decimal? PnL, decimal? CumulativeReturn);

    private static IResult? ValidateCoverage(PortfolioCoverage? coverage, DateOnly from, DateOnly to)
    {
        if (coverage is null)
            return Results.Problem("El portafolio solicitado no existe.", statusCode: 404);

        if (coverage.FirstDate is null || coverage.LastDate is null)
            return Results.Problem("El portafolio todavía no tiene posiciones.", statusCode: 400);

        if (from < coverage.FirstDate || from < coverage.InceptionDate || to > coverage.LastDate)
            return Results.Problem("Las fechas deben estar dentro del histórico del portafolio.", statusCode: 400);

        return null;
    }

    private static bool TryGetDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
