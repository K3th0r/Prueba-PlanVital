# Diagnóstico SQL

## Medición inicial

Ejecuté `dbo.usp_GetPerformance` para el portafolio 7 y el año 2023 con `SET STATISTICS IO, TIME, XML ON`. En esta ejecución, la consulta mensual tardó 23.856 ms y registró 154.526 lecturas lógicas sobre `PositionDaily` y 16.145.556 sobre `Worktable`. [Script de medición original](../db/diagnostics/01_original_performance.sql) · [Captura original con estadísticas y plan real](evidencia/finperf-original-p7-2023.txt)

El nodo 29 (`Index Spool`) entregó 67.834.000 filas en 260 ejecuciones, sumando los contadores reales de todos los hilos. El promedio fue de 260.900 filas por ejecución, que coincide con las 2.609 fechas del histórico por los 100 instrumentos del portafolio. El filtro por día (nodo 28) dejó pasar, en promedio, 100 filas por ejecución.

Mi hipótesis es que agrupar los instrumentos por portafolio y fecha una sola vez reduciría ese trabajo repetido. Queda por comprobarlo comparando las lecturas y los tiempos con los mismos parámetros.

## Del instrumento al retorno diario del portafolio

Para calcular el YTD, el procedimiento original usa `SUM(DailyReturn)`. Primero quise verificar qué pasa al sumar los retornos de los instrumentos de un solo día. Para el 3 de enero comparé esa suma con el P&L total dividido por el valor inicial del portafolio.

[Ejecutar la comprobación SQL](../db/diagnostics/02_daily_return_check.sql)

| Posiciones | P&L del día (CLP) | Valor inicial (CLP) | Suma de retornos | Retorno del portafolio |
|---:|---:|---:|---:|---:|
| 100 | 129.379,8494 | 53.639.476,0000 | 28,232000 % | 0,241202 % |

El P&L corresponde al porcentaje mostrado en `PortfolioReturnPct`. Ganar 129.379,8494 pesos sobre un valor inicial de 53.639.476 pesos equivale aproximadamente a un 0,241202 %. Al sumar primero los montos en pesos, cada instrumento pesa según el capital que tenía invertido ese día.

## Del retorno diario al mes y al año

Pasé de un día a un mes. Comparé la suma de los `DailyRate` con el retorno acumulado que obtuve al multiplicar los factores diarios `1 + DailyRate`. En SQL usé `EXP(SUM(LOG(1.0 + DailyRate))) - 1.0` para calcular ese producto y restarle uno.

En la consulta siguiente usé `'20230201'` como límite superior para enero. Después cambié únicamente ese límite a `'20240101'` para calcular todo 2023.

[Ejecutar la comprobación SQL](../db/diagnostics/03_compounded_return_check.sql)

| Período | Fechas | Suma de retornos diarios | Retorno compuesto |
|---|---:|---:|---:|
| Enero de 2023 | 22 | −0,037400 % | −0,038771 % |
| Año 2023 | 260 | 5,204100 % | 5,324222 % |

En enero, el procedimiento devolvió −0,037400 %, que coincide con la suma de los 22 retornos diarios. Al componer esos mismos retornos obtuve −0,038771 %. La suma subestimó la pérdida del mes en 0,001371 puntos porcentuales.

Para todo 2023, la suma también difiere del compuesto. Además, el procedimiento original devolvió `6.1314199999999905` como razón, aproximadamente **613,142 %**. Revisé qué sumaba cada consulta. El YTD original suma los retornos individuales de las 26.000 posiciones. En mi comprobación calculé primero el retorno ponderado de cada una de las 260 fechas y después compuse esos retornos. Así obtuve **5,324222 %**.

## Comprobación del P&L anual

Calculé el P&L de dos formas para ver si coincidía con el `PnLYTD` del procedimiento original. Primero sumé el P&L almacenado. Después lo reconstruí para cada posición a partir del valor al cierre, el valor inicial y los flujos netos. Como el P&L en pesos es aditivo, pude sumar las 26.000 posiciones directamente.

