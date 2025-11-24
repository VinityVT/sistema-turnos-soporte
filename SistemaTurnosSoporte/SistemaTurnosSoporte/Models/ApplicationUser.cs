using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SistemaSoporte.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Departamento { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string TipoUsuario { get; set; } = string.Empty;

        public string? FotoPerfil { get; set; }

        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        public DateTime? UltimoAcceso { get; set; }

        public virtual ICollection<Ticket> SolicitudesCreadas { get; set; } = new List<Ticket>();

        public virtual ICollection<Ticket> SolicitudesAsignadas { get; set; } = new List<Ticket>();
    }
}