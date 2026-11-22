namespace FinPerf.Performance;

public sealed record DailyTotal(DateOnly PositionDate, decimal DailyPnL, decimal OpeningValue);

public sealed record PeriodPerformance(decimal PnL, decimal? CumulativeReturn);

public static class ReturnCalculator
{
    // Suma el P&L y compone los retornos de las filas diarias. Devuelve null si el período está vacío.
    public static PeriodPerformance? Calculate(IEnumerable<DailyTotal> days)
    {
        decimal totalPnL = 0m;
        decimal? factor = 1m;
        bool hasDays = false;

        foreach (var day in days)
        {
            hasDays = true;
            totalPnL += day.DailyPnL;
            if (day.OpeningValue == 0m)
            {
                factor = null;
            }
            else if (factor.HasValue)
            {
                factor *= 1m + day.DailyPnL / day.OpeningValue;
            }
        }

        return hasDays ? new PeriodPerformance(totalPnL, factor - 1m) : null;
    }
}
