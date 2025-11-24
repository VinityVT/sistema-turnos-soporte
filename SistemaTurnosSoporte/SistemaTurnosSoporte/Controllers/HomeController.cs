using Microsoft.AspNetCore.Mvc;
using SistemaSoporte.Services;
using SistemaTurnosSoporte.Models;
using System.Diagnostics;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using SistemaSoporte.Models;

namespace SistemaTurnosSoporte.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IApiService _apiService;

        public HomeController(ILogger<HomeController> logger, IApiService apiService)
        {
            _logger = logger;
            _apiService = apiService;
        }

        public async Task<IActionResult> Index()
        {
            if (User.Identity.IsAuthenticated)
            {
                try
                {
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    var tickets = await _apiService.GetAsync<List<SIGESTEC.API.DTOs.TicketDTO>>($"api/Tickets/mis-tickets");
                    ViewBag.SolicitudesRecientes = tickets?.Take(5).ToList() ?? new List<SIGESTEC.API.DTOs.TicketDTO>();
                }
                catch
                {
                    ViewBag.SolicitudesRecientes = new List<SIGESTEC.API.DTOs.TicketDTO>();
                }
            }
            else
            {
                ViewBag.SolicitudesRecientes = new List<SIGESTEC.API.DTOs.TicketDTO>();
            }

            // Obtener el teléfono de la base de datos a través de la API
            try
            {
                var configuracion = await _apiService.GetAsync<SIGESTEC.API.DTOs.ConfiguracionSistemaDTO>("api/Configuracion/TelefonoSoporte");
                ViewBag.TelefonoSoporte = configuracion?.Valor ?? "555-123-4567";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo obtener el teléfono de soporte, usando valor por defecto");
                ViewBag.TelefonoSoporte = "555-123-4567";
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarTelefono(string nuevoTelefono)
        {
            try
            {
                // Verificar si el usuario es administrador
                if (!User.IsInRole("Admin") && !User.IsInRole("Administrador"))
                {
                    return Json(new { success = false, message = "No tienes permisos para realizar esta acción" });
                }

                if (string.IsNullOrWhiteSpace(nuevoTelefono))
                {
                    return Json(new { success = false, message = "El teléfono no puede estar vacío" });
                }

                // Formatear automáticamente el teléfono
                var telefonoFormateado = FormatearTelefono(nuevoTelefono);

                if (string.IsNullOrEmpty(telefonoFormateado))
                {
                    return Json(new { success = false, message = "Formato de teléfono inválido" });
                }

                // Actualizar en la base de datos a través de la API
                var actualizacion = new
                {
                    Clave = "TelefonoSoporte",
                    Valor = telefonoFormateado
                };

                var resultado = await _apiService.PutAsync<object>("api/Configuracion", actualizacion);

                _logger.LogInformation($"Administrador {User.Identity.Name} actualizó teléfono de soporte a: {telefonoFormateado}");

                return Json(new
                {
                    success = true,
                    message = "Teléfono actualizado correctamente",
                    telefono = telefonoFormateado
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar teléfono de soporte");
                return Json(new { success = false, message = "Error al actualizar el teléfono" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubirManual(IFormFile archivo, string version = "1.0")
        {
            try
            {
                if (archivo == null || archivo.Length == 0)
                {
                    TempData["Error"] = "Por favor seleccione un archivo PDF";
                    return RedirectToAction("Index");
                }

                // Validación simple de PDF
                if (!archivo.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Error"] = "Solo se permiten archivos PDF";
                    return RedirectToAction("Index");
                }

                // Crear FormData
                using var formData = new MultipartFormDataContent();
                using var fileStream = archivo.OpenReadStream();
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(archivo.ContentType);

                formData.Add(fileContent, "Archivo", archivo.FileName);
                formData.Add(new StringContent(version ?? "1.0"), "Version");

                var resultado = await _apiService.PostFormDataAsync<SIGESTEC.API.DTOs.ManualUsuarioInfoDTO>("api/ManualUsuario/subir", formData);

                if (resultado != null)
                {
                    TempData["Success"] = $"Manual '{archivo.FileName}' subido exitosamente (v{version})";
                    _logger.LogInformation("Manual subido exitosamente: {Archivo}", archivo.FileName);
                }
                else
                {
                    TempData["Error"] = "Error al subir el manual";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir manual de usuario");
                TempData["Error"] = $"Error al subir el manual: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> DescargarManual()
        {
            try
            {
                // ✅ CORREGIDO: Usar DownloadFileAsync para descargar el archivo directamente
                var contenido = await _apiService.DownloadFileAsync("api/ManualUsuario/descargar-ultimo");

                if (contenido != null && contenido.Length > 0)
                {
                    // Obtener información del manual para el nombre y tipo de contenido
                    var manual = await _apiService.GetAsync<SIGESTEC.API.DTOs.ManualUsuarioInfoDTO>("api/ManualUsuario/ultimo-activo");

                    if (manual != null)
                    {
                        return File(contenido, manual.TipoContenido, manual.NombreArchivo);
                    }
                    else
                    {
                        // Si no puede obtener la info, usar valores por defecto
                        return File(contenido, "application/pdf", $"manual-usuario-{DateTime.Now:yyyyMMdd}.pdf");
                    }
                }
                else
                {
                    TempData["Error"] = "No se pudo descargar el manual - archivo vacío o no encontrado";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al descargar manual de usuario");
                TempData["Error"] = $"Error al descargar el manual: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GestionarManuales()
        {
            try
            {
                var manuales = await _apiService.GetAsync<List<ManualUsuarioViewModel>>("api/ManualUsuario/historial");
                return View(manuales ?? new List<ManualUsuarioViewModel>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener historial de manuales");
                TempData["Error"] = "Error al cargar el historial de manuales";
                return View(new List<ManualUsuarioViewModel>());
            }
        }

        private string FormatearTelefono(string telefono)
        {
            if (string.IsNullOrWhiteSpace(telefono))
                return null;

            // Limpiar el número (quitar espacios, guiones, paréntesis, etc.)
            var numeroLimpio = Regex.Replace(telefono, @"[^\d+]", "");

            // Si no tiene lada, agregar +52 por defecto (México)
            if (!numeroLimpio.StartsWith("+"))
            {
                // Si empieza con 52, agregar el +
                if (numeroLimpio.StartsWith("52") && numeroLimpio.Length > 2)
                {
                    numeroLimpio = "+" + numeroLimpio;
                }
                else
                {
                    // Agregar lada de México por defecto
                    numeroLimpio = "+52" + numeroLimpio;
                }
            }

            // Formatear según la lada detectada
            if (numeroLimpio.StartsWith("+52"))
            {
                // Formato México: +52 XXX XXX XXXX
                if (numeroLimpio.Length == 13) // +52 + 10 dígitos
                {
                    return $"+52 {numeroLimpio.Substring(3, 3)}-{numeroLimpio.Substring(6, 3)}-{numeroLimpio.Substring(9, 4)}";
                }
                else if (numeroLimpio.Length == 12) // +52 + 9 dígitos (sin el 1 después de lada)
                {
                    return $"+52 {numeroLimpio.Substring(3, 2)}-{numeroLimpio.Substring(5, 4)}-{numeroLimpio.Substring(9, 4)}";
                }
            }
            else if (numeroLimpio.StartsWith("+1"))
            {
                // Formato USA/Canadá: +1 (XXX) XXX-XXXX
                if (numeroLimpio.Length == 12) // +1 + 10 dígitos
                {
                    return $"+1 ({numeroLimpio.Substring(2, 3)}) {numeroLimpio.Substring(5, 3)}-{numeroLimpio.Substring(8, 4)}";
                }
            }
            else if (numeroLimpio.StartsWith("+34"))
            {
                // Formato España: +34 XXX XXX XXX
                if (numeroLimpio.Length == 12) // +34 + 9 dígitos
                {
                    return $"+34 {numeroLimpio.Substring(3, 3)} {numeroLimpio.Substring(6, 3)} {numeroLimpio.Substring(9, 3)}";
                }
            }

            // Si no coincide con ningún formato específico, retornar el número limpio
            return numeroLimpio;
        }

        private string DetectarPais(string telefono)
        {
            if (string.IsNullOrWhiteSpace(telefono))
                return "Desconocido";

            var numeroLimpio = Regex.Replace(telefono, @"[^\d+]", "");

            return numeroLimpio.StartsWith("+52") ? "México" :
                   numeroLimpio.StartsWith("+1") ? "USA/Canadá" :
                   numeroLimpio.StartsWith("+34") ? "España" :
                   numeroLimpio.StartsWith("+") ? "Internacional" : "Desconocido";
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}