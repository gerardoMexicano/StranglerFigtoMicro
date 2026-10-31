using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Xml;
using System.Data.SqlClient;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Owin.Security.Cookies;
using WebApp_MVC.Backend;
using WebApp_MVC.Models;

namespace WebApp_MVC.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        // Interruptor del Strangler Fig (Web.config backend:PaginaAntiguaNuevoBackend):
        // "api" lee de 05_backend (/api/taxes/consolidated-report), "sql" consulta taxes_proto directo como antes
        private static bool UsarApi =>
            string.Equals(ConfigurationManager.AppSettings["backend:PaginaAntiguaNuevoBackend"], "api", StringComparison.OrdinalIgnoreCase);

        [AllowAnonymous]
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Menu()
        {
            return View();
        }

        public async Task<ActionResult> PaginaAntiguaNuevoBackend(DateTime? fechaInicio, DateTime? fechaFin, int page = 1, int pageSize = 50)
        {
            var model = new List<Dictionary<string, object>>();
            bool usarApi = UsarApi;

            ViewBag.UsarApi = usarApi;
            ViewBag.Origen = usarApi ? "backend nuevo (/api/taxes/consolidated-report)" : "SQL directo (taxes_proto)";
            ViewBag.Page = Math.Max(1, page);
            ViewBag.PageSize = pageSize < 1 || pageSize > 500 ? 50 : pageSize;
            ViewBag.TotalCount = 0;
            ViewBag.TotalPages = 0;
            ViewBag.HasPreviousPage = false;
            ViewBag.HasNextPage = false;

            // Si no hay fechas, no cargamos los datos (estado inicial)
            if (!fechaInicio.HasValue && !fechaFin.HasValue)
            {
                ViewBag.Message = "Por favor, utilice el filtro de fechas para consultar los registros.";
                return View(model);
            }

            // Para mantener los valores en la vista
            ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
            ViewBag.FechaFin = fechaFin?.ToString("yyyy-MM-dd");

            if (usarApi)
            {
                DateTime startD = fechaInicio ?? new DateTime(2020, 1, 1);
                DateTime endD = fechaFin ?? new DateTime(2026, 12, 31);
                int paginaActual = (int)ViewBag.Page;
                int tamanoPagina = (int)ViewBag.PageSize;

                string ruta = string.Format(
                    CultureInfo.InvariantCulture,
                    "taxes/consolidated-report?startDate={0:yyyy-MM-dd}&endDate={1:yyyy-MM-dd}&page={2}&pageSize={3}",
                    startD, endD, paginaActual, tamanoPagina);

                try
                {
                    var pagina = await BackendApi.ObtenerAsync<PaginaReporteConsolidado>(HttpContext.GetOwinContext(), ruta);
                    model = pagina.ItemsComoDiccionarios();

                    ViewBag.Page = pagina.Page;
                    ViewBag.PageSize = pagina.PageSize;
                    ViewBag.TotalCount = pagina.TotalCount;
                    ViewBag.TotalPages = pagina.TotalPages;
                    ViewBag.HasPreviousPage = pagina.HasPreviousPage;
                    ViewBag.HasNextPage = pagina.HasNextPage;
                }
                catch (BackendSinSesionException)
                {
                    // Keycloak ya no renueva el token: se cierra solo la sesión local y se recarga la página
                    // para que OWIN vuelva a pasar por Keycloak.
                    HttpContext.GetOwinContext().Authentication.SignOut(CookieAuthenticationDefaults.AuthenticationType);
                    return Redirect(Request.RawUrl);
                }
                catch (BackendApiException ex)
                {
                    ViewBag.Error = "No se pudo consultar el backend: " + ex.Message;
                }
                catch (HttpRequestException)
                {
                    ViewBag.Error = "No se pudo conectar con el backend.";
                }
                catch (TaskCanceledException)
                {
                    ViewBag.Error = "El backend tardó demasiado en responder.";
                }

                return View(model);
            }

            string connectionString = ConfigurationManager.ConnectionStrings["ConexionTaxesProto"]?.ConnectionString;
            
            if (!string.IsNullOrEmpty(connectionString))
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        
                        string query = @"
                            SELECT 
                                c.UUID, c.EmisorRFC, c.ReceptorRFC, c.Total, c.Fecha,
                                comp.TipoComplemento,
                                nr.NumEmpleado, nr.Departamento, nr.Puesto, nr.TotalPercepciones, nr.TotalDeducciones,
                                nm.TipoMovimiento, nm.Concepto, nm.Importe
                            FROM CfdiCore c
                            LEFT JOIN CfdiComplemento comp ON c.UUID = comp.UUID
                            LEFT JOIN NominaResumen nr ON c.UUID = nr.UUID
                            LEFT JOIN NominaMovimiento nm ON c.UUID = nm.UUID
                            WHERE 1=1
                        ";
                        
                        if (fechaInicio.HasValue)
                        {
                            query += " AND c.Fecha >= @FechaInicio";
                        }
                        if (fechaFin.HasValue)
                        {
                            // Incluir hasta el final del día
                            query += " AND c.Fecha <= @FechaFin";
                        }

                        query += " ORDER BY c.Fecha DESC";
                        
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            if (fechaInicio.HasValue) 
                                cmd.Parameters.AddWithValue("@FechaInicio", fechaInicio.Value);
                            if (fechaFin.HasValue) 
                                cmd.Parameters.AddWithValue("@FechaFin", fechaFin.Value.Date.AddDays(1).AddSeconds(-1));

                            cmd.CommandTimeout = 300; 
                            
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var row = new Dictionary<string, object>();
                                    for (int i = 0; i < reader.FieldCount; i++)
                                    {
                                        row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                    }
                                    model.Add(row);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ViewBag.Error = "Error al obtener los datos: " + ex.Message;
                }
            }
            else
            {
                ViewBag.Error = "No se encontró la cadena de conexión 'ConexionTaxesProto'.";
            }

            return View(model);
        }

        public ActionResult PaginaAngular()
        {
            return View();
        }

        // Endpoint para la comparativa real desde /taxes/Home/PaginaAngular.
        // Permite seleccionar de manera controlada qué backend atiende la operación:
        // - backend="legacy": procesa directamente en ASP.NET MVC (.NET Framework 4.8)
        // - backend="net10": enruta la petición autenticada con JWT hacia 05_backend (.NET 10)
        [HttpGet]
        public async Task<ActionResult> CompararSismos(string backend = "legacy", int count = 5000, double minMagnitude = 1.0, int seed = 2026)
        {
            if (string.Equals(backend, "net10", StringComparison.OrdinalIgnoreCase))
            {
                string ruta = string.Format(
                    CultureInfo.InvariantCulture,
                    "taxes/seismic-geojson?count={0}&minMagnitude={1}&seed={2}",
                    count, minMagnitude, seed);

                try
                {
                    var resultadoNet10 = await BackendApi.ObtenerAsync<ResultadoComparativaSismica>(HttpContext.GetOwinContext(), ruta);
                    return Content(Newtonsoft.Json.JsonConvert.SerializeObject(resultadoNet10), "application/json");
                }
                catch (BackendSinSesionException)
                {
                    HttpContext.GetOwinContext().Authentication.SignOut(CookieAuthenticationDefaults.AuthenticationType);
                    Response.StatusCode = 401;
                    Response.TrySkipIisCustomErrors = true;
                    return Content(
                        Newtonsoft.Json.JsonConvert.SerializeObject(new { title = "Sesión expirada", detail = "La sesión con el backend venció. Recargue la página." }),
                        "application/json");
                }
                catch (BackendApiException ex)
                {
                    Response.StatusCode = (int)ex.Estado;
                    Response.TrySkipIisCustomErrors = true;
                    return Content(
                        Newtonsoft.Json.JsonConvert.SerializeObject(new { title = "Error de validación o autorización en .NET 10", detail = ex.Message }),
                        "application/json");
                }
                catch (HttpRequestException)
                {
                    Response.StatusCode = 502;
                    Response.TrySkipIisCustomErrors = true;
                    return Content(
                        Newtonsoft.Json.JsonConvert.SerializeObject(new { title = "Error de conexión", detail = "No se pudo conectar con 05_backend (.NET 10)." }),
                        "application/json");
                }
                catch (TaskCanceledException)
                {
                    Response.StatusCode = 504;
                    Response.TrySkipIisCustomErrors = true;
                    return Content(
                        Newtonsoft.Json.JsonConvert.SerializeObject(new { title = "Tiempo de espera agotado", detail = "El backend .NET 10 tardó demasiado en responder." }),
                        "application/json");
                }
            }

            // Implementación original conservada en el backend legado (.NET Framework 4.8)
            // Reproduce exactamente las mismas reglas de negocio, validaciones y algoritmo determinista.
            if (count < 1 || count > 10000)
            {
                Response.StatusCode = 400;
                Response.TrySkipIisCustomErrors = true;
                return Content(
                    Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        title = "Error de validación (.NET Framework 4.8)",
                        detail = "La cantidad de sismos debe estar entre 1 y 10000.",
                        errorCode = "Seismic.InvalidCount"
                    }),
                    "application/json");
            }

            if (double.IsNaN(minMagnitude) || minMagnitude < 0.0 || minMagnitude > 10.0)
            {
                Response.StatusCode = 400;
                Response.TrySkipIisCustomErrors = true;
                return Content(
                    Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        title = "Error de validación (.NET Framework 4.8)",
                        detail = "La magnitud mínima debe estar entre 0.0 y 10.0.",
                        errorCode = "Seismic.InvalidMagnitude"
                    }),
                    "application/json");
            }

            var sw = Stopwatch.StartNew();
            long memBefore = GC.GetTotalMemory(false);

            uint state = unchecked((uint)seed);
            if (state == 0)
            {
                state = 2026u;
            }

            var points = new List<PuntoSismico>(count);
            double sumMag = 0;
            double maxMag = 0;

            for (int i = 0; i < count; i++)
            {
                double rLat = NextUnitDouble(ref state);
                double rLng = NextUnitDouble(ref state);
                double rMag = NextUnitDouble(ref state);

                double lat = Math.Round(14.0 + (rLat * 18.0), 4);
                double lng = Math.Round(-118.0 + (rLng * 32.0), 4);
                double mag = Math.Round(1.0 + (rMag * 5.0), 2);

                if (mag >= minMagnitude)
                {
                    points.Add(new PuntoSismico { Lat = lat, Lng = lng, Mag = mag });
                    sumMag += mag;
                    if (mag > maxMag)
                    {
                        maxMag = mag;
                    }
                }
            }

            double avgMag = points.Count > 0 ? Math.Round(sumMag / points.Count, 2) : 0.0;
            long memAfter = GC.GetTotalMemory(false);
            long allocatedBytes = Math.Max(1024L, memAfter - memBefore);
            double memoryMb = Math.Max(0.01, Math.Round(allocatedBytes / (1024.0 * 1024.0), 2));

            var resultadoLegado = new ResultadoComparativaSismica
            {
                Engine = ".NET Framework 4.8 (ASP.NET MVC)",
                RequestedCount = count,
                ReturnedCount = points.Count,
                MinMagnitude = minMagnitude,
                Seed = seed,
                AverageMagnitude = avgMag,
                MaxMagnitude = maxMag,
                ServerElapsedMs = sw.ElapsedMilliseconds,
                MemoryMegabytes = memoryMb,
                Points = points
            };

            string jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(resultadoLegado);
            sw.Stop();
            resultadoLegado.ServerElapsedMs = sw.ElapsedMilliseconds;
            jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(resultadoLegado);

            return Content(jsonPayload, "application/json");
        }

        private static double NextUnitDouble(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) / (double)0x01000000u;
        }


        public ActionResult PaginaPrincipal()
        {
            return RedirectToAction("Menu");
        }



        [HttpPost]
        public ActionResult ProcesarXML(IEnumerable<HttpPostedFileBase> xmlFiles)
        {
            if (xmlFiles == null || !xmlFiles.Any())
            {
                return Json(new { success = false, message = "No se recibieron archivos." });
            }

            int processedCount = 0;
            var stopwatch = Stopwatch.StartNew();
            
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionTaxesProto"]?.ConnectionString;
            
            if (string.IsNullOrEmpty(connectionString))
            {
                return Json(new { success = false, message = "No se encontró la cadena de conexión 'ConexionTaxesProto'." });
            }

            try
            {
                // Procesamiento intencionalmente lento y síncrono (sin bulk copy, sin async)
                foreach (var file in xmlFiles)
                {
                    if (file != null && file.ContentLength > 0)
                    {
                        string xmlContent;
                        using (StreamReader reader = new StreamReader(file.InputStream, System.Text.Encoding.UTF8))
                        {
                            xmlContent = reader.ReadToEnd();
                        }

                        // Sanitizar string (quitar Ü y caracteres no válidos XML)
                        xmlContent = xmlContent.Replace("Ü", "U").Replace("ü", "u");
                        xmlContent = new string(xmlContent.Where(c => XmlConvert.IsXmlChar(c)).ToArray());
                        
                        // Asegurarnos de leer correctamente haya o no declaración XML
                        int startIndex = xmlContent.IndexOf("<cfdi:Comprobante");
                        if (startIndex > 0)
                        {
                            xmlContent = xmlContent.Substring(startIndex);
                        }

                        XmlDocument xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(xmlContent);

                        XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
                        nsmgr.AddNamespace("cfdi", "http://www.sat.gob.mx/cfd/4");
                        nsmgr.AddNamespace("tfd", "http://www.sat.gob.mx/TimbreFiscalDigital");
                        nsmgr.AddNamespace("nomina12", "http://www.sat.gob.mx/nomina12");

                        // Extraer datos básicos
                        XmlNode comprobanteNode = xmlDoc.SelectSingleNode("/cfdi:Comprobante", nsmgr);
                        XmlNode emisorNode = xmlDoc.SelectSingleNode("/cfdi:Comprobante/cfdi:Emisor", nsmgr);
                        XmlNode receptorNode = xmlDoc.SelectSingleNode("/cfdi:Comprobante/cfdi:Receptor", nsmgr);
                        XmlNode tfdNode = xmlDoc.SelectSingleNode("/cfdi:Comprobante/cfdi:Complemento/tfd:TimbreFiscalDigital", nsmgr);

                        if (comprobanteNode != null && tfdNode != null && emisorNode != null && receptorNode != null)
                        {
                            string uuidStr = tfdNode.Attributes["UUID"]?.Value;
                            Guid uuid = Guid.TryParse(uuidStr, out Guid parsedGuid) ? parsedGuid : Guid.NewGuid();
                            
                            using (SqlConnection conn = new SqlConnection(connectionString))
                            {
                                conn.Open();
                                
                                // ========== CfdiCore ==========
                                string sqlCore = @"
                                    IF NOT EXISTS (SELECT 1 FROM CfdiCore WHERE UUID = @UUID)
                                    BEGIN
                                        INSERT INTO CfdiCore (
                                            UUID, TipoComprobante, Serie, Folio, Fecha, Moneda, SubTotal, Descuento, Total, 
                                            MetodoPago, LugarExpedicion, EmisorRFC, EmisorNombre, ReceptorRFC, ReceptorNombre, 
                                            UsoCFDI, FechaTimbrado, RfcProvCertif, XmlOriginal
                                        ) VALUES (
                                            @UUID, @TipoComprobante, @Serie, @Folio, @Fecha, @Moneda, @SubTotal, @Descuento, @Total, 
                                            @MetodoPago, @LugarExpedicion, @EmisorRFC, @EmisorNombre, @ReceptorRFC, @ReceptorNombre, 
                                            @UsoCFDI, @FechaTimbrado, @RfcProvCertif, @XmlOriginal
                                        )
                                    END";
                                
                                using (SqlCommand cmd = new SqlCommand(sqlCore, conn))
                                {
                                    cmd.Parameters.AddWithValue("@UUID", uuid);
                                    cmd.Parameters.AddWithValue("@TipoComprobante", comprobanteNode.Attributes["TipoDeComprobante"]?.Value ?? "I");
                                    cmd.Parameters.AddWithValue("@Serie", (object)comprobanteNode.Attributes["Serie"]?.Value ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Folio", (object)comprobanteNode.Attributes["Folio"]?.Value ?? DBNull.Value);
                                    
                                    DateTime.TryParse(comprobanteNode.Attributes["Fecha"]?.Value, out DateTime fecha);
                                    cmd.Parameters.AddWithValue("@Fecha", fecha == DateTime.MinValue ? DateTime.Now : fecha);
                                    
                                    cmd.Parameters.AddWithValue("@Moneda", (object)comprobanteNode.Attributes["Moneda"]?.Value ?? DBNull.Value);
                                    
                                    decimal.TryParse(comprobanteNode.Attributes["SubTotal"]?.Value, out decimal subTotal);
                                    cmd.Parameters.AddWithValue("@SubTotal", subTotal);
                                    
                                    decimal.TryParse(comprobanteNode.Attributes["Descuento"]?.Value, out decimal descuento);
                                    cmd.Parameters.AddWithValue("@Descuento", descuento);
                                    
                                    decimal.TryParse(comprobanteNode.Attributes["Total"]?.Value, out decimal total);
                                    cmd.Parameters.AddWithValue("@Total", total);
                                    
                                    cmd.Parameters.AddWithValue("@MetodoPago", (object)comprobanteNode.Attributes["MetodoPago"]?.Value ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@LugarExpedicion", (object)comprobanteNode.Attributes["LugarExpedicion"]?.Value ?? DBNull.Value);
                                    
                                    cmd.Parameters.AddWithValue("@EmisorRFC", emisorNode.Attributes["Rfc"]?.Value ?? "");
                                    cmd.Parameters.AddWithValue("@EmisorNombre", (object)emisorNode.Attributes["Nombre"]?.Value ?? DBNull.Value);
                                    
                                    cmd.Parameters.AddWithValue("@ReceptorRFC", receptorNode.Attributes["Rfc"]?.Value ?? "");
                                    cmd.Parameters.AddWithValue("@ReceptorNombre", (object)receptorNode.Attributes["Nombre"]?.Value ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@UsoCFDI", (object)receptorNode.Attributes["UsoCFDI"]?.Value ?? DBNull.Value);
                                    
                                    DateTime.TryParse(tfdNode.Attributes["FechaTimbrado"]?.Value, out DateTime fechaTimbrado);
                                    cmd.Parameters.AddWithValue("@FechaTimbrado", fechaTimbrado == DateTime.MinValue ? (object)DBNull.Value : fechaTimbrado);
                                    
                                    cmd.Parameters.AddWithValue("@RfcProvCertif", (object)tfdNode.Attributes["RfcProvCertif"]?.Value ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@XmlOriginal", xmlContent);
                                    
                                    cmd.ExecuteNonQuery();
                                }

                                // ========== Complementos ==========
                                XmlNode complementoNode = xmlDoc.SelectSingleNode("/cfdi:Comprobante/cfdi:Complemento", nsmgr);
                                if (complementoNode != null)
                                {
                                    // 1. CfdiComplemento
                                    string sqlComp = @"
                                        IF NOT EXISTS (SELECT 1 FROM CfdiComplemento WHERE UUID = @UUID)
                                        BEGIN
                                            INSERT INTO CfdiComplemento (UUID, TipoComplemento, JsonData, XmlFragment)
                                            VALUES (@UUID, @TipoComplemento, @JsonData, @XmlFragment)
                                        END";
                                    using (SqlCommand cmdComp = new SqlCommand(sqlComp, conn))
                                    {
                                        cmdComp.Parameters.AddWithValue("@UUID", uuid);
                                        string tipoComp = complementoNode.SelectSingleNode("nomina12:Nomina", nsmgr) != null ? "Nomina12" : "TFD/Otro";
                                        cmdComp.Parameters.AddWithValue("@TipoComplemento", tipoComp);
                                        
                                        // Convertir TODO el nodo complemento a JSON
                                        string jsonComp = Newtonsoft.Json.JsonConvert.SerializeXmlNode(complementoNode);
                                        cmdComp.Parameters.AddWithValue("@JsonData", jsonComp);
                                        
                                        // Guardar el XML completo
                                        cmdComp.Parameters.AddWithValue("@XmlFragment", xmlContent);
                                        
                                        cmdComp.ExecuteNonQuery();
                                    }
                                }

                                // ========== Nomina ==========
                                XmlNode nominaNode = xmlDoc.SelectSingleNode("/cfdi:Comprobante/cfdi:Complemento/nomina12:Nomina", nsmgr);
                                if (nominaNode != null)
                                {
                                    // 2. NominaResumen
                                    XmlNode receptorNomina = nominaNode.SelectSingleNode("nomina12:Receptor", nsmgr);
                                    XmlNode percepcionesNode = nominaNode.SelectSingleNode("nomina12:Percepciones", nsmgr);
                                    
                                    decimal totalGravado = 0;
                                    decimal totalExento = 0;
                                    if (percepcionesNode != null)
                                    {
                                        decimal.TryParse(percepcionesNode.Attributes["TotalGravado"]?.Value, out totalGravado);
                                        decimal.TryParse(percepcionesNode.Attributes["TotalExento"]?.Value, out totalExento);
                                    }

                                    string sqlNomina = @"
                                        IF NOT EXISTS (SELECT 1 FROM NominaResumen WHERE UUID = @UUID)
                                        BEGIN
                                            INSERT INTO NominaResumen (
                                                UUID, TipoNomina, FechaPago, FechaInicialPago, FechaFinalPago, NumDiasPagados, 
                                                TotalPercepciones, TotalDeducciones, TotalGravado, TotalExento, NumEmpleado, Departamento, Puesto, 
                                                TipoContrato, TipoRegimen, PeriodicidadPago, SalarioBaseCotApor, SalarioDiarioIntegrado
                                            ) VALUES (
                                                @UUID, @TipoNomina, @FechaPago, @FechaInicialPago, @FechaFinalPago, @NumDiasPagados, 
                                                @TotalPercepciones, @TotalDeducciones, @TotalGravado, @TotalExento, @NumEmpleado, @Departamento, @Puesto, 
                                                @TipoContrato, @TipoRegimen, @PeriodicidadPago, @SalarioBaseCotApor, @SalarioDiarioIntegrado
                                            )
                                        END";

                                    using (SqlCommand cmdNom = new SqlCommand(sqlNomina, conn))
                                    {
                                        cmdNom.Parameters.AddWithValue("@UUID", uuid);
                                        cmdNom.Parameters.AddWithValue("@TipoNomina", (object)nominaNode.Attributes["TipoNomina"]?.Value ?? DBNull.Value);
                                        
                                        DateTime.TryParse(nominaNode.Attributes["FechaPago"]?.Value, out DateTime fechaPago);
                                        cmdNom.Parameters.AddWithValue("@FechaPago", fechaPago == DateTime.MinValue ? (object)DBNull.Value : fechaPago);
                                        
                                        DateTime.TryParse(nominaNode.Attributes["FechaInicialPago"]?.Value, out DateTime fechaInicialPago);
                                        cmdNom.Parameters.AddWithValue("@FechaInicialPago", fechaInicialPago == DateTime.MinValue ? (object)DBNull.Value : fechaInicialPago);
                                        
                                        DateTime.TryParse(nominaNode.Attributes["FechaFinalPago"]?.Value, out DateTime fechaFinalPago);
                                        cmdNom.Parameters.AddWithValue("@FechaFinalPago", fechaFinalPago == DateTime.MinValue ? (object)DBNull.Value : fechaFinalPago);
                                        
                                        decimal.TryParse(nominaNode.Attributes["NumDiasPagados"]?.Value, out decimal numDiasPagados);
                                        cmdNom.Parameters.AddWithValue("@NumDiasPagados", numDiasPagados);
                                        
                                        decimal.TryParse(nominaNode.Attributes["TotalPercepciones"]?.Value, out decimal totalPercepciones);
                                        cmdNom.Parameters.AddWithValue("@TotalPercepciones", totalPercepciones);
                                        
                                        decimal.TryParse(nominaNode.Attributes["TotalDeducciones"]?.Value, out decimal totalDeducciones);
                                        cmdNom.Parameters.AddWithValue("@TotalDeducciones", totalDeducciones);
                                        
                                        cmdNom.Parameters.AddWithValue("@TotalGravado", totalGravado);
                                        cmdNom.Parameters.AddWithValue("@TotalExento", totalExento);

                                        cmdNom.Parameters.AddWithValue("@NumEmpleado", receptorNomina?.Attributes["NumEmpleado"]?.Value ?? "");
                                        cmdNom.Parameters.AddWithValue("@Departamento", (object)receptorNomina?.Attributes["Departamento"]?.Value ?? DBNull.Value);
                                        cmdNom.Parameters.AddWithValue("@Puesto", (object)receptorNomina?.Attributes["Puesto"]?.Value ?? DBNull.Value);
                                        cmdNom.Parameters.AddWithValue("@TipoContrato", (object)receptorNomina?.Attributes["TipoContrato"]?.Value ?? DBNull.Value);
                                        cmdNom.Parameters.AddWithValue("@TipoRegimen", (object)receptorNomina?.Attributes["TipoRegimen"]?.Value ?? DBNull.Value);
                                        cmdNom.Parameters.AddWithValue("@PeriodicidadPago", (object)receptorNomina?.Attributes["PeriodicidadPago"]?.Value ?? DBNull.Value);
                                        
                                        decimal.TryParse(receptorNomina?.Attributes["SalarioBaseCotApor"]?.Value, out decimal salarioBase);
                                        cmdNom.Parameters.AddWithValue("@SalarioBaseCotApor", salarioBase);
                                        
                                        decimal.TryParse(receptorNomina?.Attributes["SalarioDiarioIntegrado"]?.Value, out decimal sdi);
                                        cmdNom.Parameters.AddWithValue("@SalarioDiarioIntegrado", sdi);
                                        
                                        cmdNom.ExecuteNonQuery();
                                    }

                                    // 3. NominaMovimiento - Percepciones
                                    XmlNodeList percepciones = nominaNode.SelectNodes("nomina12:Percepciones/nomina12:Percepcion", nsmgr);
                                    if (percepciones != null)
                                    {
                                        string sqlPerc = @"
                                            INSERT INTO NominaMovimiento (UUID, TipoMovimiento, TipoCodigo, Clave, Concepto, ImporteGravado, ImporteExento, Importe)
                                            VALUES (@UUID, 'Percepcion', @TipoCodigo, @Clave, @Concepto, @ImporteGravado, @ImporteExento, @Importe)";
                                        foreach (XmlNode perc in percepciones)
                                        {
                                            using (SqlCommand cmdPerc = new SqlCommand(sqlPerc, conn))
                                            {
                                                decimal.TryParse(perc.Attributes["ImporteGravado"]?.Value, out decimal gravado);
                                                decimal.TryParse(perc.Attributes["ImporteExento"]?.Value, out decimal exento);

                                                cmdPerc.Parameters.AddWithValue("@UUID", uuid);
                                                cmdPerc.Parameters.AddWithValue("@TipoCodigo", (object)perc.Attributes["TipoPercepcion"]?.Value ?? DBNull.Value);
                                                cmdPerc.Parameters.AddWithValue("@Clave", (object)perc.Attributes["Clave"]?.Value ?? DBNull.Value);
                                                cmdPerc.Parameters.AddWithValue("@Concepto", (object)perc.Attributes["Concepto"]?.Value ?? DBNull.Value);
                                                cmdPerc.Parameters.AddWithValue("@ImporteGravado", gravado);
                                                cmdPerc.Parameters.AddWithValue("@ImporteExento", exento);
                                                cmdPerc.Parameters.AddWithValue("@Importe", gravado + exento);
                                                cmdPerc.ExecuteNonQuery();
                                            }
                                        }
                                    }

                                    // 4. NominaMovimiento - Deducciones
                                    XmlNodeList deducciones = nominaNode.SelectNodes("nomina12:Deducciones/nomina12:Deduccion", nsmgr);
                                    if (deducciones != null)
                                    {
                                        string sqlDeduc = @"
                                            INSERT INTO NominaMovimiento (UUID, TipoMovimiento, TipoCodigo, Clave, Concepto, ImporteGravado, ImporteExento, Importe)
                                            VALUES (@UUID, 'Deduccion', @TipoCodigo, @Clave, @Concepto, 0, 0, @Importe)";
                                        foreach (XmlNode deduc in deducciones)
                                        {
                                            using (SqlCommand cmdDeduc = new SqlCommand(sqlDeduc, conn))
                                            {
                                                decimal.TryParse(deduc.Attributes["Importe"]?.Value, out decimal importe);

                                                cmdDeduc.Parameters.AddWithValue("@UUID", uuid);
                                                cmdDeduc.Parameters.AddWithValue("@TipoCodigo", (object)deduc.Attributes["TipoDeduccion"]?.Value ?? DBNull.Value);
                                                cmdDeduc.Parameters.AddWithValue("@Clave", (object)deduc.Attributes["Clave"]?.Value ?? DBNull.Value);
                                                cmdDeduc.Parameters.AddWithValue("@Concepto", (object)deduc.Attributes["Concepto"]?.Value ?? DBNull.Value);
                                                cmdDeduc.Parameters.AddWithValue("@Importe", importe);
                                                cmdDeduc.ExecuteNonQuery();
                                            }
                                        }
                                    }
                                }
                                
                                // No usamos BulkCopy deliberadamente para que sea lento
                            }
                            processedCount++;
                        }
                    }
                }
                stopwatch.Stop();
                return Json(new { success = true, message = $"Se procesaron {processedCount} XMLs en {stopwatch.ElapsedMilliseconds} ms." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error durante el procesamiento: " + ex.Message });
            }
        }
    }
}
