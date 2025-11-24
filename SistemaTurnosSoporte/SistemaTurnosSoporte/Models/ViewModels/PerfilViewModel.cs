using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SistemaSoporte.ViewModels
{
    public class PerfilViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public string TipoUsuario { get; set; } = string.Empty;
        public bool EsTecnico { get; set; }
        public bool EmailConfirmado { get; set; }
        public string? Telefono { get; set; }
        public string? Especialidad { get; set; }
        public IFormFile? FotoArchivo { get; set; }
        public string? FotoPerfil { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? UltimoAcceso { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
    }

    public class CambiarFotoViewModel
    {
        [Required(ErrorMessage = "Debe seleccionar una imagen")]
        public IFormFile FotoArchivo { get; set; } = null!;
    }
}