[Ejecutar la comprobación SQL](../db/diagnostics/04_annual_pnl_check.sql)

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

## Consulta mensual corregida antes de crear índices

Para el portafolio 7 en 2023, filtré por portafolio y por rango de fechas. Agrupé primero los instrumentos por día y después compuse los retornos diarios de cada mes. Obtuve 12 meses. En enero coincidieron los tres valores de control (22 fechas, −23.804,8236 pesos de P&L y −0,038771 % de retorno). Los doce P&L mensuales sumaron 2.856.350,8692 pesos, el mismo total anual que había comprobado por separado.

| Medición mensual | Original | Consulta corregida, con los índices originales |
|---|---:|---:|
| Tiempo transcurrido | 23.856 ms | 92 ms |
| Tiempo de CPU | 93.341 ms | 557 ms |
| Lecturas lógicas de `PositionDaily` | 154.526 | 51.816 |
| Lecturas lógicas de `Worktable` | 16.145.556 | 0 |

La reescritura quedó por debajo de 200 ms en esta ejecución y eliminó las lecturas lógicas registradas sobre la tabla interna. Las 51.816 lecturas sobre la tabla de posiciones sirven como referencia para medir el efecto de un índice sobre portafolio y fecha. Los tiempos corresponden a una ejecución de cada versión.

## Efecto del índice por portafolio y fecha

Elegí `(PortfolioId, AsOfDate)` porque las consultas piden un solo portafolio en un intervalo de fechas. La primera clave reúne las posiciones del portafolio y la segunda las ordena por fecha dentro de ese grupo. Incluí `PnL` y `MarketValueBegin` en las hojas del índice porque son los montos que necesito sumar. Así el motor puede resolver la consulta con el propio índice. SQL Server mantiene sus entradas al insertar o corregir posiciones. El costo adicional es espacio y trabajo de escritura.

Repetí la misma consulta mensual inmediatamente antes y después de aplicar la [migración](../db/migrations/001_position_daily_portfolio_date.sql). Los doce meses coincidieron en fechas, P&L y retorno.

| Medición del SELECT mensual corregido | Antes del índice | Con el índice |
|---|---:|---:|
| Lecturas lógicas de `PositionDaily` | 51.816 | 145 |
| Tiempo transcurrido | 79 ms | 12 ms |
| Tiempo de CPU | 516 ms | 12 ms |
| Lecturas lógicas de `Worktable` | 0 | 0 |

La medición anterior de 92 ms y esta repetición de 79 ms tuvieron las mismas 51.816 lecturas. Con el índice, el plan real muestra un `Index Seek` para el portafolio 7 desde el 1 de enero de 2023 hasta el inicio de 2024, exclusivo. El operador leyó y entregó 26.000 posiciones (100 instrumentos por 260 fechas).

[Consulta usada en la comparación](../db/diagnostics/05_monthly_benchmark.sql) · [Estadísticas antes y después](evidencia/mensual-p7-2023-comparacion-indice.txt) · [Plan real con índice](evidencia/mensual-p7-2023-con-indice.sqlplan)

## Consulta mensual por portafolio y rango

La [consulta mensual parametrizada](../db/queries/monthly_performance.sql) recibe `@PortfolioId`, `@From` y `@To`. Ambas fechas son inclusivas. Cada fila identifica el mes por su primer día, cuenta solo las fechas con posiciones dentro del rango y devuelve el P&L en pesos y el retorno como razón. Por ejemplo, un retorno de 0,01 representa un 1 %. Si no hay posiciones en el rango, la consulta devuelve cero filas.

