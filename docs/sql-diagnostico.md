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