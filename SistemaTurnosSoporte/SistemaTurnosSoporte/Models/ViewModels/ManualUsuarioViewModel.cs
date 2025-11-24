namespace SistemaSoporte.Models
{
    public class ManualUsuarioViewModel
    {
        public int Id { get; set; }
        public string NombreArchivo { get; set; } = string.Empty;
        public string TipoContenido { get; set; } = string.Empty;
        public long TamañoBytes { get; set; }
        public DateTime FechaSubida { get; set; }
        public string? UsuarioSubioNombre { get; set; }
        public string Version { get; set; } = string.Empty;
        public bool Activo { get; set; }

        public string TamañoFormateado => FormatFileSize(TamañoBytes);

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double len = bytes;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}