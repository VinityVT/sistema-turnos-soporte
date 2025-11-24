using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SIGESTEC.API.DTOs;
using SistemaSoporte.Models.ViewModels;
using SistemaSoporte.Services;

namespace SistemaSoporte.Controllers
{
    public class CuentaController : Controller
    {
        private readonly IApiService _apiService;
        private readonly ILogger<CuentaController> _logger;
        private const string LogFolder = "Logs/AuthErrors";

        public CuentaController(
            IApiService apiService,
            ILogger<CuentaController> logger)
        {
            _apiService = apiService;
            _logger = logger;

            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), LogFolder));
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (User.Identity?.IsAuthenticated ?? false)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                try
                {
                    var loginResult = await _apiService.PostAsync<UsuarioRespuestaDTO>("api/Auth/login", new
                    {
                        email = model.Email,
                        password = model.Password
                    });

                    if (loginResult == null || string.IsNullOrEmpty(loginResult.Token))
                    {
                        ModelState.AddModelError(string.Empty, "Credenciales inválidas");
                        return View(model);
                    }

                    var handler = new JwtSecurityTokenHandler();
                    var jwtToken = handler.ReadJwtToken(loginResult.Token);

                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, loginResult.Id),
                        new Claim(ClaimTypes.Name, loginResult.NombreCompleto),
                        new Claim(ClaimTypes.Email, loginResult.Email),
                        new Claim("EsTecnico", loginResult.EsTecnico.ToString())
                    };

                    var roleClaims = jwtToken.Claims.Where(c => c.Type == ClaimTypes.Role).ToList();
                    if (roleClaims.Any())
                    {
                        claims.AddRange(roleClaims);
                    }
                    else
                    {
                        claims.Add(new Claim(ClaimTypes.Role, loginResult.EsTecnico ? "Tecnico" : "Usuario"));
                    }

                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = model.RememberMe,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12),
                        IssuedUtc = DateTimeOffset.UtcNow
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
                        authProperties);

                    Response.Cookies.Append("AuthToken", loginResult.Token, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTime.Now.AddHours(12),
                        Path = "/"
                    });

                    // Redirigir según el rol
                    if (claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "Admin"))
                    {
                        return RedirectToAction("Dashboard", "Admin");
                    }
                    else if (claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "Tecnico"))
                    {
                        return RedirectToAction("Index", "Tecnico");
                    }
                    else
                    {
                        return RedirectToLocal(returnUrl);
                    }
                }
                catch (ApplicationException ex)
                {
                    // Manejar errores específicos de la API
                    string errorMessage = ex.Message.ToLower();

                    if (errorMessage.Contains("bloqueado") || errorMessage.Contains("blocked"))
                    {
                        ModelState.AddModelError(string.Empty, "Su cuenta está bloqueada. Contacte al administrador.");
                    }
                    else if (errorMessage.Contains("credenciales") || errorMessage.Contains("invalid"))
                    {
                        ModelState.AddModelError(string.Empty, "Correo electrónico o contraseña incorrectos.");
                    }
                    else if (errorMessage.Contains("no encontrado") || errorMessage.Contains("not found"))
                    {
                        ModelState.AddModelError(string.Empty, "El correo electrónico no está registrado.");
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "Error al iniciar sesión: " + ex.Message);
                    }

                    _logger.LogWarning("Login failed for {Email}: {Error}", model.Email, ex.Message);
                }
                catch (UnauthorizedAccessException)
                {
                    ModelState.AddModelError(string.Empty, "Acceso no autorizado. Contacte al administrador.");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, "Error de conexión. Verifique su conexión a internet e intente nuevamente.");
                    _logger.LogError(ex, "Unexpected error during login for {Email}", model.Email);
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Response.Cookies.Delete("AuthToken");
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccesoDenegado()
        {
            return View();
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }
    }
}