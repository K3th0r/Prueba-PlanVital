USE FinPerf;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PositionDaily', N'U')
      AND name = N'IX_PositionDaily_PortfolioId_AsOfDate'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_PositionDaily_PortfolioId_AsOfDate
        ON dbo.PositionDaily (PortfolioId, AsOfDate)
        INCLUDE (PnL, MarketValueBegin);
END;
GO
