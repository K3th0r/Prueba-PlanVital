USE FinPerf;
GO
SELECT
    COUNT(*) AS Positions,
    SUM(PnL) AS PortfolioPnL,
    SUM(MarketValueBegin) AS PortfolioBeginValue,
    CAST(100.0 * SUM(DailyReturn) AS decimal(12, 6)) AS SumInstrumentReturnsPct,
    CAST(100.0 * SUM(PnL) / NULLIF(SUM(MarketValueBegin), 0)
         AS decimal(12, 6)) AS PortfolioReturnPct
FROM dbo.PositionDaily
WHERE PortfolioId = 7
  AND AsOfDate >= '20230103'
  AND AsOfDate <  '20230104';
