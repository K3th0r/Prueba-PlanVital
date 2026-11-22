using FinPerf.Performance;
using Xunit;

namespace FinPerf.Performance.Tests;

public class ReturnCalculatorTests
{
    private static readonly DateOnly FirstDate = new(2023, 1, 2);

    [Fact]
    public void GainThenLossCompoundsToMinusOnePercent()
    {
        var result = ReturnCalculator.Calculate([
            new(FirstDate, 10m, 100m),
            new(FirstDate.AddDays(1), -11m, 110m)
        ]);

        Assert.Equal(new PeriodPerformance(-1m, -0.01m), result);
    }

    [Fact]
    public void ThirdDayGainCompoundsToThreePointNineFivePercent()
    {
        var result = ReturnCalculator.Calculate([
            new(FirstDate, 10m, 100m),
            new(FirstDate.AddDays(1), -11m, 110m),
            new(FirstDate.AddDays(2), 4.95m, 99m)
        ]);

        Assert.Equal(new PeriodPerformance(3.95m, 0.0395m), result);
    }

    [Fact]
    public void DailyReturnUsesTheCombinedOpeningValueOfBothInstruments()
    {
        var day = new DailyTotal(FirstDate, 10_000m - 5_000m, 1_000_000m + 100_000m);

        var result = ReturnCalculator.Calculate([day]);

        Assert.NotNull(result);
        Assert.Equal(5_000m, result.PnL);
        Assert.Equal(0.4545m, decimal.Round(result.CumulativeReturn!.Value * 100m, 4));
    }

    [Fact]
    public void RangeAcrossMonthsMatchesPortfolioSevenControl()
    {
        var result = ReturnCalculator.Calculate([
            new(new DateOnly(2023, 1, 31), 13_344.1187m, 56_967_369m),
            new(new DateOnly(2023, 2, 1), 16_935.8978m, 55_513_559m)
        ]);

        Assert.NotNull(result);
        Assert.Equal(30_280.0165m, result.PnL);
        Assert.Equal(0.053939m, decimal.Round(result.CumulativeReturn!.Value * 100m, 6));
    }

    [Fact]
    public void ZeroOpeningValueMakesReturnUndefinedAndPreservesAllPnL()
    {
        var result = ReturnCalculator.Calculate([
            new(FirstDate, 10m, 100m),
            new(FirstDate.AddDays(1), 5m, 0m),
            new(FirstDate.AddDays(2), -3m, 100m)
        ]);

        Assert.Equal(new PeriodPerformance(12m, null), result);
    }

    [Fact]
    public void TotalLossKeepsTheCompoundedReturnAtMinusOne()
    {
        var result = ReturnCalculator.Calculate([
            new(FirstDate, -100m, 100m),
            new(FirstDate.AddDays(1), 5m, 100m)
        ]);

        Assert.Equal(new PeriodPerformance(-95m, -1m), result);
    }

    [Fact]
    public void EmptyPeriodHasNoResult()
    {
        Assert.Null(ReturnCalculator.Calculate([]));
    }
}
