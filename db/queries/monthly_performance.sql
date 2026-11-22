-- Parámetros del llamador: @PortfolioId int, @From date y @To date (ambas fechas inclusivas).
-- MonthlyReturn se expresa como razón. Un valor de 0.01 equivale a 1 %.
WITH DailyTotals AS (
    SELECT
        CAST(AsOfDate AS date) AS PositionDate,
        SUM(PnL) AS DailyPnL,
        SUM(MarketValueBegin) AS OpeningValue
    FROM dbo.PositionDaily
    WHERE PortfolioId = @PortfolioId
      AND AsOfDate >= @From
      AND AsOfDate < DATEADD(day, 1, @To)
    GROUP BY CAST(AsOfDate AS date)
),
DailyFactors AS (
    SELECT
        DATEFROMPARTS(YEAR(PositionDate), MONTH(PositionDate), 1) AS MonthStart,
        DailyPnL,
        -- La conversión antes de dividir conserva precisión para la composición.
        1.0 + CAST(DailyPnL AS float) / NULLIF(OpeningValue, 0) AS Factor
    FROM DailyTotals
)
SELECT
    MonthStart,
    COUNT(*) AS TradingDays,
    SUM(DailyPnL) AS MonthlyPnL,
    CASE
        WHEN COUNT(Factor) < COUNT(*) THEN NULL
        WHEN MIN(ABS(Factor)) = 0 THEN -1.0
        ELSE (CASE WHEN SUM(CASE WHEN Factor < 0 THEN 1 ELSE 0 END) % 2 = 0
                   THEN 1.0 ELSE -1.0 END)
             * EXP(SUM(LOG(NULLIF(ABS(Factor), 0)))) - 1.0
    END AS MonthlyReturn
FROM DailyFactors
GROUP BY MonthStart
ORDER BY MonthStart;
