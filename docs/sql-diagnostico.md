# Diagnóstico SQL

## Medición inicial

Ejecuté `dbo.usp_GetPerformance` para el portafolio 7 y el año 2023 con
`SET STATISTICS IO, TIME, XML ON`. En esta ejecución, la consulta mensual
tardó 23.856 ms y registró 154.526 lecturas lógicas sobre `PositionDaily`
y 16.145.556 sobre `Worktable`.

El nodo 29 (`Index Spool`) entregó 67.834.000 filas en 260 ejecuciones,
sumando los contadores reales de todos los hilos. El promedio fue de
260.900 filas por ejecución, que coincide con las 2.609 fechas del
histórico por los 100 instrumentos del portafolio. El filtro por día
(nodo 28) dejó pasar, en promedio, 100 filas por ejecución.

Mi hipótesis es que agrupar los instrumentos por portafolio y fecha una
sola vez reduciría ese trabajo repetido. Queda por comprobarlo comparando
las lecturas y los tiempos con los mismos parámetros.

[Captura original con estadísticas y plan real](evidencia/finperf-original-p7-2023.txt)


Para calcular YTD el procedimiento original usa SUM(DailyReturn), 
primero quiero verificar que DailyReturn entregue el retorno correcto para el dia en cuestión
dado que se utiliza en el retorno acumulado (1+rd) para calcular el acumulado de lo que va del año.

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

Positions   PortfolioPnL                             PortfolioBeginValue                      SumInstrumentReturnsPct PortfolioReturnPct
----------- ---------------------------------------- ---------------------------------------- ----------------------- ------------------
        100                              129379.8494                            53639476.0000               28.232000            .241202


El PnL corresponde al porcentaje mostrado en la columna PortfolioReturnPct, por lo que SumInstrumentReturnsPct está fallando como retorno.