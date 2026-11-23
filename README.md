# FinPerf

Consulta de rendimiento de portafolios de inversión con SQL Server 2022, ASP.NET Core (.NET 10) y React. La pantalla muestra retorno y P&L a una fecha de corte y un historial de retornos mensuales. [Repositorio con el historial de commits](https://github.com/K3th0r/Prueba-PlanVital)

## Requisitos de ejecución

Los comandos siguientes se ejecutan en PowerShell desde la carpeta que contiene `FinPerf.slnx`. Se necesita Docker Desktop en modo de contenedores Linux con Docker Compose, el SDK de .NET 10 y Node.js 24 con npm. La primera instalación descarga la imagen y las dependencias. Se usan los puertos locales 1433 (SQL Server), 5080 (API) y 5173 (React).

## Levantar una copia nueva de la base

Desde la raíz del repositorio, en PowerShell con Docker Desktop en ejecución, define la contraseña de desarrollo en la sesión. Usa una contraseña que cumpla la directiva de SQL Server (al menos ocho caracteres con mayúsculas, minúsculas y números) y consérvala para reutilizar el volumen.

```powershell
$claveSql = Read-Host 'Contraseña de sa para FinPerf' -AsSecureString
$env:SA_PASSWORD = [System.Net.NetworkCredential]::new('', $claveSql).Password
Remove-Variable claveSql
docker compose up -d
if ($LASTEXITCODE -ne 0) { throw 'No se pudo iniciar Docker Compose.' }
$init = docker compose ps -a -q db-init
if (!$init) { throw 'Falta el contenedor db-init.' }
$salida = docker wait $init
docker compose logs db-init
if ($salida -ne '0') { throw 'Falló la inicialización. Revisa el log de db-init.' }
```

La inicialización debe terminar con `Base FinPerf lista.` en la primera carga. Con la base creada, aplica la migración del índice:

```powershell
Get-Content -Raw db/migrations/001_position_daily_portfolio_date.sql | docker exec -i finperf-sql bash -c 'SQLCMDPASSWORD=$MSSQL_SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -d FinPerf'
```

El juego de prueba completo contiene 5.218.000 posiciones, 20 portafolios, 100 instrumentos y 2.609 fechas entre el 01-01-2015 y el 31-12-2024. Comprueba la carga con esta consulta. Si ya tienes el contenedor `finperf-sql` del ejercicio en ejecución, puedes usarlo con su contraseña y aplicar la misma migración.

```powershell
'SELECT COUNT_BIG(*) AS Positions, COUNT(DISTINCT PortfolioId) AS Portfolios, COUNT(DISTINCT InstrumentId) AS Instruments, COUNT(DISTINCT AsOfDate) AS TradingDays FROM dbo.PositionDaily;' | docker exec -i finperf-sql bash -c 'SQLCMDPASSWORD=$MSSQL_SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -d FinPerf'
```

El diagnóstico contiene los planes, lecturas, tiempos y valores de control del portafolio 7. [Diagnóstico SQL](docs/sql-diagnostico.md)

## Ejecutar la API

Con SQL Server disponible en el puerto local 1433 y `SA_PASSWORD` definida, configura la cadena en memoria e inicia la API:

```powershell
if (-not $env:SA_PASSWORD) { throw 'Define SA_PASSWORD en esta sesión.' }
$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
$connection.set_ConnectionString('Server=127.0.0.1,1433;Database=FinPerf;User ID=sa;Encrypt=True;TrustServerCertificate=True')
$connection['Password'] = $env:SA_PASSWORD
$env:ConnectionStrings__FinPerf = $connection.get_ConnectionString()
$connection.Clear()
dotnet run --project src/FinPerf.Api -- --urls http://127.0.0.1:5080
```

Desde otra ventana:

```powershell
Invoke-RestMethod 'http://127.0.0.1:5080/api/portfolios/7/performance?from=2023-01-31&to=2023-02-01'
```

Las dos fechas son inclusivas. El resultado de control es un P&L de 30.280,0165 pesos y un retorno de 0,053939 %. El JSON expresa `cumulativeReturn` como razón. Un período válido sin posiciones responde 200 con cero días e importes nulos. Un rango inválido responde 400 y un portafolio inexistente responde 404, con `ProblemDetails`.

Uso `Microsoft.Data.SqlClient` para ejecutar el SQL parametrizado y propagar la cancelación de la petición. El repositorio agrupa las posiciones por día con la consulta que ya se midió. `ReturnCalculator` compone los retornos en memoria.

## Pantalla de consulta en React

Inicia la API en `http://127.0.0.1:5080` siguiendo los pasos de la sección anterior. Desde la raíz de FinPerf, abre otra ventana de PowerShell. La versión de Node usada para verificar el proyecto fue 24.16.0.

```powershell
Set-Location 'src/FinPerf.Web'
npm.cmd ci
npm.cmd run dev
```

Abre [la pantalla local](http://127.0.0.1:5173/). Vite reenvía las llamadas a `/api` al proceso de ASP.NET Core. La conexión a SQL se configura únicamente en la API. Para compilar el frontend ejecuta `npm.cmd run build` en esa misma carpeta.

La pantalla parte con el primer portafolio del catálogo y el corte de ejemplo del enunciado (15-08-2023). Lee el catálogo al abrir. Cuando cambia el portafolio o el corte, solicita el resumen y los retornos mensuales en paralelo y cancela las solicitudes anteriores. La fecha mínima es la de inicio del portafolio. El historial de la tabla llega hasta el corte; una celda sin retorno se muestra con un guion. React aplica el formato `es-CL` a los porcentajes y pesos.

**Verificación manual.** Selecciona el portafolio 7 y el 15-08-2023. La tarjeta MTD y la celda de agosto de 2023 muestran +0,20 % a dos decimales. Al retroceder al 15-07-2023, agosto debe quedar vacío y la celda de julio debe coincidir con el nuevo MTD.


## Listado de portafolios

```powershell
Invoke-RestMethod 'http://127.0.0.1:5080/api/portfolios'
```

El selector recibe una lista ordenada por identificador. Cada elemento contiene `id`, `name` e `inceptionDate`. Una consulta exitosa sin filas devuelve HTTP 200 con `[]`. Un fallo inesperado de lectura devuelve HTTP 500 con `ProblemDetails`.

## Resumen a una fecha de corte

Con la API en ejecución:

```powershell
Invoke-RestMethod 'http://127.0.0.1:5080/api/portfolios/7/performance/summary?asOf=2023-08-15' | ConvertTo-Json -Depth 3
```

La respuesta incluye `mtd`, `qtd`, `ytd` e `itd`. Cada período indica sus fechas inclusivas, la cantidad de fechas con posiciones, el P&L y el retorno acumulado como razón. MTD comienza el primer día del mes, QTD el primer día del trimestre calendario e YTD el 1 de enero. ITD comienza en `Portfolio.InceptionDate`, que también limita los otros períodos si el portafolio se creó después de su inicio calendario. Un corte en fin de semana conserva la fecha solicitada y considera las posiciones disponibles hasta ella. Un período sin posiciones devuelve cero días y valores nulos.

## Retorno y P&L por mes

```powershell
Invoke-RestMethod 'http://127.0.0.1:5080/api/portfolios/7/performance/monthly?from=2023-01-30&to=2023-02-02' | ConvertTo-Json -Depth 3
```

La API valida el portafolio y las fechas. SQL agrupa las posiciones incluidas en el rango y devuelve los meses en orden cronológico. Cada fila de `months` contiene `monthStart` (primer día calendario del mes), `tradingDays`, `pnl` y `cumulativeReturn` (razón). En este ejemplo hay dos días de enero y dos de febrero. Un rango válido sin posiciones devuelve `months: []`. La vista puede usar el año y el mes de `monthStart` para construir la grilla y aplicar formato porcentual `es-CL` al retorno.

## Pruebas

```powershell
dotnet test FinPerf.slnx --configuration Release
```

Las pruebas unitarias verifican la regla financiera. Las de HTTP usan `WebApplicationFactory` con datos de prueba y comprueban el contrato del endpoint. Ambas se pueden ejecutar sin levantar Docker.

## Decisiones y supuestos

- Elegí .NET 10 y Minimal APIs para exponer cuatro consultas de lectura. `Microsoft.Data.SqlClient` permite ejecutar el mismo SQL parametrizado que medí en el diagnóstico y pasar el `CancellationToken` hasta el lector.
- SQL agrupa las posiciones de los instrumentos por portafolio y día. Para el rango y el resumen, C# divide P&L diario por capital inicial y multiplica los factores diarios. Así puedo probar la regla por separado y reutilizarla en los cuatro períodos. Para la grilla, SQL entrega directamente una fila por mes.
- Las fechas son inclusivas y el trimestre es calendario. Un día sin posiciones dentro de la cobertura se omite. Si el capital inicial total de un día es cero, el retorno del período es nulo y el P&L sigue disponible.
- El JSON expresa los retornos como razón (0,01 equivale a 1 %). React se ocupa de presentar porcentajes y pesos en `es-CL` y de conservar los filtros mientras cambian los resultados.

## Qué haría con más tiempo

El resumen con todo el histórico del portafolio 7 tardó 227,1 ms por HTTP (mediana). La consulta de totales diarios tardó 138 ms. Compararía esa implementación con una que devuelva los cuatro períodos ya compuestos en SQL y mediría el endpoint completo en las mismas condiciones antes de incorporar una preagregación. [Mediciones y tratamiento de correcciones históricas](docs/sql-diagnostico.md)

## Tiempo real aproximado

| Actividad | Tiempo |
|---|---:|
| Preparación del ambiente, estudio de fórmulas y del modelo de datos | 3 h |
| Diagnóstico SQL, cambios y mediciones | 3 h |
| API .NET y frontend React, estimados en conjunto | 1 h 30 min |
| **Total estimado** | **7 h 30 min** |
