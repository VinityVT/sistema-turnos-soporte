using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaSoporte.Services;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SistemaSoporte.Controllers
{
    [Authorize(Roles = "Tecnico,Admin")]
    public class TecnicoController : Controller
    {
        private readonly IApiService _apiService;

        public TecnicoController(IApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task<IActionResult> Index()
        {
            // Limpiar mensajes temporales después de mostrarlos
            var successMessage = TempData["SuccessMessage"] as string;
            var errorMessage = TempData["ErrorMessage"] as string;

            if (!string.IsNullOrEmpty(successMessage))
            {
                ViewBag.SuccessMessage = successMessage;
                TempData.Remove("SuccessMessage"); // Limpiar después de usar
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                ViewBag.ErrorMessage = errorMessage;
                TempData.Remove("ErrorMessage"); // Limpiar después de usar
            }

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var tickets = await _apiService.GetAsync<List<SIGESTEC.API.DTOs.TicketDTO>>("api/Tickets");

                // Tickets asignados al técnico actual
                var misTickets = tickets?
                    .Where(t => t.TecnicoId == userId && t.Estado != "Terminado" && t.Estado != "Cancelado")
                    .OrderByDescending(t => t.Prioridad == "Alta")
                    .ThenBy(t => t.FechaCreacion)
                    .ToList() ?? new List<SIGESTEC.API.DTOs.TicketDTO>();

                // Tickets pendientes de asignación
                var ticketsPendientes = tickets?
                    .Where(t => string.IsNullOrEmpty(t.TecnicoId) && t.Estado == "En espera")
                    .OrderByDescending(t => t.Prioridad == "Alta")
                    .ThenBy(t => t.FechaCreacion)
                    .ToList() ?? new List<SIGESTEC.API.DTOs.TicketDTO>();

                // Tickets resueltos por el técnico actual
                var ticketsResueltos = tickets?
                    .Where(t => t.TecnicoId == userId && t.Estado == "Terminado")
                    .OrderByDescending(t => t.FechaResolucion)
                    .ToList() ?? new List<SIGESTEC.API.DTOs.TicketDTO>();

                // ✅ NUEVO: Tickets cancelados por el técnico actual
                var ticketsCancelados = tickets?
                    .Where(t => t.TecnicoId == userId && t.Estado == "Cancelado")
                    .OrderByDescending(t => t.FechaResolucion)
                    .ToList() ?? new List<SIGESTEC.API.DTOs.TicketDTO>();

                ViewBag.SolicitudesAsignadas = misTickets;
                ViewBag.SolicitudesPendientes = ticketsPendientes;
                ViewBag.TicketsResueltos = ticketsResueltos;
                ViewBag.TicketsCancelados = ticketsCancelados; // ✅ AGREGADO

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Error al cargar los tickets: " + ex.Message;
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Asignar(int id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                await _apiService.PutAsync<dynamic>($"api/Tickets/{id}", new
                {
                    estado = "En progreso",
                    tecnicoId = userId
                });
                TempData.Remove("SuccessMessage");
                TempData.Remove("ErrorMessage");

                TempData["SuccessMessage"] = "Ticket asignado correctamente";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData.Remove("SuccessMessage");
                TempData.Remove("ErrorMessage");

                TempData["ErrorMessage"] = "Error al asignar el ticket: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolver(int id, string comentario)
        {
            // Verificar si es una petición AJAX
            bool isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                               Request.Headers["Accept"].ToString().Contains("application/json");

            try
            {
                await _apiService.PutAsync<dynamic>($"api/Tickets/{id}/resolver", new
                {
                    comentario = comentario
                });

                // Si es AJAX, retornar JSON
                if (isAjaxRequest)
                {
                    return Json(new
                    {
                        success = true,
                        message = "Ticket marcado como resuelto"
                    });
                }

                TempData["SuccessMessage"] = "Ticket marcado como resuelto";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                // Si es AJAX, retornar error en JSON
                if (isAjaxRequest)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Error al resolver el ticket",
                        errors = new { General = ex.Message }
                    });
                }

                TempData["ErrorMessage"] = "Error al resolver el ticket: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}