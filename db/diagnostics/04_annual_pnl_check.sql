USE FinPerf;
GO
SELECT
    COUNT(*) AS Positions,
    SUM(PnL) AS StoredPnL,
    SUM(MarketValueEnd - MarketValueBegin - NetFlows) AS ReconstructedPnL
FROM dbo.PositionDaily
WHERE PortfolioId = 7
  AND AsOfDate >= '20230101'
  AND AsOfDate <  '20240101';
