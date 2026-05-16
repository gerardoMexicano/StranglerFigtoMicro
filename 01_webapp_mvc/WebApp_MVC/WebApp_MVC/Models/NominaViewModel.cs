using System;
using System.Collections.Generic;

namespace WebApp_MVC.Models
{
    public class NominaItem
    {
        public string UUID { get; set; }
        public DateTime? Fecha { get; set; }
        public string ReceptorNombre { get; set; }
        public string ReceptorRFC { get; set; }
        public decimal Total { get; set; }
        public DateTime? FechaPago { get; set; }
        public decimal TotalPercepciones { get; set; }
        public decimal TotalDeducciones { get; set; }
        public string NumEmpleado { get; set; }
        public string Departamento { get; set; }
        public string Puesto { get; set; }
    }

    public class NominaResumenViewModel
    {
        public List<NominaItem> Nominas { get; set; }
        public decimal GranTotalPercepciones { get; set; }
        public decimal GranTotalDeducciones { get; set; }
        public decimal GranTotalNeto { get; set; }
        public Dictionary<string, decimal> CostoPorDepartamento { get; set; }
        
        public NominaResumenViewModel()
        {
            Nominas = new List<NominaItem>();
            CostoPorDepartamento = new Dictionary<string, decimal>();
        }
    }
}
