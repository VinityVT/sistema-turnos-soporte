using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaSoporte.Services;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using SistemaSoporte.Models;

namespace SistemaSoporte.Controllers
{
    [Authorize]
    public class TicketsController : Controller
    {
        private readonly IApiService _apiService;

        public TicketsController(IApiService apiService)
        {
            _apiService = apiService;
        }

        [HttpGet]
        public async Task<IActionResult> MisSolicitudes()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var tickets = await _apiService.GetAsync<List<SIGESTEC.API.DTOs.TicketDTO>>($"api/Tickets/mis-tickets");
                return View(tickets ?? new List<SIGESTEC.API.DTOs.TicketDTO>());
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error al cargar las solicitudes. Por favor, intente nuevamente.";
                return View(new List<SIGESTEC.API.DTOs.TicketDTO>());
            }
        }

        [HttpGet]
        public IActionResult Crear()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(SIGESTEC.API.DTOs.TicketCreacionDTO model)
        {
            // Verificar si es una petición AJAX
            bool isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                               Request.Headers["Accept"].ToString().Contains("application/json");

            if (ModelState.IsValid)
            {
                try
                {
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    await _apiService.PostAsync<dynamic>("api/Tickets", new
                    {
                        titulo = model.Titulo,
                        descripcion = model.Descripcion,
                        tipoProblema = model.TipoProblema,
                        prioridad = model.Prioridad
                    });

                    // Si es AJAX, retornar JSON
                    if (isAjaxRequest)
                    {
                        return Json(new
                        {
                            success = true,
                            message = "Ticket creado exitosamente.",
                            redirectUrl = Url.Action("MisSolicitudes", "Tickets")
                        });
                    }

                    // Si no es AJAX, comportamiento tradicional
                    TempData["SuccessMessage"] = "Ticket creado exitosamente.";
                    return RedirectToAction(nameof(MisSolicitudes));
                }
                catch (Exception ex)
                {
                    // Si es AJAX, retornar error en JSON
                    if (isAjaxRequest)
                    {
                        return Json(new
                        {
                            success = false,
                            message = "Error al crear la solicitud. Por favor, intente nuevamente.",
                            errors = new { General = ex.Message }
                        });
                    }

                    ModelState.AddModelError(string.Empty, "Error al crear la solicitud. Por favor, intente nuevamente.");
                }
            }

            // Si hay errores de validación y es AJAX
            if (isAjaxRequest)
            {
                var errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );
                return Json(new
                {
                    success = false,
                    message = "Por favor, corrija los errores de validación.",
                    errors = errors
                });
            }

            // Si no es AJAX, retornar la vista con errores
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Detalle(int id)
        {
            try
            {
                var ticket = await _apiService.GetAsync<SIGESTEC.API.DTOs.TicketDTO>($"api/Tickets/{id}");
                if (ticket == null)
                {
                    return NotFound();
                }

                // Cargar información del usuario para el modal
                if (User.IsInRole("Admin") || User.IsInRole("Tecnico"))
                {
                    var historialUsuario = await _apiService.GetAsync<List<HistorialUsuarioViewModel>>($"api/Tickets/{id}/historial-usuario");
                    ViewBag.HistorialUsuario = historialUsuario ?? new List<HistorialUsuarioViewModel>();
                    ViewBag.UsuarioSolicitante = ticket.UsuarioNombre;
                }
                else
                {
                    ViewBag.HistorialUsuario = new List<HistorialUsuarioViewModel>();
                    ViewBag.UsuarioSolicitante = "";
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                return View(ticket);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error al cargar el detalle del ticket.";
                return RedirectToAction(nameof(MisSolicitudes));
            }
        }

        [HttpGet]
        public async Task<IActionResult> CancelarConfirmacion(int id)
        {
            try
            {
                var ticket = await _apiService.GetAsync<SIGESTEC.API.DTOs.TicketDTO>($"api/Tickets/{id}");
                if (ticket == null)
                {
                    return NotFound();
                }

                return View(ticket);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error al cargar el ticket.";
                return RedirectToAction(nameof(MisSolicitudes));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar(int id, string comentario)
        {
            // Verificar si es una petición AJAX
            bool isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                               Request.Headers["Accept"].ToString().Contains("application/json");

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var result = await _apiService.PutAsync<dynamic>($"api/Tickets/{id}/cancelar", new
                {
                    comentario = comentario ?? "Cancelado por el usuario"
                });

                // Si es AJAX, retornar JSON
                if (isAjaxRequest)
                {
                    return Json(new
                    {
                        success = true,
                        message = "Ticket cancelado exitosamente.",
                        redirectUrl = Url.Action("MisSolicitudes", "Tickets")
                    });
                }

                TempData["SuccessMessage"] = "Ticket cancelado exitosamente.";
                return RedirectToAction(nameof(MisSolicitudes));
            }
            catch (Exception ex)
            {
                // Si es AJAX, retornar error en JSON
                if (isAjaxRequest)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Error al cancelar el ticket",
                        errors = new { General = ex.Message }
                    });
                }

                TempData["ErrorMessage"] = "Error al cancelar el ticket: " + ex.Message;
                return RedirectToAction(nameof(MisSolicitudes));
            }
        }

        // Nueva acción para cargar historial del usuario via AJAX
        [HttpGet]
        [Authorize(Roles = "Admin,Tecnico")]
        public async Task<JsonResult> ObtenerHistorialUsuario(int id)
        {
            try
            {
                var historialUsuario = await _apiService.GetAsync<List<HistorialUsuarioViewModel>>($"api/Tickets/{id}/historial-usuario");
                var ticketActual = await _apiService.GetAsync<SIGESTEC.API.DTOs.TicketDTO>($"api/Tickets/{id}");

                return Json(new
                {
                    success = true,
                    data = historialUsuario ?? new List<HistorialUsuarioViewModel>(),
                    usuarioSolicitante = ticketActual?.UsuarioNombre ?? "Usuario no encontrado"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al cargar el historial del usuario" });
            }
        }
    }
}