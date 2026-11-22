# FinPerf

API de rendimiento de portafolios con ASP.NET Core (.NET 10) y SQL Server 2022.

## Levantar una copia nueva de la base

Desde la raíz del repositorio, en PowerShell con Docker Desktop en ejecución, define la contraseña de desarrollo en la sesión y guárdala para reutilizar el volumen:

```powershell
$claveSql = Read-Host 'Contraseña de sa para FinPerf' -AsSecureString
$env:SA_PASSWORD = [System.Net.NetworkCredential]::new('', $claveSql).Password
Remove-Variable claveSql
docker compose up -d
docker compose logs -f db-init
```

La inicialización debe terminar con `Base FinPerf lista.` en la primera carga. Con la base creada, aplica la migración del índice:

```powershell
Get-Content -Raw db/migrations/001_position_daily_portfolio_date.sql | docker exec -i finperf-sql bash -c 'SQLCMDPASSWORD=$MSSQL_SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -d FinPerf'
```

Para trabajar con la base local ya cargada desde el proyecto Compose `recursos`, usa directamente el contenedor `finperf-sql` en ejecución y su contraseña existente. El diagnóstico y las mediciones se encuentran en [docs/sql-diagnostico.md](docs/sql-diagnostico.md).

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

Queda por integrar en la API el listado de portafolios.
