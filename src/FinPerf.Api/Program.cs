using FinPerf.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddScoped<IPerformanceRepository, SqlPerformanceRepository>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapGet("/api/portfolios/{id:int}/performance", PerformanceEndpoints.GetRange);
app.Run();

public partial class Program { }
