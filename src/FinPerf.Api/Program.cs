using FinPerf.Api;

// Configura el repositorio y los errores HTTP para servir las consultas de rendimiento.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddScoped<IPerformanceRepository, SqlPerformanceRepository>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapGet("/api/portfolios/{id:int}/performance", PerformanceEndpoints.GetRange);
app.MapGet("/api/portfolios/{id:int}/performance/summary", PerformanceEndpoints.GetSummary);
app.Run();

public partial class Program { }
