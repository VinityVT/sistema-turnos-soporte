using System;
using System.ComponentModel.DataAnnotations;

namespace SistemaSoporte.Models
{
    public class Usuario
    {
        public string Id { get; set; }

        [Required]
        public string NombreCompleto { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Rol { get; set; } // "Admin", "Tecnico", "Usuario"

        public bool Activo { get; set; } = true;

        public string Departamento { get; set; }

        public string Telefono { get; set; }

        public string Posicion { get; set; }

        public DateTime? UltimoAcceso { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;
    }

    public class HistorialUsuarioViewModel
    {
        public int TicketId { get; set; }
        public string Titulo { get; set; } = null!;
        public string TipoProblema { get; set; } = null!;
        public string Estado { get; set; } = null!;
        public string Prioridad { get; set; } = null!;
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaResolucion { get; set; }
        public string TecnicoAsignado { get; set; } = null!;
        public TimeSpan? TiempoResolucion { get; set; }
        public string? ComentarioResolucion { get; set; }
        public string? ComentarioCancelacion { get; set; }
    }
}