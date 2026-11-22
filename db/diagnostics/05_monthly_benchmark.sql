USE FinPerf;
GO
SET NOCOUNT ON;
SET STATISTICS IO, TIME ON;

WITH PortfolioDays AS (
    SELECT
        CAST(AsOfDate AS date) AS PositionDate,
        SUM(PnL) AS DailyPnL,
        CAST(SUM(PnL) / NULLIF(SUM(MarketValueBegin), 0) AS float) AS DailyRate
    FROM dbo.PositionDaily
    WHERE PortfolioId = 7
      AND AsOfDate >= '20230101'
      AND AsOfDate <  '20240101'
    GROUP BY CAST(AsOfDate AS date)
)
SELECT
    MONTH(PositionDate) AS MonthNumber,
    COUNT(*) AS TradingDays,
    SUM(DailyPnL) AS MonthlyPnL,
    CAST(100.0 * (EXP(SUM(LOG(1.0 + DailyRate))) - 1.0)
         AS decimal(12, 6)) AS MonthlyReturnPct
FROM PortfolioDays
GROUP BY MONTH(PositionDate)
ORDER BY MonthNumber;
