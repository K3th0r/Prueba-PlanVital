using System.Data;
using System.Text.Json.Serialization;
using FinPerf.Performance;
using Microsoft.Data.SqlClient;

namespace FinPerf.Api;

public sealed record PortfolioCoverage(DateOnly InceptionDate, DateOnly? FirstDate, DateOnly? LastDate);

public sealed record MonthlyPerformance(DateOnly MonthStart, int TradingDays,
    [property: JsonPropertyName("pnl")] decimal PnL, double? CumulativeReturn);

public interface IPerformanceRepository
{
    // Devuelve las fechas de inicio y de cobertura para validar el período solicitado.
    Task<PortfolioCoverage?> GetCoverageAsync(int id, CancellationToken cancellationToken);

    // Devuelve los meses con posiciones dentro del rango, ordenados por año y mes.
    Task<IReadOnlyList<MonthlyPerformance>> GetMonthlyAsync(
        int id, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    // Devuelve una fila por fecha con el P&L y el valor inicial del portafolio.
    Task<IReadOnlyList<DailyTotal>> GetDailyTotalsAsync(
        int id, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

internal sealed class SqlPerformanceRepository(IConfiguration configuration) : IPerformanceRepository
{
    private static readonly string DailyTotalsSql = LoadSql("DailyTotalsByRange.sql");
    private static readonly string MonthlySql = LoadSql("MonthlyPerformance.sql");

    // Prepara la conexión a FinPerf con la cadena configurada para la API.
    private SqlConnection CreateConnection() => new(configuration.GetConnectionString("FinPerf")
        ?? throw new InvalidOperationException("Configura ConnectionStrings__FinPerf."));

    // Lee de SQL Server el inicio del portafolio y sus fechas con datos (primera y última). Devuelve null si el portafolio no existe.
    public async Task<PortfolioCoverage?> GetCoverageAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT p.InceptionDate,
                (SELECT TOP (1) AsOfDate FROM dbo.PositionDaily WHERE PortfolioId = p.PortfolioId ORDER BY AsOfDate),
                (SELECT TOP (1) AsOfDate FROM dbo.PositionDaily WHERE PortfolioId = p.PortfolioId ORDER BY AsOfDate DESC)
            FROM dbo.Portfolio AS p
            WHERE p.PortfolioId = @PortfolioId;
            """;
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@PortfolioId", SqlDbType.Int).Value = id;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new PortfolioCoverage(
            DateOnly.FromDateTime(reader.GetDateTime(0)),
            reader.IsDBNull(1) ? null : DateOnly.FromDateTime(reader.GetDateTime(1)),
            reader.IsDBNull(2) ? null : DateOnly.FromDateTime(reader.GetDateTime(2)));
    }

    // Consulta SQL Server y devuelve el P&L y el valor inicial por día para componer el retorno del rango.
    public async Task<IReadOnlyList<DailyTotal>> GetDailyTotalsAsync(
        int id, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        using var command = CreateRangeCommand(connection, DailyTotalsSql, id, from, to);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var days = new List<DailyTotal>();
        while (await reader.ReadAsync(cancellationToken))
        {
            days.Add(new DailyTotal(DateOnly.FromDateTime(reader.GetDateTime(0)),
                reader.GetDecimal(1), reader.GetDecimal(2)));
        }
        return days;
    }

    // Ejecuta la consulta mensual medida en SQL y conserva el retorno nulo cuando es indefinido.
    public async Task<IReadOnlyList<MonthlyPerformance>> GetMonthlyAsync(
        int id, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        using var command = CreateRangeCommand(connection, MonthlySql, id, from, to);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var months = new List<MonthlyPerformance>();
        while (await reader.ReadAsync(cancellationToken))
        {
            months.Add(new MonthlyPerformance(DateOnly.FromDateTime(reader.GetDateTime(0)),
                reader.GetInt32(1), reader.GetDecimal(2), reader.IsDBNull(3) ? null : reader.GetDouble(3)));
        }
        return months;
    }

    // Pasa el portafolio y las fechas como parámetros SQL tipados en las consultas por rango.
    private static SqlCommand CreateRangeCommand(SqlConnection connection, string sql, int id, DateOnly from, DateOnly to)
    {
        var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@PortfolioId", SqlDbType.Int).Value = id;
        command.Parameters.Add("@From", SqlDbType.Date).Value = from.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@To", SqlDbType.Date).Value = to.ToDateTime(TimeOnly.MinValue);
        return command;
    }

    // Carga el archivo SQL incluido en el ensamblado para reutilizar la consulta del diagnóstico.
    private static string LoadSql(string resourceName)
    {
        using var stream = typeof(SqlPerformanceRepository).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Falta el recurso SQL {resourceName} en el ensamblado.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
