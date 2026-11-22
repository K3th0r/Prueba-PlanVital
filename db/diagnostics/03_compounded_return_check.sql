USE FinPerf;
GO
WITH PortfolioDays AS (
    SELECT
        AsOfDate,
        CAST(SUM(PnL) / NULLIF(SUM(MarketValueBegin), 0) AS float) AS DailyRate
    FROM dbo.PositionDaily
    WHERE PortfolioId = 7
      AND AsOfDate >= '20230101'
      AND AsOfDate <  '20240101'
    GROUP BY AsOfDate
)
SELECT
    COUNT(*) AS TradingDays,
    CAST(100.0 * SUM(DailyRate) AS decimal(12, 6)) AS SumDailyPct,
    CAST(100.0 * (EXP(SUM(LOG(1.0 + DailyRate))) - 1.0)
         AS decimal(12, 6)) AS CompoundedPct
FROM PortfolioDays;
