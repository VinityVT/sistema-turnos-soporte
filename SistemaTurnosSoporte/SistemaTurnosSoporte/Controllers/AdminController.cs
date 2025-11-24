using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIGESTEC.API.DTOs;
using SIGESTEC.API.Entities;
using SistemaSoporte.Models;
using SistemaSoporte.Models.ViewModels;
using SistemaSoporte.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SistemaSoporte.Controllers
{
    [Authorize(Policy = "RequireAdmin")]
    public class AdminController : Controller
    {
        private readonly IApiService _apiService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IApiService apiService,
            UserManager<ApplicationUser> userManager,
            ILogger<AdminController> logger)
        {
            _apiService = apiService;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard(DateTime? fechaInicio, DateTime? fechaFin, string periodo = "30dias")
        {
            try
            {
                // Aplicar filtros de fecha por defecto si no se especifican
                if (!fechaInicio.HasValue || !fechaFin.HasValue)
                {
                    (fechaInicio, fechaFin) = ObtenerRangoPorPeriodo(periodo);
                }

                var tickets = await GetTicketsFiltrados(fechaInicio, fechaFin);
                var tecnicos = await GetTecnicosSafely();
                var equipos = await GetEquiposSafely();

                var model = new AdminDashboardViewModel
                {
                    TotalSolicitudes = tickets?.Count ?? 0,
                    TiempoPromedioAtencion = await CalcularTiempoPromedio(tickets),
                    SatisfaccionPromedio = 4.2,
                    TecnicosActivos = tecnicos?.Count ?? 0,
                    TecnicosDisponibles = tecnicos?.Count(t => !tickets?.Any(ticket =>
                        ticket.TecnicoNombre == t.NombreCompleto && ticket.Estado == "En progreso") ?? false) ?? 0,
                    SolicitudesPorTipo = tickets?.GroupBy(t => t.TipoProblema)
                        .ToDictionary(g => g.Key, g => g.Count()) ?? new Dictionary<string, int>(),
                    SolicitudesPorTecnico = tickets?.Where(t => !string.IsNullOrEmpty(t.TecnicoNombre))
                        .GroupBy(t => t.TecnicoNombre)
                        .ToDictionary(g => g.Key, g => g.Count()) ?? new Dictionary<string, int>(),
                    SolicitudesUrgentes = tickets?.Where(t => t.Prioridad == "Alta" &&
                        (t.Estado == "En espera" || t.Estado == "En progreso"))
                        .Take(5)
                        .Select(t => new SolicitudUrgenteViewModel
                        {
                            Id = t.Id,
                            Titulo = $"{t.TipoProblema} - {t.Titulo}",
                            UsuarioNombre = t.UsuarioNombre,
                            Departamento = "N/A"
                        })
                        .ToList() ?? new List<SolicitudUrgenteViewModel>(),
                    FechaInicio = fechaInicio,
                    FechaFin = fechaFin,
                    PeriodoSeleccionado = periodo
                };

                ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
                ViewBag.FechaFin = fechaFin?.ToString("yyyy-MM-dd");
                ViewBag.PeriodoSeleccionado = periodo;

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar dashboard con filtros");
                var emptyModel = new AdminDashboardViewModel
                {
                    TotalSolicitudes = 0,
                    TiempoPromedioAtencion = 0,
                    SatisfaccionPromedio = 0,
                    TecnicosActivos = 0,
                    TecnicosDisponibles = 0,
                    SolicitudesPorTipo = new Dictionary<string, int>(),
                    SolicitudesPorTecnico = new Dictionary<string, int>(),
                    SolicitudesUrgentes = new List<SolicitudUrgenteViewModel>(),
                    FechaInicio = DateTime.Now.AddDays(-30),
                    FechaFin = DateTime.Now,
                    PeriodoSeleccionado = "30dias"
                };
                return View(emptyModel);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AplicarFiltrosDashboard(EstadisticasFiltradasRequest filtros)
        {
            try
            {
                // Validar fechas
                if (filtros.FechaInicio.HasValue && filtros.FechaFin.HasValue &&
                    filtros.FechaInicio > filtros.FechaFin)
                {
                    TempData["ErrorMessage"] = "La fecha de inicio no puede ser mayor a la fecha de fin";
                    return RedirectToAction(nameof(Dashboard));
                }

                // Si se selecciona un período predefinido, ignorar las fechas específicas
                if (!string.IsNullOrEmpty(filtros.Periodo) && filtros.Periodo != "personalizado")
                {
                    (filtros.FechaInicio, filtros.FechaFin) = ObtenerRangoPorPeriodo(filtros.Periodo);
                }

                return RedirectToAction(nameof(Dashboard), new
                {
                    fechaInicio = filtros.FechaInicio?.ToString("yyyy-MM-dd"),
                    fechaFin = filtros.FechaFin?.ToString("yyyy-MM-dd"),
                    periodo = filtros.Periodo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al aplicar filtros al dashboard");
                TempData["ErrorMessage"] = "Error al aplicar los filtros";
                return RedirectToAction(nameof(Dashboard));
            }
        }

        [HttpGet]
        public async Task<JsonResult> ObtenerEstadisticasFiltradas(DateTime? fechaInicio, DateTime? fechaFin, string periodo = "30dias")
        {
            try
            {
                // Aplicar filtros de fecha
                if (!fechaInicio.HasValue || !fechaFin.HasValue)
                {
                    (fechaInicio, fechaFin) = ObtenerRangoPorPeriodo(periodo);
                }

                var tickets = await GetTicketsFiltrados(fechaInicio, fechaFin);

                var estadisticas = new
                {
                    solicitudesPorTipo = tickets?.GroupBy(t => t.TipoProblema)
                        .ToDictionary(g => g.Key, g => g.Count()) ?? new Dictionary<string, int>(),
                    solicitudesPorTecnico = tickets?.Where(t => !string.IsNullOrEmpty(t.TecnicoNombre))
                        .GroupBy(t => t.TecnicoNombre)
                        .ToDictionary(g => g.Key, g => g.Count()) ?? new Dictionary<string, int>(),
                    totalSolicitudes = tickets?.Count ?? 0,
                    tiempoPromedio = await CalcularTiempoPromedio(tickets),
                    rangoFechas = $"{fechaInicio:dd/MM/yyyy} - {fechaFin:dd/MM/yyyy}"
                };

                return Json(new { success = true, data = estadisticas });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas filtradas");
                return Json(new { success = false, message = "Error al cargar las estadísticas" });
            }
        }

        private async Task<List<TicketDTO>> GetTicketsFiltrados(DateTime? fechaInicio, DateTime? fechaFin)
        {
            try
            {
                var todosLosTickets = await _apiService.GetAsync<List<TicketDTO>>("api/Tickets");

                if (todosLosTickets == null || !todosLosTickets.Any())
                    return new List<TicketDTO>();

                // Aplicar filtro de fechas
                if (fechaInicio.HasValue && fechaFin.HasValue)
                {
                    return todosLosTickets
                        .Where(t => t.FechaCreacion >= fechaInicio.Value.Date &&
                                   t.FechaCreacion <= fechaFin.Value.Date.AddDays(1).AddSeconds(-1))
                        .ToList();
                }

                return todosLosTickets;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener tickets filtrados");
                return new List<TicketDTO>();
            }
        }

        private (DateTime?, DateTime?) ObtenerRangoPorPeriodo(string periodo)
        {
            var fechaFin = DateTime.Today;
            DateTime? fechaInicio = null;

            switch (periodo?.ToLower())
            {
                case "7dias":
                    fechaInicio = fechaFin.AddDays(-7);
                    break;
                case "30dias":
                    fechaInicio = fechaFin.AddDays(-30);
                    break;
                case "90dias":
                    fechaInicio = fechaFin.AddDays(-90);
                    break;
                case "este_mes":
                    fechaInicio = new DateTime(fechaFin.Year, fechaFin.Month, 1);
                    break;
                case "mes_anterior":
                    var primerDiaMesAnterior = new DateTime(fechaFin.Year, fechaFin.Month, 1).AddMonths(-1);
                    var ultimoDiaMesAnterior = new DateTime(fechaFin.Year, fechaFin.Month, 1).AddDays(-1);
                    fechaInicio = primerDiaMesAnterior;
                    fechaFin = ultimoDiaMesAnterior;
                    break;
                case "este_año":
                    fechaInicio = new DateTime(fechaFin.Year, 1, 1);
                    break;
                default:
                    fechaInicio = fechaFin.AddDays(-30); // Por defecto 30 días
                    break;
            }

            return (fechaInicio, fechaFin);
        }

        public async Task<IActionResult> Tickets()
        {
            try
            {
                var tickets = await _apiService.GetAsync<List<TicketDTO>>("api/Tickets");
                return View(tickets ?? new List<TicketDTO>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar tickets");
                return View(new List<TicketDTO>());
            }
        }

        public async Task<IActionResult> Tecnicos()
        {
            try
            {
                var tecnicos = await _apiService.GetAsync<List<UsuarioRespuestaDTO>>("api/Auth/tecnicos");
                return View(tecnicos ?? new List<UsuarioRespuestaDTO>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar técnicos");
                return View(new List<UsuarioRespuestaDTO>());
            }
        }

        [Authorize(Roles = "Admin,Tecnico")]
        public async Task<IActionResult> Equipos()
        {
            try
            {
                var equipos = await _apiService.GetAsync<List<EquipoDTO>>("api/Equipos");
                return View(equipos ?? new List<EquipoDTO>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar equipos");
                return View(new List<EquipoDTO>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> DetallesUsuario(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return Json(new { success = false, message = "ID de usuario no válido" });
                }

                // Usar el endpoint correcto de la API
                var usuario = await _apiService.GetAsync<UsuarioDetalleDTO>($"api/Auth/usuarios/{id}");

                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no encontrado" });
                }

                // Obtener la foto de perfil del usuario
                var fotoPerfil = await ObtenerFotoPerfil(id);

                // Agregar la foto de perfil al objeto de respuesta
                var usuarioConFoto = new
                {
                    usuario.Id,
                    usuario.NombreCompleto,
                    usuario.Email,
                    usuario.EsTecnico,
                    usuario.EmailConfirmado,
                    usuario.Bloqueado,
                    usuario.Roles,
                    usuario.FechaCreacion,
                    usuario.Telefono,
                    usuario.Especialidad,
                    usuario.UltimoAcceso,
                    FotoPerfil = fotoPerfil
                };

                return Json(new { success = true, data = usuarioConFoto });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalles del usuario {UserId}", id);
                return Json(new { success = false, message = "Error al cargar los detalles del usuario" });
            }
        }

        private async Task<string?> ObtenerFotoPerfil(string userId)
        {
            try
            {
                var webHostEnvironment = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
                var uploadsFolder = Path.Combine(webHostEnvironment.WebRootPath, "uploads", "perfiles");

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

        [Authorize(Roles = "Admin,Tecnico")]
        public async Task<IActionResult> DetalleEquipo(int id)
        {
            try
            {
                var equipo = await _apiService.GetAsync<EquipoDTO>($"api/Equipos/{id}");
                if (equipo == null)
                {
                    return RedirectToAction(nameof(Equipos));
                }

                var historial = await _apiService.GetAsync<List<HistorialIncidenteDTO>>($"api/Equipos/{id}/historial");
                ViewBag.HistorialIncidentes = historial ?? new List<HistorialIncidenteDTO>();

                return View(equipo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar detalle del equipo {EquipoId}", id);
                return RedirectToAction(nameof(Equipos));
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Tecnico")]
        public IActionResult CrearEquipo()
        {
            return View(new EquipoCreacionDTO
            {
                FechaAdquisicion = DateTime.Today,
                Estado = "Disponible",
                Componentes = new List<ComponenteCreacionDTO>()
            });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin,Tecnico")]
        public async Task<IActionResult> CrearEquipo([FromBody] EquipoCreacionDTO modelo)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var resultado = await _apiService.PostAsync<EquipoDTO>("api/Equipos", modelo);
                    if (resultado != null)
                    {
                        return Json(new { success = true, message = "Equipo creado exitosamente." });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Error al crear el equipo." });
                    }
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Error al crear el equipo: {ex.Message}" });
                }
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return Json(new { success = false, message = "Errores de validación:", errors });
        }

        [Authorize(Roles = "Admin,Tecnico")]
        public async Task<IActionResult> EditarEquipo(int id)
        {
            try
            {
                var equipo = await _apiService.GetAsync<EquipoDTO>($"api/Equipos/{id}");
                if (equipo == null)
                {
                    return RedirectToAction(nameof(Equipos));
                }

                var modelo = new EquipoCreacionDTO
                {
                    NumeroSerie = equipo.NumeroSerie,
                    Modelo = equipo.Modelo,
                    Marca = equipo.Marca,
                    Tipo = equipo.Tipo,
                    FechaAdquisicion = equipo.FechaAdquisicion,
                    Descripcion = equipo.Descripcion,
                    Estado = equipo.Estado,
                    Componentes = equipo.Componentes.Select(c => new ComponenteCreacionDTO
                    {
                        Id = c.Id,
                        Nombre = c.Nombre,
                        NumeroSerie = c.NumeroSerie,
                        Modelo = c.Modelo,
                        Marca = c.Marca,
                        Especificaciones = c.Especificaciones,
                        Estado = c.Estado
                    }).ToList()
                };

                ViewBag.EquipoId = id;
                return View("EditarEquipo", modelo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar equipo para editar {EquipoId}", id);
                return RedirectToAction(nameof(Equipos));
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Tecnico")]
        public async Task<IActionResult> EditarEquipo(int id, [FromBody] EquipoCreacionDTO modelo)
        {
            try
            {
                if (modelo == null)
                {
                    return Json(new { success = false, message = "Datos del equipo no válidos" });
                }

                // Validación básica
                if (string.IsNullOrEmpty(modelo.NumeroSerie) || string.IsNullOrEmpty(modelo.Modelo) ||
                    string.IsNullOrEmpty(modelo.Marca) || string.IsNullOrEmpty(modelo.Tipo))
                {
                    return Json(new { success = false, message = "Todos los campos requeridos deben estar completos" });
                }

                if (modelo.Componentes == null || !modelo.Componentes.Any())
                {
                    return Json(new { success = false, message = "El equipo debe tener al menos un componente" });
                }

                // Validar componentes
                foreach (var componente in modelo.Componentes)
                {
                    if (string.IsNullOrEmpty(componente.Nombre))
                    {
                        return Json(new { success = false, message = "Todos los componentes deben tener un nombre" });
                    }
                }

                var resultado = await _apiService.PutAsync<object>($"api/Equipos/{id}", modelo);

                // SIEMPRE devolver éxito si la API responde (aunque sea null)
                // porque a veces la API devuelve null en operaciones PUT exitosas
                return Json(new
                {
                    success = true,
                    message = "Equipo actualizado exitosamente",
                    redirectUrl = Url.Action("DetalleEquipo", "Admin", new { id = id })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar equipo {EquipoId}", id);
                return Json(new
                {
                    success = false,
                    message = $"Error al actualizar el equipo: {ex.Message}"
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EliminarEquipo(int id)
        {
            try
            {
                var resultado = await _apiService.DeleteAsync($"api/Equipos/{id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar equipo {EquipoId}", id);
            }
            return RedirectToAction(nameof(Equipos));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Tecnico")]
        public async Task<IActionResult> CambiarEstadoEquipo(int id, string estado)
        {
            try
            {
                var estadosValidos = new[] { "Disponible", "EnUso", "EnMantenimiento", "DadoDeBaja" };
                if (!estadosValidos.Contains(estado))
                {
                    return RedirectToAction(nameof(Equipos));
                }

                // Llamar a la API
                var resultado = await _apiService.PutAsync<object>($"api/Equipos/{id}/estado", new { Estado = estado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar estado del equipo {EquipoId} a {Estado}", id, estado);
            }

            return RedirectToAction(nameof(Equipos));
        }

        [Authorize(Roles = "Admin,Tecnico")]
        public async Task<IActionResult> AgregarIncidente(int id)
        {
            try
            {
                var equipo = await _apiService.GetAsync<EquipoDTO>($"api/Equipos/{id}");
                if (equipo == null)
                {
                    _logger.LogWarning("Equipo con ID {EquipoId} no encontrado", id);
                    TempData["ErrorMessage"] = $"Equipo con ID {id} no encontrado";
                    return RedirectToAction(nameof(Equipos));
                }

                ViewBag.Equipo = equipo;
                ViewBag.EquipoId = id; // Pasar el ID correctamente
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar equipo para agregar incidente {EquipoId}", id);
                TempData["ErrorMessage"] = "Error al cargar la información del equipo";
                return RedirectToAction(nameof(Equipos));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Tecnico")]
        public async Task<IActionResult> AgregarIncidente(int id, HistorialIncidenteCreacionDTO modelo)
        {
            _logger.LogInformation("Agregando incidente al equipo {EquipoId}", id);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("Modelo inválido al agregar incidente. Errores: {Errores}", string.Join(", ", errors));

                // Si es petición AJAX, retornar JSON
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Errores de validación", errors });
                }

                // Cargar datos para vista normal
                try
                {
                    var equipo = await _apiService.GetAsync<EquipoDTO>($"api/Equipos/{id}");
                    ViewBag.Equipo = equipo;
                    ViewBag.EquipoId = id;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al cargar equipo para mostrar formulario");
                }
                return View(modelo);
            }

            try
            {
                _logger.LogInformation("Validando existencia del equipo {EquipoId}", id);
                var equipo = await _apiService.GetAsync<EquipoDTO>($"api/Equipos/{id}");
                if (equipo == null)
                {
                    // Si es AJAX
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = "Equipo no encontrado" });
                    }

                    TempData["ErrorMessage"] = "Equipo no encontrado";
                    return RedirectToAction(nameof(Equipos));
                }

                _logger.LogInformation("Enviando solicitud POST a API para agregar incidente");
                var resultado = await _apiService.PostAsync<HistorialIncidenteDTO>($"api/Equipos/{id}/historial", modelo);

                if (resultado != null)
                {
                    _logger.LogInformation("Incidente agregado exitosamente al equipo {EquipoId}", id);

                    // Si es petición AJAX
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new
                        {
                            success = true,
                            message = "Incidente agregado correctamente",
                            incidenteId = resultado.Id
                        });
                    }

                    TempData["SuccessMessage"] = "Incidente agregado correctamente";
                    return RedirectToAction(nameof(DetalleEquipo), new { id = id });
                }
                else
                {
                    _logger.LogWarning("La API respondió con resultado nulo al agregar incidente");

                    // Si es AJAX
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = "Error al agregar el incidente" });
                    }

                    TempData["ErrorMessage"] = "Error al agregar el incidente";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar incidente para equipo {EquipoId}", id);

                // Si es AJAX
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = $"Error al registrar el incidente: {ex.Message}" });
                }

                TempData["ErrorMessage"] = $"Error al registrar el incidente: {ex.Message}";
            }

            // Si llegamos aquí, hubo un error - recargar datos de la vista
            try
            {
                var equipo = await _apiService.GetAsync<EquipoDTO>($"api/Equipos/{id}");
                ViewBag.Equipo = equipo;
                ViewBag.EquipoId = id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar equipo para mostrar formulario");
            }

            // Si es AJAX pero hubo error
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = false, message = "Error al procesar la solicitud" });
            }

            return View(modelo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Tecnico")]
        public async Task<IActionResult> ActualizarComponente(int componenteId, ComponenteCreacionDTO modelo)
        {
            try
            {
                var resultado = await _apiService.PutAsync<object>($"api/Equipos/componentes/{componenteId}", modelo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar componente {ComponenteId}", componenteId);
            }
            return RedirectToAction(nameof(Equipos));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Tecnico")]
        public async Task<IActionResult> CambiarEstadoComponente(int componenteId, string estado)
        {
            try
            {
                var resultado = await _apiService.PutAsync<object>($"api/Equipos/componentes/{componenteId}/estado", new { Estado = estado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar estado del componente {ComponenteId}", componenteId);
            }
            return RedirectToAction(nameof(Equipos));
        }

        [Authorize(Roles = "Admin,Tecnico")]
        public async Task<IActionResult> ReporteInventario()
        {
            try
            {
                var equipos = await _apiService.GetAsync<List<EquipoDTO>>("api/Equipos");
                var equiposDisponibles = await _apiService.GetAsync<List<EquipoDTO>>("api/Equipos/disponibles");
                var equiposEnUso = await _apiService.GetAsync<List<EquipoDTO>>("api/Equipos/por-estado/EnUso");
                var equiposMantenimiento = await _apiService.GetAsync<List<EquipoDTO>>("api/Equipos/por-estado/EnMantenimiento");

                var reporte = new EquipoReporteViewModel
                {
                    TotalEquipos = equipos?.Count ?? 0,
                    EquiposDisponibles = equiposDisponibles?.Count ?? 0,
                    EquiposEnUso = equiposEnUso?.Count ?? 0,
                    EquiposEnMantenimiento = equiposMantenimiento?.Count ?? 0,
                    EquiposDadosDeBaja = equipos?.Count(e => e.Estado == "DadoDeBaja") ?? 0,
                    UltimosEquiposRegistrados = equipos?.OrderByDescending(e => e.Id).Take(5).ToList() ?? new List<EquipoDTO>(),
                    UltimosIncidentes = new List<HistorialIncidenteDTO>()
                };

                return View(reporte);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de inventario");
                return View(new EquipoReporteViewModel());
            }
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GestionUsuarios()
        {
            try
            {
                var timestamp = DateTime.Now.Ticks;
                var usuarios = await _apiService.GetAsync<List<UsuarioInfoDTO>>($"api/Auth/usuarios?t={timestamp}");

                if (usuarios == null)
                {
                    usuarios = await _apiService.GetAsync<List<UsuarioInfoDTO>>("api/Auth/usuarios");
                    if (usuarios == null)
                    {
                        return View(new List<UsuarioInfoDTO>());
                    }
                }

                // LOS USUARIOS YA VIENEN ORDENADOS DESDE LA API POR FECHA DE CREACIÓN DESCENDENTE
                // Los más recientes estarán primero automáticamente

                return View(usuarios);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar usuarios");
                return View(new List<UsuarioInfoDTO>());
            }
        }

        private DateTime GetFechaCreacionFromId(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId) || userId.Length < 8)
                    return DateTime.MinValue;
                return DateTime.Now;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BloquearUsuario(string id, bool bloquear)
        {
            string logFolder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "UserManagementErrors");
            string logFileName = $"BlockUser_{DateTime.Now:yyyyMMdd_HHmmssfff}_{id}.txt";
            string fullLogPath = Path.Combine(logFolder, logFileName);

            try
            {
                Directory.CreateDirectory(logFolder);

                await System.IO.File.WriteAllTextAsync(fullLogPath,
                    $"BLOQUEAR USUARIO ATTEMPT:\n" +
                    $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n" +
                    $"User ID: {id}\n" +
                    $"Action: {(bloquear ? "Bloquear" : "Desbloquear")}\n" +
                    $"Admin User: {User.Identity?.Name}\n" +
                    $"Remote IP: {HttpContext.Connection.RemoteIpAddress}\n\n");

                var resultado = await _apiService.PostAsync<JsonElement>($"api/Auth/usuarios/{id}/bloquear", new { Bloquear = bloquear });

                string successMessage = bloquear ? "Usuario bloqueado correctamente" : "Usuario desbloqueado correctamente";
                string errorMessage = "Error al realizar la operación";

                if (resultado.ValueKind != JsonValueKind.Undefined && resultado.ValueKind != JsonValueKind.Null)
                {
                    if (resultado.TryGetProperty("success", out JsonElement successElement) &&
                        successElement.ValueKind == JsonValueKind.True)
                    {
                        if (resultado.TryGetProperty("message", out JsonElement messageElement))
                        {
                            successMessage = messageElement.GetString();
                        }

                        await System.IO.File.AppendAllTextAsync(fullLogPath,
                            $"SUCCESS:\n" +
                            $"Response: {resultado}\n");

                        return RedirectToAction(nameof(GestionUsuarios));
                    }
                    else if (resultado.TryGetProperty("message", out JsonElement messageElement))
                    {
                        await System.IO.File.AppendAllTextAsync(fullLogPath,
                            $"SUCCESS:\n" +
                            $"Response: {resultado}\n");

                        return RedirectToAction(nameof(GestionUsuarios));
                    }
                }

                if (resultado.TryGetProperty("message", out JsonElement errorMessageElement))
                {
                    errorMessage = errorMessageElement.GetString();
                }
                else if (resultado.TryGetProperty("error", out JsonElement errorElement))
                {
                    errorMessage = errorElement.GetString();
                }

                await System.IO.File.AppendAllTextAsync(fullLogPath,
                    $"ERROR:\n" +
                    $"Response: {resultado}\n" +
                    $"Message: {errorMessage}\n");
            }
            catch (Exception ex)
            {
                string errorDetails = $"EXCEPTION:\n" +
                                    $"Type: {ex.GetType().FullName}\n" +
                                    $"Message: {ex.Message}\n" +
                                    $"Stack Trace:\n{ex.StackTrace}\n";

                if (ex.InnerException != null)
                {
                    errorDetails += $"INNER EXCEPTION:\n" +
                                  $"Type: {ex.InnerException.GetType().FullName}\n" +
                                  $"Message: {ex.InnerException.Message}\n" +
                                  $"Stack Trace:\n{ex.InnerException.StackTrace}\n";
                }

                Directory.CreateDirectory(logFolder);
                await System.IO.File.AppendAllTextAsync(fullLogPath, errorDetails);

                _logger.LogError(ex, "Error al bloquear/desbloquear usuario {UserId}. See log: {LogPath}", id, fullLogPath);
            }

            return RedirectToAction(nameof(GestionUsuarios));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReiniciarPassword(string id, string nuevaPassword)
        {
            try
            {
                var resultado = await _apiService.PostAsync<object>($"api/Auth/usuarios/{id}/reiniciar-password", new { NuevaPassword = nuevaPassword });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reiniciar password para usuario {UserId}", id);
            }

            return RedirectToAction(nameof(GestionUsuarios));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(string nombreCompleto, string email, string password, string userType, string telefono, string departamento, string posicion)
        {
            try
            {
                if (string.IsNullOrEmpty(nombreCompleto) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                {
                    return RedirectToAction(nameof(GestionUsuarios));
                }

                if (password.Length < 8)
                {
                    return RedirectToAction(nameof(GestionUsuarios));
                }

                object userData;
                string endpoint;

                if (userType == "Usuario")
                {
                    userData = new
                    {
                        NombreCompleto = nombreCompleto,
                        Email = email,
                        Password = password,
                        Telefono = telefono,
                        Departamento = departamento,
                        Posicion = posicion
                    };
                    endpoint = "api/Auth/registro-empleado";
                }
                else
                {
                    userData = new
                    {
                        NombreCompleto = nombreCompleto,
                        Email = email,
                        Password = password,
                        EsTecnico = userType == "Tecnico" || userType == "Admin"
                    };
                    endpoint = "api/Auth/registro";
                }

                var result = await _apiService.PostAsync<object>(endpoint, userData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear usuario");
            }

            return RedirectToAction(nameof(GestionUsuarios));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(104857600)] // 100MB
        public async Task<IActionResult> ExportarReporteInventario(string format = "Excel")
        {
            try
            {
                _logger.LogInformation("Exportando reporte de inventario, Formato: {Format}", format);

                var request = new ReportRequestDTO
                {
                    ReportType = "Inventario",
                    Format = format
                };

                var fileBytes = await _apiService.PostAsyncBytes("api/Reports/export", request);

                if (fileBytes == null || fileBytes.Length == 0)
                {
                    _logger.LogWarning("No se generaron datos para el reporte de inventario");
                    return RedirectToAction(nameof(ReporteInventario));
                }

                string contentType, fileExtension;
                if (format?.ToLower() == "pdf")
                {
                    contentType = "application/pdf";
                    fileExtension = "pdf";
                }
                else
                {
                    contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    fileExtension = "xlsx";
                }

                var fileName = $"Reporte_Inventario_{DateTime.Now:yyyyMMddHHmmss}.{fileExtension}";

                _logger.LogInformation("Reporte de inventario exportado exitosamente, tamaño: {Size} bytes", fileBytes.Length);

                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar el reporte de inventario");
                TempData["ErrorMessage"] = $"Error al exportar el reporte: {ex.Message}";
                return RedirectToAction(nameof(ReporteInventario));
            }
        }

        // NUEVO: Método para exportar reportes
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportarReporte(string reportType, string format, DateTime? startDate = null, DateTime? endDate = null, string filterBy = null, string filterValue = null)
        {
            try
            {
                // Construir la URL para la API de reportes
                var url = $"api/Reports/export";

                // Crear el objeto de solicitud
                var request = new ReportRequestDTO
                {
                    ReportType = reportType,
                    Format = format,
                    StartDate = startDate,
                    EndDate = endDate,
                    FilterBy = filterBy,
                    FilterValue = filterValue
                };

                // Llamar al servicio de API
                var fileBytes = await _apiService.PostAsyncBytes(url, request);

                if (fileBytes == null || fileBytes.Length == 0)
                {
                    return RedirectToAction(nameof(Dashboard));
                }

                // Determinar el tipo de contenido y extensión del archivo
                string contentType, fileExtension;
                if (format?.ToLower() == "pdf")
                {
                    contentType = "application/pdf";
                    fileExtension = "pdf";
                }
                else
                {
                    contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    fileExtension = "xlsx";
                }

                // Generar nombre del archivo
                var fileName = $"Reporte_{reportType}_{DateTime.Now:yyyyMMddHHmmss}.{fileExtension}";

                // Retornar el archivo
                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar reporte {ReportType}", reportType);
                return RedirectToAction(nameof(Dashboard));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
        [RequestSizeLimit(104857600)]
        public async Task<IActionResult> ExportarReporteSimple(string reportType, string format)
        {
            try
            {
                _logger.LogInformation("Exportando reporte simple: {ReportType}, Formato: {Format}", reportType, format);

                if (string.IsNullOrEmpty(reportType))
                {
                    return RedirectToAction(nameof(ReporteInventario));
                }

                var request = new ReportRequestDTO
                {
                    ReportType = reportType,
                    Format = format
                };

                var fileBytes = await _apiService.PostAsyncBytes("api/Reports/export", request);

                if (fileBytes == null || fileBytes.Length == 0)
                {
                    return RedirectToAction(nameof(ReporteInventario));
                }

                string contentType, fileExtension;
                if (format?.ToLower() == "pdf")
                {
                    contentType = "application/pdf";
                    fileExtension = "pdf";
                }
                else
                {
                    contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    fileExtension = "xlsx";
                }

                var fileName = $"Reporte_{reportType}_{DateTime.Now:yyyyMMddHHmmss}.{fileExtension}";

                _logger.LogInformation("Reporte {ReportType} exportado exitosamente, tamaño: {Size} bytes", reportType, fileBytes.Length);

                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar el reporte simple {ReportType}", reportType);
                return RedirectToAction(nameof(ReporteInventario));
            }
        }

        [HttpGet]
        public async Task<JsonResult> ObtenerUsuarioParaEditar(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return Json(new { success = false, message = "ID de usuario no válido" });
                }

                var usuario = await _apiService.GetAsync<UsuarioEdicionDTO>($"api/Auth/usuarios/{id}/editar");

                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no encontrado" });
                }

                return Json(new { success = true, data = usuario });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuario para editar: {UserId}", id);
                return Json(new { success = false, message = "Error al cargar el usuario para editar" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> EditarUsuario([FromBody] UsuarioEdicionDTO modelo)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Json(new { success = false, message = "Errores de validación", errors });
                }

                var resultado = await _apiService.PutAsync<UsuarioRespuestaDTO>($"api/Auth/usuarios/{modelo.Id}", modelo);

                if (resultado != null)
                {
                    return Json(new { success = true, message = "Usuario actualizado correctamente" });
                }
                else
                {
                    return Json(new { success = false, message = "Error al actualizar el usuario" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar usuario: {UserId}", modelo.Id);
                return Json(new { success = false, message = $"Error al actualizar el usuario: {ex.Message}" });
            }
        }

        // === NUEVO MÉTODO REVISAR INCIDENTE - CORREGIDO ===
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Tecnico")]
        public async Task<IActionResult> RevisarIncidente(int id, [FromBody] RevisarIncidenteRequest modelo)
        {
            try
            {
                if (modelo == null)
                {
                    return Json(new { success = false, message = "Los datos de revisión no pueden ser nulos" });
                }

                if (string.IsNullOrWhiteSpace(modelo.Comentario))
                {
                    return Json(new { success = false, message = "El comentario de revisión es requerido" });
                }

                // Obtener el usuario actual
                var usuarioActual = User.Identity?.Name ?? "Sistema";

                // Llamar directamente a la API usando el ApiService
                var resultado = await _apiService.PostAsync<object>($"api/Equipos/historial/{id}/revisar", new
                {
                    comentario = modelo.Comentario
                });

                // Si la API responde exitosamente, devolver éxito
                if (resultado != null)
                {
                    // Obtener el conteo actualizado de incidentes no revisados
                    var incidenteActualizado = await _apiService.GetAsync<HistorialIncidenteDTO>($"api/Equipos/historial/{id}");
                    var incidentesNoRevisados = 0;

                    if (incidenteActualizado != null)
                    {
                        var equipoId = incidenteActualizado.EquipoId;
                        var historialCompleto = await _apiService.GetAsync<List<HistorialIncidenteDTO>>($"api/Equipos/{equipoId}/historial");
                        incidentesNoRevisados = historialCompleto?.Count(i => !i.Revisado) ?? 0;
                    }

                    return Json(new
                    {
                        success = true,
                        message = "Incidente marcado como revisado correctamente",
                        fechaRevision = DateTime.Now,
                        revisadoPor = usuarioActual,
                        incidentesNoRevisados = incidentesNoRevisados
                    });
                }
                else
                {
                    return Json(new { success = false, message = "Error al revisar el incidente en la API" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en AdminController al revisar incidente {IncidenteId}", id);
                return Json(new
                {
                    success = false,
                    message = "Error interno del servidor al procesar la solicitud"
                });
            }
        }

        [Authorize(Roles = "Admin,Tecnico")]
        public async Task<IActionResult> Inventario()
        {
            try
            {
                var articulos = await _apiService.GetAsync<List<InventarioDTO>>("api/Inventario");
                return View(articulos ?? new List<InventarioDTO>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar inventario");
                return View(new List<InventarioDTO>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Tecnico")]
        public async Task<IActionResult> CrearArticuloInventario(InventarioCreacionDTO modelo)
        {
            try
            {
                var resultado = await _apiService.PostAsync<InventarioDTO>("api/Inventario", modelo);

                if (resultado != null)
                {
                    return Json(new { success = true, message = "Artículo creado correctamente" });
                }
                else
                {
                    return Json(new { success = false, message = "Error al crear el artículo" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear artículo de inventario");
                return Json(new { success = false, message = $"Error al crear el artículo: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Tecnico")]
        public async Task<IActionResult> ActualizarArticuloInventario(int id, InventarioCreacionDTO modelo)
        {
            try
            {
                var resultado = await _apiService.PutAsync<object>($"api/Inventario/{id}", modelo);

                if (resultado != null)
                {
                    return Json(new { success = true, message = "Artículo actualizado correctamente" });
                }
                else
                {
                    return Json(new { success = false, message = "Error al actualizar el artículo" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar artículo {ArticuloId}", id);
                return Json(new { success = false, message = $"Error al actualizar el artículo: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<JsonResult> ObtenerTecnicosDisponibles()
        {
            try
            {
                var tecnicos = await _apiService.GetAsync<List<UsuarioRespuestaDTO>>("api/Auth/tecnicos");

                // Si no hay endpoint específico, puedes obtener todos los usuarios y filtrar
                if (tecnicos == null || !tecnicos.Any())
                {
                    var todosUsuarios = await _apiService.GetAsync<List<UsuarioInfoDTO>>("api/Auth/usuarios");
                    tecnicos = todosUsuarios?
                        .Where(u => u.Roles.Contains("Tecnico") || u.EsTecnico)
                        .Select(u => new UsuarioRespuestaDTO
                        {
                            Id = u.Id,
                            NombreCompleto = u.NombreCompleto,
                            Email = u.Email,
                            EsTecnico = u.EsTecnico,
                            Token = string.Empty
                        })
                        .ToList() ?? new List<UsuarioRespuestaDTO>();
                }

                return Json(new { success = true, data = tecnicos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener técnicos disponibles");
                return Json(new { success = false, message = "Error al cargar los técnicos" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> AsignarTicket([FromBody] AsignarTicketRequest modelo)
        {
            try
            {
                if (modelo == null || modelo.TicketId <= 0 || string.IsNullOrEmpty(modelo.TecnicoId))
                {
                    return Json(new { success = false, message = "Datos de asignación inválidos" });
                }

                var resultado = await _apiService.PutAsync<object>($"api/Tickets/{modelo.TicketId}/asignar", new
                {
                    TecnicoId = modelo.TecnicoId
                });

                // Si la API devuelve NoContent (204), consideramos éxito
                return Json(new { success = true, message = "Ticket asignado correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al asignar ticket {TicketId} a técnico {TecnicoId}", modelo?.TicketId, modelo?.TecnicoId);
                return Json(new { success = false, message = $"Error al asignar el ticket: {ex.Message}" });
            }
        }

        public class AsignarTicketRequest
        {
            public int TicketId { get; set; }
            public string TecnicoId { get; set; } = string.Empty;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Tecnico")]
        public async Task<IActionResult> AjustarStockInventario(AjusteStockDTO modelo)
        {
            try
            {
                var resultado = await _apiService.PostAsync<object>("api/Inventario/ajustar-stock", modelo);

                if (resultado != null)
                {
                    return Json(new { success = true, message = "Stock actualizado correctamente" });
                }
                else
                {
                    return Json(new { success = false, message = "Error al actualizar el stock" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al ajustar stock del artículo {ArticuloId}", modelo.ArticuloId);
                return Json(new { success = false, message = $"Error al actualizar el stock: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EliminarArticuloInventario(int id)
        {
            try
            {
                var resultado = await _apiService.DeleteAsync($"api/Inventario/{id}");

                if (resultado)
                {
                    return Json(new { success = true, message = "Artículo eliminado correctamente" });
                }
                else
                {
                    return Json(new { success = false, message = "Error al eliminar el artículo" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar artículo {ArticuloId}", id);
                return Json(new { success = false, message = $"Error al eliminar el artículo: {ex.Message}" });
            }
        }

        // === MÉTODOS AUXILIARES PRIVADOS ===

        private async Task<List<TicketDTO>> GetTicketsSafely()
        {
            try
            {
                return await _apiService.GetAsync<List<TicketDTO>>("api/Tickets");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener tickets");
                return new List<TicketDTO>();
            }
        }

        private async Task<List<UsuarioRespuestaDTO>> GetTecnicosSafely()
        {
            try
            {
                return await _apiService.GetAsync<List<UsuarioRespuestaDTO>>("api/Auth/tecnicos");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener técnicos");
                return new List<UsuarioRespuestaDTO>();
            }
        }

        private async Task<List<EquipoDTO>> GetEquiposSafely()
        {
            try
            {
                return await _apiService.GetAsync<List<EquipoDTO>>("api/Equipos");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener equipos");
                return new List<EquipoDTO>();
            }
        }

        private async Task<double> CalcularTiempoPromedio(List<TicketDTO> tickets)
        {
            if (tickets == null || !tickets.Any())
                return 0;

            var ticketsResueltos = tickets
                .Where(t => t.Estado == "Terminado" && t.FechaResolucion.HasValue)
                .ToList();

            if (ticketsResueltos.Any())
            {
                return ticketsResueltos
                    .Average(t => (t.FechaResolucion.Value - t.FechaCreacion).TotalHours);
            }
            return 0;
        }
    }

    // === CLASES DTO - FUERA DE AdminController ===
    public class RevisarIncidenteRequest
    {
        public string Comentario { get; set; }
    }

    public class EquipoReporteViewModel
    {
        public int TotalEquipos { get; set; }
        public int EquiposDisponibles { get; set; }
        public int EquiposEnUso { get; set; }
        public int EquiposEnMantenimiento { get; set; }
        public int EquiposDadosDeBaja { get; set; }
        public List<EquipoDTO> UltimosEquiposRegistrados { get; set; } = new List<EquipoDTO>();
        public List<HistorialIncidenteDTO> UltimosIncidentes { get; set; } = new List<HistorialIncidenteDTO>();
    }

    // === CLASES PARA FILTROS DE DASHBOARD ===
    public class EstadisticasFiltradasRequest
    {
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string Periodo { get; set; }
    }
}