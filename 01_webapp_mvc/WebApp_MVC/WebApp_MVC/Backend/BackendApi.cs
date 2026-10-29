using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Owin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WebApp_MVC.Backend
{
    // Llamadas a 05_backend (/api por nginx + gateway) con el access token del usuario.
    // Se llama desde el servidor (Controller): el token nunca llega al navegador.
    public static class BackendApi
    {
        private static readonly HttpClient Http = CrearCliente();

        // Decimales como Decimal (no Double), igual que las columnas money/decimal de SQL
        private static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            FloatParseHandling = FloatParseHandling.Decimal
        };

        private static HttpClient CrearCliente()
        {
            return new HttpClient
            {
                BaseAddress = new Uri(ConfigurationManager.AppSettings["backend:BaseUrl"].TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromSeconds(int.Parse(ConfigurationManager.AppSettings["backend:TimeoutSegundos"]))
            };
        }

        // ruta es relativa a backend:BaseUrl, p. ej. "taxes/consolidated-report?startDate=2024-01-01&endDate=2024-12-31&page=1&pageSize=50"
        public static async Task<T> ObtenerAsync<T>(IOwinContext contexto, string ruta)
        {
            string token = await TokenDeAcceso.ObtenerAsync(contexto);
            if (token == null)
            {
                throw new BackendSinSesionException();
            }

            using (var peticion = new HttpRequestMessage(HttpMethod.Get, ruta))
            {
                peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using (HttpResponseMessage respuesta = await Http.SendAsync(peticion))
                {
                    string cuerpo = await respuesta.Content.ReadAsStringAsync();

                    if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        throw new BackendSinSesionException();
                    }

                    if (!respuesta.IsSuccessStatusCode)
                    {
                        throw new BackendApiException(respuesta.StatusCode, DetalleDelProblema(cuerpo, respuesta.ReasonPhrase));
                    }

                    return JsonConvert.DeserializeObject<T>(cuerpo, Json);
                }
            }
        }

        // El backend responde errores como ProblemDetails (RFC 7807): detail, o si no title
        private static string DetalleDelProblema(string cuerpo, string porDefecto)
        {
            if (string.IsNullOrWhiteSpace(cuerpo))
            {
                return porDefecto;
            }

            try
            {
                JObject problema = JObject.Parse(cuerpo);
                return problema.Value<string>("detail") ?? problema.Value<string>("title") ?? porDefecto;
            }
            catch (JsonReaderException)
            {
                return porDefecto;
            }
        }
    }

    // PagedResult<ConsolidatedReportItemDto> del backend
    public class PaginaReporteConsolidado
    {
        public List<ReporteConsolidadoItem> Items { get; set; } = new List<ReporteConsolidadoItem>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }

        // Convierte las filas tipadas al mismo formato List<Dictionary<string, object>> que consume la vista Razor
        public List<Dictionary<string, object>> ItemsComoDiccionarios()
        {
            var resultado = new List<Dictionary<string, object>>();
            if (Items == null)
            {
                return resultado;
            }

            foreach (ReporteConsolidadoItem item in Items)
            {
                resultado.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["UUID"] = item.UUID,
                    ["EmisorRFC"] = item.EmisorRFC,
                    ["ReceptorRFC"] = item.ReceptorRFC,
                    ["Total"] = item.Total,
                    ["Fecha"] = item.Fecha,
                    ["TipoComplemento"] = item.TipoComplemento,
                    ["NumEmpleado"] = item.NumEmpleado,
                    ["Departamento"] = item.Departamento,
                    ["Puesto"] = item.Puesto,
                    ["TotalPercepciones"] = item.TotalPercepciones,
                    ["TotalDeducciones"] = item.TotalDeducciones,
                    ["TipoMovimiento"] = item.TipoMovimiento,
                    ["Concepto"] = item.Concepto,
                    ["Importe"] = item.Importe
                });
            }

            return resultado;
        }
    }

    public class ReporteConsolidadoItem
    {
        public Guid UUID { get; set; }
        public string EmisorRFC { get; set; }
        public string ReceptorRFC { get; set; }
        public decimal? Total { get; set; }
        public DateTime Fecha { get; set; }
        public string TipoComplemento { get; set; }
        public string NumEmpleado { get; set; }
        public string Departamento { get; set; }
        public string Puesto { get; set; }
        public decimal? TotalPercepciones { get; set; }
        public decimal? TotalDeducciones { get; set; }
        public string TipoMovimiento { get; set; }
        public string Concepto { get; set; }
        public decimal? Importe { get; set; }
    }

    // No hay token válido o el backend lo rechazó (401): hay que volver a hacer login
    public class BackendSinSesionException : Exception
    {
        public BackendSinSesionException()
            : base("La sesión con el backend venció.")
        {
        }
    }

    public class BackendApiException : Exception
    {
        public HttpStatusCode Estado { get; }

        public BackendApiException(HttpStatusCode estado, string detalle)
            : base(detalle)
        {
            Estado = estado;
        }
    }
}
