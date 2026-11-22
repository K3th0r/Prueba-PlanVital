USE FinPerf;
GO
SET STATISTICS IO, TIME, XML ON;
EXEC dbo.usp_GetPerformance @PortfolioId = 7, @Year = 2023;
SET STATISTICS IO, TIME, XML OFF;
