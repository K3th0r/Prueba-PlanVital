-- @PortfolioId int, @From date, @To date. Ambos extremos del rango son inclusivos.
SELECT
    CAST(AsOfDate AS date) AS PositionDate,
    SUM(PnL) AS DailyPnL,
    SUM(MarketValueBegin) AS OpeningValue
FROM dbo.PositionDaily
WHERE PortfolioId = @PortfolioId
  AND AsOfDate >= @From
  AND AsOfDate < DATEADD(day, 1, @To)
GROUP BY CAST(AsOfDate AS date)
ORDER BY PositionDate;
