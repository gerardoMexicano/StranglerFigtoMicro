using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Gateway.Tests;

/// <summary>
/// Retiro de las apps viejas: todo /legacy (WebForms) y /taxes (MVC, "Second System Broken")
/// manda a /rutas-retiradas, salvo las páginas ya migradas a Angular que siguen bajo /legacy.
/// </summary>
public sealed class RutasRetiradasTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RutasRetiradasTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/legacy")]
    [InlineData("/legacy/")]
    [InlineData("/legacy/Menu.aspx")]
    [InlineData("/LEGACY/Dashboard.aspx")]
    [InlineData("/taxes/Home/Menu")]
    [InlineData("/taxes/Home/PaginaAntigua?x=1")]
    public async Task Get_RutaLegacyOBroken_RedirigeARutasRetiradasConLaRutaOriginal(string ruta)
    {
        using var client = CrearCliente();

        var response = await client.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var destino = response.Headers.Location?.OriginalString;
        Assert.NotNull(destino);
        Assert.StartsWith("/rutas-retiradas?desde=", destino);
        var desde = Uri.UnescapeDataString(destino["/rutas-retiradas?desde=".Length..]);
        Assert.Equal(ruta.Split('?')[0], desde);
    }

    [Fact]
    public async Task Post_RutaLegacy_TambienRedirige()
    {
        using var client = CrearCliente();

        var response = await client.PostAsync("/taxes/Home/CargarXml", new StringContent("x"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/rutas-retiradas", response.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("/legacy/AngularPage/")]
    [InlineData("/legacy/AngularPage/salas")]
    [InlineData("/api/taxes/payroll-summary")]
    [InlineData("/newapp/")]
    [InlineData("/legacyx/algo")]
    [InlineData("/taxesx")]
    public async Task Get_RutaMigradaONueva_NoSeRetira(string ruta)
    {
        using var client = CrearCliente();

        var response = await client.GetAsync(ruta);

        Assert.False(
            response.Headers.Location?.OriginalString.StartsWith("/rutas-retiradas") ?? false,
            $"{ruta} no debía mandarse a /rutas-retiradas");
    }

    [Fact]
    public async Task Get_AngularPageAspx_SigueRedirigiendoALaVersionAngular()
    {
        using var client = CrearCliente();

        var response = await client.GetAsync("/legacy/AngularPage.aspx");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/legacy/AngularPage", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Get_RutasRetiradas_Devuelve410ConMensajeYBotonAlInicio()
    {
        using var client = CrearCliente();

        var response = await client.GetAsync("/rutas-retiradas?desde=%2Flegacy%2FMenu.aspx");

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Estas rutas ya no existen", html);
        Assert.Contains("Sentimos las molestias", html);
        Assert.Contains("/legacy/Menu.aspx", html);
        Assert.Contains("<a class=\"boton\" href=\"/\">Volver al inicio</a>", html);
    }

    [Fact]
    public async Task Get_RutasRetiradas_CodificaLaRutaOriginal()
    {
        using var client = CrearCliente();

        var response = await client.GetAsync("/rutas-retiradas?desde=%3Cscript%3Ealert(1)%3C%2Fscript%3E");

        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task Get_RutasRetiradas_SinDesde_MuestraElMensajeGeneral()
    {
        using var client = CrearCliente();

        var response = await client.GetAsync("/rutas-retiradas");

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Estas rutas ya no existen", html);
    }

    [Fact]
    public async Task RutasRetiradasDesactivadas_LegacyVuelveAPasarAlProxy()
    {
        using var client = CrearCliente(new Dictionary<string, string?>
        {
            ["RutasRetiradas:Habilitado"] = "false",
        });

        var response = await client.GetAsync("/legacy/Menu.aspx");

        Assert.False(
            response.Headers.Location?.OriginalString.StartsWith("/rutas-retiradas") ?? false,
            "con el interruptor apagado /legacy debe ir al proxy como antes");
    }

    private HttpClient CrearCliente(Dictionary<string, string?>? extra = null)
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                // Destinos inalcanzables al instante: en las pruebas nada sale a IIS ni a los contenedores
                var valores = new Dictionary<string, string?>
                {
                    ["ReverseProxy:Clusters:legacy-cluster:Destinations:legacy:Address"] = "http://127.0.0.1:9",
                    ["ReverseProxy:Clusters:taxes-cluster:Destinations:taxes:Address"] = "http://127.0.0.1:9",
                    ["ReverseProxy:Clusters:backend-api-cluster:Destinations:backend-api:Address"] = "http://127.0.0.1:9",
                    ["ReverseProxy:Clusters:frontend-newapp-cluster:Destinations:frontend-newapp:Address"] = "http://127.0.0.1:9",
                };
                foreach (var (clave, valor) in extra ?? [])
                {
                    valores[clave] = valor;
                }
                config.AddInMemoryCollection(valores);
            });
        });

        return factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }
}