Al revisar la consulta de diagnóstico detecté que SQL Server dividía dos sumas decimales antes de convertir el cociente a `float`. La división dejaba seis decimales en la tasa diaria. En la consulta mensual convierto el numerador antes de dividir y conservo los montos en pesos como decimales. En enero de 2023, para el portafolio 7, el cálculo con mayor precisión dio **−0,038953 %** frente al **−0,038771 %** de la consulta medida anteriormente. El P&L sigue siendo **−23.804,8236 pesos**. El mismo control, componiendo los 260 días del año con mayor precisión, dio **5,326416 %**.

En la prueba para todo 2023, SQL Server devolvió 12 meses en **18 ms**, con **145 lecturas lógicas** de `PositionDaily`. Verifiqué el P&L y el retorno de cada mes multiplicando por separado los factores diarios. El rango del 30 de enero de 2023 al 3 de enero de 2024 produjo 13 meses, con dos fechas en el primer enero y tres en el segundo. También comprobé un solo día de otro portafolio y un sábado sin datos.

Para los casos límite, si el valor inicial total de un día es cero, el retorno mensual queda indefinido (`NULL`) y el P&L sigue disponible. Un día con retorno −100 % lleva el retorno compuesto a −100 %. Comprobé además los tres ejemplos del enunciado con datos temporales: −1 %, +3,95 % y aproximadamente +0,4545 %.

## Decisión de diseño para la API: acumulado por rango y resumen a una fecha

Para estos dos endpoints elegí agrupar las posiciones en SQL Server y componer los retornos diarios en C#. El enunciado admite ambas formas de composición y pide aislar el cálculo para probarlo. Tomé la decisión al diseñar la API. Las mediciones anteriores comparan consultas e índices en SQL Server. La comparación de tiempos de extremo a extremo entre SQL y C# queda sin medir.

La [consulta de totales diarios](../db/queries/daily_totals_by_range.sql) filtra por portafolio y fechas y suma el P&L y el valor inicial de los instrumentos. Para el portafolio 7 en 2023, las 26.000 posiciones quedan reducidas a 260 filas, una por día. Si consulto todo el histórico cargado de un portafolio, la aplicación recibe como máximo 2.609 filas.

En C# implementé una [función de acumulación](../src/FinPerf.Performance/ReturnCalculator.cs) que suma el P&L y multiplica los factores diarios usando `decimal`. Devuelve el retorno como razón y deja el redondeo para la presentación. Así puedo verificar con un test unitario que +10 % seguido de −10 % da −1 %, sin levantar la base. Para un rango usaré todos los días devueltos. Para MTD, QTD, YTD e ITD haré una sola consulta hasta el corte y aplicaré la misma regla a los días de cada período. Verifiqué la función en .NET 10 con siete [pruebas xUnit](../tests/FinPerf.Performance.Tests/ReturnCalculatorTests.cs), incluidos los tres ejemplos del enunciado y el rango del 31 de enero al 1 de febrero de 2023 (30.280,0165 pesos y 0,053939 %). Las pruebas usan datos en memoria. El endpoint de rango ya conecta la consulta con la función. En la prueba HTTP contra FinPerf, el rango de dos días respondió 200 con 30.280,0165 pesos de P&L y 0,053939 % de retorno. El año 2023 respondió con 260 días, 2.856.350,8692 pesos y 5,326416 %. El resumen a una fecha sigue pendiente.

Resolver el acumulado completo en SQL permitiría enviar una sola fila por rango o cuatro resultados para el resumen. Con la opción elegida acepto transferir las filas diarias para poder reutilizar y probar la regla directamente en la aplicación. La consulta mensual ya compone en SQL y devuelve una fila por mes. Sus resultados sirven de control: para un rango que coincida con un mes completo, ambas implementaciones deben coincidir a la precisión publicada, incluidos los casos de retorno indefinido.

Al integrar la API mediré la petición completa, incluida la transferencia, con un año y con todo el histórico de un portafolio. Si el resumen histórico resulta lento, compararé ambas opciones con los mismos datos y resultados antes de cambiar la distribución del trabajo.
