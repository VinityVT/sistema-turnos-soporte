using System;
using System.ComponentModel.DataAnnotations;

namespace SistemaSoporte.Models
{
    public class Ticket
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; }

        public string TecnicoAsignadoId { get; set; }

        [Required]
        [StringLength(100)]
        public string Titulo { get; set; }

        [Required]
        public string Descripcion { get; set; }

        [Required]
        public string TipoProblema { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public DateTime? FechaResolucion { get; set; }

        [Required]
        public string Estado { get; set; } = "En espera";

        [Required]
        public string Prioridad { get; set; } = "Media";

        public virtual ApplicationUser Usuario { get; set; }
        public virtual ApplicationUser TecnicoAsignado { get; set; }
        public string ComentariosTecnico { get; internal set; }
        public DateTime FechaCierre { get; internal set; }
    }
}