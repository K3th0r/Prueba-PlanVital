# Diagnóstico SQL

## Medición inicial

Ejecuté `dbo.usp_GetPerformance` para el portafolio 7 y el año 2023 con `SET STATISTICS IO, TIME, XML ON`. En esta ejecución, la consulta mensual tardó 23.856 ms y registró 154.526 lecturas lógicas sobre `PositionDaily` y 16.145.556 sobre `Worktable`. [Captura original con estadísticas y plan real](evidencia/finperf-original-p7-2023.txt)

El nodo 29 (`Index Spool`) entregó 67.834.000 filas en 260 ejecuciones, sumando los contadores reales de todos los hilos. El promedio fue de 260.900 filas por ejecución, que coincide con las 2.609 fechas del histórico por los 100 instrumentos del portafolio. El filtro por día (nodo 28) dejó pasar, en promedio, 100 filas por ejecución.

Mi hipótesis es que agrupar los instrumentos por portafolio y fecha una sola vez reduciría ese trabajo repetido. Queda por comprobarlo comparando las lecturas y los tiempos con los mismos parámetros.

## Del instrumento al retorno diario del portafolio

Para calcular el YTD, el procedimiento original usa `SUM(DailyReturn)`. Primero quise verificar qué pasa al sumar los retornos de los instrumentos de un solo día. Para el 3 de enero comparé esa suma con el P&L total dividido por el valor inicial del portafolio.

```sql
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
```

| Posiciones | P&L del día (CLP) | Valor inicial (CLP) | Suma de retornos | Retorno del portafolio |
|---:|---:|---:|---:|---:|
| 100 | 129.379,8494 | 53.639.476,0000 | 28,232000 % | 0,241202 % |

El P&L corresponde al porcentaje mostrado en `PortfolioReturnPct`. Ganar 129.379,8494 pesos sobre un valor inicial de 53.639.476 pesos equivale aproximadamente a un 0,241202 %. Al sumar primero los montos en pesos, cada instrumento pesa según el capital que tenía invertido ese día.

## Del retorno diario al mes y al año

Pasé de un día a un mes. Comparé la suma de los `DailyRate` con el retorno acumulado que obtuve al multiplicar los factores diarios `1 + DailyRate`. En SQL usé `EXP(SUM(LOG(1.0 + DailyRate))) - 1.0` para calcular ese producto y restarle uno.

En la consulta siguiente usé `'20230201'` como límite superior para enero. Después cambié únicamente ese límite a `'20240101'` para calcular todo 2023.

```sql
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
```

| Período | Fechas | Suma de retornos diarios | Retorno compuesto |
|---|---:|---:|---:|
| Enero de 2023 | 22 | −0,037400 % | −0,038771 % |
| Año 2023 | 260 | 5,204100 % | 5,324222 % |

En enero, el procedimiento devolvió −0,037400 %, que coincide con la suma de los 22 retornos diarios. Al componer esos mismos retornos obtuve −0,038771 %. La suma subestimó la pérdida del mes en 0,001371 puntos porcentuales.

Para todo 2023, la suma también difiere del compuesto. Además, el procedimiento original devolvió `6.1314199999999905` como razón, aproximadamente **613,142 %**. Revisé qué sumaba cada consulta. El YTD original suma los retornos individuales de las 26.000 posiciones. En mi comprobación calculé primero el retorno ponderado de cada una de las 260 fechas y después compuse esos retornos. Así obtuve **5,324222 %**.

## Comprobación del P&L anual

Calculé el P&L de dos formas para ver si coincidía con el `PnLYTD` del procedimiento original. Primero sumé el P&L almacenado. Después lo reconstruí para cada posición a partir del valor al cierre, el valor inicial y los flujos netos. Como el P&L en pesos es aditivo, pude sumar las 26.000 posiciones directamente.

```sql
SELECT
    COUNT(*) AS Positions,
    SUM(PnL) AS StoredPnL,
    SUM(MarketValueEnd - MarketValueBegin - NetFlows) AS ReconstructedPnL
FROM dbo.PositionDaily
WHERE PortfolioId = 7
  AND AsOfDate >= '20230101'
  AND AsOfDate <  '20240101';
```

| Posiciones | P&L almacenado (CLP) | P&L reconstruido (CLP) | P&L del procedimiento (CLP) |
|---:|---:|---:|---:|
| 26.000 | 2.856.350,8692 | 2.856.350,8692 | 2.856.350,8692 |

Las tres cifras coinciden. Para este portafolio y año, el procedimiento calculó correctamente el total de P&L. El error financiero que encontré está en el retorno.

## Qué consulta conviene optimizar primero

Para comparar el costo de los cálculos, volví a la captura de la ejecución original del procedimiento para el portafolio 7 en 2023.

| Consulta original | Tiempo transcurrido | Tiempo de CPU | Lecturas lógicas de `PositionDaily` | Lecturas lógicas de `Worktable` |
|---|---:|---:|---:|---:|
| Mensual (12 meses) | 23.856 ms | 93.341 ms | 154.526 | 16.145.556 |
| Anual (un resumen) | 74 ms | 541 ms | 51.816 | 0 |

Para entender los 74 ms del anual, comparé las filas examinadas con las que pasaron el filtro. El nodo 7 recorrió las **5.218.000 filas** de `PositionDaily` usando el índice de la clave primaria. Pasaron **26.000 posiciones**, una por cada uno de los 100 instrumentos del portafolio en cada una de las 260 fechas de 2023.

Las **51.816 lecturas lógicas** del anual cuentan accesos a páginas en memoria. Una página puede contener varias filas y leerse más de una vez. La consulta registró **0 lecturas físicas**. Los **541 ms de CPU** acumulan el trabajo de los hilos en paralelo. El tiempo transcurrido fue de **74 ms**.

Priorizo la consulta mensual. De los **23.945 ms** del procedimiento, consumió **23.856 ms (99,6 %)**. La consulta anual quedó por debajo del objetivo orientativo de 200 ms en esta ejecución, aunque su cálculo de retorno requiere la corrección identificada arriba.
