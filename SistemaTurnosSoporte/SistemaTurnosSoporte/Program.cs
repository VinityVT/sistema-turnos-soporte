using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SistemaSoporte.Data;
using SistemaSoporte.Models;
using SistemaSoporte.Services;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddMemoryCache();

builder.Services.AddControllersWithViews();

// Database Configuration
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
    sqlOptions => sqlOptions.EnableRetryOnFailure()));

// Identity Configuration
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Authentication Configuration
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/Cuenta/Login";
    options.AccessDeniedPath = "/Cuenta/AccesoDenegado";
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.SlidingExpiration = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;

    options.Cookie.Name = "AuthCookie";
    options.Cookie.Path = "/";
    options.Cookie.Domain = null;

    // Events for better control
    options.Events = new CookieAuthenticationEvents
    {
        OnValidatePrincipal = async context =>
        {
            // Custom validation logic if needed
            await Task.CompletedTask;
        }
    };
});

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireEmpleado", policy => policy.RequireRole("Usuario", "Tecnico", "Admin"));
    options.AddPolicy("RequireTecnico", policy => policy.RequireRole("Tecnico", "Admin"));
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireTecnicoOrAdmin", policy => policy.RequireRole("Tecnico", "Admin"));
});

// ? CONFIGURACIÓN ACTUALIZADA DE HTTP CLIENT PARA LA API
builder.Services.AddHttpClient("SigestecApi", client =>
{
    var baseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7181";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "SistemaSoporte-Web");
    client.Timeout = TimeSpan.FromSeconds(60);
});

// ? AGREGAR CLIENTE HTTP POR DEFECTO
builder.Services.AddHttpClient("Default", client =>
{
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "SistemaSoporte-Web");
    client.Timeout = TimeSpan.FromSeconds(60);
});

// ? SERVICIOS DE APLICACIÓN ACTUALIZADOS
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IApiService, ApiService>();

// Session Configuration
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Response Caching
builder.Services.AddResponseCaching();

// Route Options
builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true;
});

// Logging
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddConsole();
    loggingBuilder.AddDebug();
    loggingBuilder.AddConfiguration(builder.Configuration.GetSection("Logging"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

// ? CONFIGURACIÓN MEJORADA PARA SERVIR ARCHIVOS ESTÁTICOS
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Cache static files for 1 hour
        ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=3600");

        // Configuración adicional para imágenes
        if (ctx.File.Name.Contains("uploads") || ctx.File.Name.Contains("perfiles"))
        {
            ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=86400"); // 1 día para fotos de perfil
        }
    }
});

// ? CREAR DIRECTORIOS DE UPLOADS SI NO EXISTEN
var uploadsPath = Path.Combine(app.Environment.WebRootPath, "uploads", "perfiles");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Directorio de uploads/perfiles creado: {UploadsPath}", uploadsPath);
}

// ? CREAR OTROS DIRECTORIOS POTENCIALES DE UPLOADS
var uploadDirs = new[] { "uploads", "uploads/tickets", "uploads/documentos", "uploads/temp" };
foreach (var dir in uploadDirs)
{
    var fullPath = Path.Combine(app.Environment.WebRootPath, dir);
    if (!Directory.Exists(fullPath))
    {
        Directory.CreateDirectory(fullPath);
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Directorio creado: {Directory}", fullPath);
    }
}

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.UseResponseCaching();

// Global error handling middleware
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception occurred");

        if (!context.Response.HasStarted)
        {
            context.Response.Redirect("/Home/Error");
        }
    }
});

// ? MIDDLEWARE PERSONALIZADO PARA VERIFICAR SERVICIO DE FOTOS
app.Use(async (context, next) =>
{
    // Log para verificar que el servicio de fotos está funcionando
    if (context.Request.Path.StartsWithSegments("/uploads/perfiles"))
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Solicitud de foto de perfil: {Path}", context.Request.Path);
    }

    await next();
});

// Map controller routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Additional routes for areas if needed
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// Database initialization and seeding
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // Apply pending migrations
        await context.Database.MigrateAsync();

        // Create roles if they don't exist
        foreach (var role in new[] { "Admin", "Tecnico", "Usuario" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("Rol creado: {Role}", role);
            }
        }

        // Create default admin user
        var adminEmail = "admin@sigestec.com";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                NombreCompleto = "Administrador del Sistema",
                Departamento = "Tecnologías de la Información",
                TipoUsuario = "Admin",
                EmailConfirmed = true,
                PhoneNumber = "+1234567890",
                FechaRegistro = DateTime.Now
            };

            var result = await userManager.CreateAsync(admin, "Admin123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
                await userManager.AddToRoleAsync(admin, "Tecnico");

                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("Usuario administrador creado exitosamente");
            }
            else
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogError("Error al crear usuario administrador: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        // Ensure all existing users have roles
        var users = await userManager.Users.ToListAsync();
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            if (roles.Count == 0)
            {
                var defaultRole = user.TipoUsuario switch
                {
                    "Admin" => "Admin",
                    "Tecnico" => "Tecnico",
                    _ => "Usuario"
                };

                await userManager.AddToRoleAsync(user, defaultRole);

                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("Rol {Role} asignado al usuario {UserName}", defaultRole, user.UserName);
            }
        }

        // ? VERIFICACIÓN ADICIONAL DEL SISTEMA DE FOTOS
        var webRootPath = app.Environment.WebRootPath;
        var uploadsDir = Path.Combine(webRootPath, "uploads", "perfiles");
        var loggerFinal = services.GetRequiredService<ILogger<Program>>();

        if (Directory.Exists(uploadsDir))
        {
            var photoFiles = Directory.GetFiles(uploadsDir).Length;
            loggerFinal.LogInformation("Sistema de fotos listo. Archivos en uploads/perfiles: {Count}", photoFiles);
        }
        else
        {
            loggerFinal.LogWarning("Directorio de fotos no encontrado: {UploadsDir}", uploadsDir);
        }

        // Log startup completion
        loggerFinal.LogInformation("Aplicación inicializada correctamente");
        loggerFinal.LogInformation("Entorno: {Environment}", app.Environment.EnvironmentName);
        loggerFinal.LogInformation("URLs: https://localhost:7094, http://localhost:5268");
        loggerFinal.LogInformation("Directorio WebRoot: {WebRootPath}", webRootPath);

    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error durante la inicialización de la aplicación");
    }
}

// Final startup message
app.Lifetime.ApplicationStarted.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("SistemaSoporte Web MVC iniciado correctamente");
    logger.LogInformation("Directorio de contenido: {ContentRoot}", app.Environment.ContentRootPath);
    logger.LogInformation("Directorio web: {WebRoot}", app.Environment.WebRootPath);
    logger.LogInformation("Servicio de fotos de perfil configurado correctamente");
});

app.Run();