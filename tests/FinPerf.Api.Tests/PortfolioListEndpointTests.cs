using System.Net;
using System.Net.Http.Json;
using FinPerf.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FinPerf.Api.Tests;

public sealed class PortfolioListEndpointTests
{
    // Comprueba los nombres de campos y la fecha que recibirá el selector de portafolios.
    [Fact]
    public async Task ReturnsPortfolioDetails()
    {
        using var factory = CreateFactory(() => [new PortfolioItem(7, "Portafolio 7", new(2015, 1, 1))]);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/portfolios");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var item = Assert.Single(items.EnumerateArray());
        Assert.Equal(7, item.GetProperty("id").GetInt32());
        Assert.Equal("Portafolio 7", item.GetProperty("name").GetString());
        Assert.Equal("2015-01-01", item.GetProperty("inceptionDate").GetString());
    }

    // Un catálogo sin filas sigue siendo una consulta exitosa.
    [Fact]
    public async Task ReturnsEmptyArrayWhenCatalogIsEmpty()
    {
        using var factory = CreateFactory(() => []);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/portfolios");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<PortfolioItem[]>())!);
    }

    // Un fallo de lectura se comunica por HTTP para que la vista muestre su estado de error.
    [Fact]
    public async Task ReturnsProblemDetailsWhenCatalogReadFails()
    {
        using var factory = CreateFactory(() => throw new InvalidOperationException("Error de lectura simulado."));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/portfolios");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(500, (await response.Content.ReadFromJsonAsync<ProblemDetails>())?.Status);
    }

    // Inyecta el resultado de cada escenario en el catálogo para probar la ruta HTTP.
    private static WebApplicationFactory<Program> CreateFactory(Func<IReadOnlyList<PortfolioItem>> read) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPortfolioCatalog>();
                services.AddSingleton<IPortfolioCatalog>(new TestCatalog(read));
            });
        });

    private sealed class TestCatalog(Func<IReadOnlyList<PortfolioItem>> read) : IPortfolioCatalog
    {
        // Devuelve las filas de prueba o reproduce el fallo configurado para el caso.
        public Task<IReadOnlyList<PortfolioItem>> GetPortfoliosAsync(CancellationToken cancellationToken) =>
            Task.FromResult(read());
    }
}
