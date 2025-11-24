using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaSoporte.Services;
using SistemaSoporte.ViewModels;
using System.Security.Claims;

namespace SistemaSoporte.Controllers
{
    [Authorize]
    public class PerfilController : Controller
    {
        private readonly IApiService _apiService;
        private readonly ILogger<PerfilController> _logger;
        private readonly IWebHostEnvironment _environment;

        public PerfilController(
            IApiService apiService,
            ILogger<PerfilController> logger,
            IWebHostEnvironment environment)
        {
            _apiService = apiService;
            _logger = logger;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                // Obtener el perfil del usuario desde la API
                var perfil = await _apiService.GetAsync<UsuarioPerfilViewModel>("api/Auth/perfil");

                if (perfil == null)
                {
                    _logger.LogWarning("No se pudo obtener el perfil del usuario desde la API");
                    return RedirectToAction("Index", "Home");
                }

                // Mapear a ViewModel
                var model = new PerfilViewModel
                {
                    Id = perfil.Id,
                    NombreCompleto = perfil.NombreCompleto,
                    Email = perfil.Email,
                    EsTecnico = perfil.EsTecnico,
                    EmailConfirmado = perfil.EmailConfirmado,
                    Telefono = perfil.Telefono,
                    Especialidad = perfil.Especialidad,
                    FechaCreacion = perfil.FechaCreacion,
                    Roles = perfil.Roles
                };

                // Determinar el tipo de usuario basado en roles
                if (perfil.Roles.Contains("Admin"))
                {
                    model.TipoUsuario = "Admin";
                }
                else if (perfil.Roles.Contains("Tecnico"))
                {
                    model.TipoUsuario = "Tecnico";
                }
                else
                {
                    model.TipoUsuario = "Usuario";
                }

                // Departamento basado en especialidad o rol
                model.Departamento = !string.IsNullOrEmpty(perfil.Especialidad) ?
                    perfil.Especialidad :
                    (perfil.EsTecnico ? "Soporte Técnico" : "Usuario General");

                // Cargar foto de perfil si existe
                model.FotoPerfil = await ObtenerFotoPerfil(perfil.Id);

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar el perfil del usuario");
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarFoto(CambiarFotoViewModel model)
        {
            try
            {
                // Obtener ID del usuario autenticado
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Cuenta");
                }

                // Validar el archivo
                if (model.FotoArchivo == null || model.FotoArchivo.Length == 0)
                {
                    return RedirectToAction(nameof(Index));
                }

                // Validar tamaño (máximo 5MB)
                if (model.FotoArchivo.Length > 5 * 1024 * 1024)
                {
                    return RedirectToAction(nameof(Index));
                }

                // Validar extensiones
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(model.FotoArchivo.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return RedirectToAction(nameof(Index));
                }

                // Crear directorio si no existe
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "perfiles");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Eliminar foto anterior si existe
                await EliminarFotoAnterior(userId);

                // Generar nombre único para el archivo
                var fileName = $"{userId}_{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                // Guardar el archivo
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.FotoArchivo.CopyToAsync(stream);
                }

                _logger.LogInformation("Foto de perfil actualizada para el usuario {UserId}", userId);

                // Redirigir para refrescar la página y mostrar la nueva foto
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar la foto de perfil");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarFoto()
        {
            try
            {
                // Obtener ID del usuario autenticado
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Cuenta");
                }

                // Eliminar foto
                await EliminarFotoAnterior(userId);

                _logger.LogInformation("Foto de perfil eliminada para el usuario {UserId}", userId);

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar la foto de perfil");
                return RedirectToAction(nameof(Index));
            }
        }

        private async Task<string?> ObtenerFotoPerfil(string userId)
        {
            try
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "perfiles");
                if (!Directory.Exists(uploadsFolder))
                {
                    return null;
                }

                // Buscar archivos que empiecen con el ID del usuario
                var files = Directory.GetFiles(uploadsFolder, $"{userId}_*");
                if (files.Length > 0)
                {
                    // Ordenar por fecha de creación y tomar la más reciente
                    var mostRecentFile = files
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.CreationTime)
                        .First();

                    var fileName = Path.GetFileName(mostRecentFile.Name);
                    return $"/uploads/perfiles/{fileName}";
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener foto de perfil para el usuario {UserId}", userId);
                return null;
            }
        }

        private async Task EliminarFotoAnterior(string userId)
        {
            try
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "perfiles");
                if (!Directory.Exists(uploadsFolder))
                {
                    return;
                }

                // Buscar y eliminar archivos que empiecen con el ID del usuario
                var files = Directory.GetFiles(uploadsFolder, $"{userId}_*");
                foreach (var file in files)
                {
                    System.IO.File.Delete(file);
                    _logger.LogInformation("Foto eliminada: {FilePath}", file);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar foto anterior para el usuario {UserId}", userId);
            }
        }
    }

    // ViewModel para recibir datos de la API
    public class UsuarioPerfilViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool EsTecnico { get; set; }
        public bool EmailConfirmado { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
        public DateTime FechaCreacion { get; set; }
        public string? Telefono { get; set; }
        public string? Especialidad { get; set; }
    }
}