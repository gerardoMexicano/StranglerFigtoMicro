using System;
using System.Web.Mvc;
using System.Data.SqlClient;
using System.Configuration;
using WebApp_MVC.Models;

namespace WebApp_MVC.Controllers
{
    [Authorize]
    public class PaginaAntiguaController : Controller
    {
        public ActionResult Index(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var model = new NominaResumenViewModel();
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionTaxesProto"]?.ConnectionString;
            
            if (string.IsNullOrEmpty(connectionString))
            {
                ViewBag.Error = "No se encontró la cadena de conexión 'ConexionTaxesProto'.";
                return View(model);
            }

            // Forzar las horas a 00:00:00 y 23:59:59 en backend
            if (fechaInicio.HasValue)
            {
                fechaInicio = fechaInicio.Value.Date; // 00:00:00
            }
            if (fechaFin.HasValue)
            {
                fechaFin = fechaFin.Value.Date.AddDays(1).AddSeconds(-1); // 23:59:59
            }
            
            ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
            ViewBag.FechaFin = fechaFin?.ToString("yyyy-MM-dd");

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT 
                            c.UUID, c.Fecha, c.ReceptorNombre, c.ReceptorRFC, c.Total,
                            n.FechaPago, n.TotalPercepciones, n.TotalDeducciones, 
                            n.NumEmpleado, n.Departamento, n.Puesto
                        FROM CfdiCore c
                        INNER JOIN NominaResumen n ON c.UUID = n.UUID
                        WHERE 1=1 ";

                    if (fechaInicio.HasValue) query += " AND c.Fecha >= @FechaInicio ";
                    if (fechaFin.HasValue) query += " AND c.Fecha <= @FechaFin ";

                    query += " ORDER BY c.Fecha DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        if (fechaInicio.HasValue) cmd.Parameters.AddWithValue("@FechaInicio", fechaInicio.Value);
                        if (fechaFin.HasValue) cmd.Parameters.AddWithValue("@FechaFin", fechaFin.Value);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var item = new NominaItem
                                {
                                    UUID = reader["UUID"].ToString(),
                                    Fecha = reader["Fecha"] as DateTime?,
                                    ReceptorNombre = reader["ReceptorNombre"].ToString(),
                                    ReceptorRFC = reader["ReceptorRFC"].ToString(),
                                    Total = reader["Total"] != DBNull.Value ? Convert.ToDecimal(reader["Total"]) : 0,
                                    FechaPago = reader["FechaPago"] as DateTime?,
                                    TotalPercepciones = reader["TotalPercepciones"] != DBNull.Value ? Convert.ToDecimal(reader["TotalPercepciones"]) : 0,
                                    TotalDeducciones = reader["TotalDeducciones"] != DBNull.Value ? Convert.ToDecimal(reader["TotalDeducciones"]) : 0,
                                    NumEmpleado = reader["NumEmpleado"].ToString(),
                                    Departamento = reader["Departamento"].ToString(),
                                    Puesto = reader["Puesto"].ToString()
                                };

                                model.Nominas.Add(item);

                                model.GranTotalPercepciones += item.TotalPercepciones;
                                model.GranTotalDeducciones += item.TotalDeducciones;
                                model.GranTotalNeto += item.Total;

                                string depto = string.IsNullOrWhiteSpace(item.Departamento) ? "Sin Departamento" : item.Departamento;
                                if (model.CostoPorDepartamento.ContainsKey(depto))
                                {
                                    model.CostoPorDepartamento[depto] += item.TotalPercepciones;
                                }
                                else
                                {
                                    model.CostoPorDepartamento.Add(depto, item.TotalPercepciones);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al consultar la base de datos: " + ex.Message;
            }

            return View(model);
        }
    }
}
