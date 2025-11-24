using System;
using System.ComponentModel.DataAnnotations;

namespace SistemaSoporte.Models
{
    public class ManualUsuario
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string NombreArchivo { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string TipoContenido { get; set; } = string.Empty;

        [Required]
        public byte[] Contenido { get; set; } = Array.Empty<byte>();

        public long Tamaño { get; set; }

        public DateTime FechaSubida { get; set; } = DateTime.Now;

        [StringLength(450)]
        public string? UsuarioSubioId { get; set; }

        [StringLength(100)]
        public string? UsuarioSubioNombre { get; set; }

        [StringLength(10)]
        public string Version { get; set; } = "1.0";

        public bool Activo { get; set; } = true;
    }
}