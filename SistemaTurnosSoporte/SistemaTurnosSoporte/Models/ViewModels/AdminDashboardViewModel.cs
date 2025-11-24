using System;
using System.Collections.Generic;

namespace SistemaSoporte.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalSolicitudes { get; set; }
        public double TiempoPromedioAtencion { get; set; }
        public double SatisfaccionPromedio { get; set; }
        public int TecnicosActivos { get; set; }
        public int TecnicosDisponibles { get; set; }
        public Dictionary<string, int> SolicitudesPorTipo { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> SolicitudesPorTecnico { get; set; } = new Dictionary<string, int>();
        public List<SolicitudUrgenteViewModel> SolicitudesUrgentes { get; set; } = new List<SolicitudUrgenteViewModel>();

        // Nuevas propiedades para filtros
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string PeriodoSeleccionado { get; set; } = "30dias";
    }

    public class SolicitudUrgenteViewModel
    {
        public int Id { get; set; }
        public string Titulo { get; set; }
        public string UsuarioNombre { get; set; }
        public string Departamento { get; set; }
    }

    // Nuevo DTO para estadísticas con filtros
    public class EstadisticasFiltradasRequest
    {
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string Periodo { get; set; }
    }
}