using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SistemaSoporte.Services
{
    public class ApiService : IApiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ApiService> _logger;
        private readonly IConfiguration _configuration;

        public ApiService(
            IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ApiService> logger,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _configuration = configuration;
        }

        private async Task<HttpClient> GetClientAsync()
        {
            var client = _httpClientFactory.CreateClient("SigestecApi");
            var baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7181";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(_configuration.GetValue<int>("ApiSettings:Timeout", 30));

            var token = _httpContextAccessor.HttpContext?.Request.Cookies["AuthToken"];
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return client;
        }

        public async Task<T> GetAsync<T>(string endpoint)
        {
            try
            {
                var client = await GetClientAsync();
                _logger.LogInformation("Haciendo GET request a: {Endpoint}", endpoint);

                var response = await client.GetAsync(endpoint);

                _logger.LogInformation("Respuesta recibida: {StatusCode}", response.StatusCode);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Acceso no autorizado al endpoint: {Endpoint}", endpoint);
                    if (_httpContextAccessor.HttpContext != null)
                    {
                        _httpContextAccessor.HttpContext.Response.Redirect("/Cuenta/Login");
                    }
                    return default(T);
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GET request failed: {StatusCode} - {Endpoint}", response.StatusCode, endpoint);
                    return default(T);
                }

                if (response.StatusCode == HttpStatusCode.NoContent ||
                    response.Content.Headers.ContentLength == 0)
                {
                    _logger.LogInformation("No content recibido para {Endpoint}", endpoint);
                    return default(T);
                }

                var result = await response.Content.ReadFromJsonAsync<T>();
                _logger.LogInformation("GET request successful para {Endpoint}, datos recibidos: {Count}",
                    endpoint, result != null ? "con datos" : "sin datos");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GET request to {Endpoint}", endpoint);
                return default(T);
            }
        }

        public async Task<byte[]> GetBytesAsync(string endpoint)
        {
            try
            {
                var client = await GetClientAsync();
                _logger.LogInformation("Haciendo GET request para bytes a: {Endpoint}", endpoint);

                var response = await client.GetAsync(endpoint);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Acceso no autorizado al endpoint: {Endpoint}", endpoint);
                    throw new UnauthorizedAccessException("Acceso no autorizado");
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GET bytes request failed: {StatusCode} - {Endpoint}", response.StatusCode, endpoint);
                    return Array.Empty<byte>();
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                _logger.LogInformation("GET bytes request successful para {Endpoint}, tamaño: {Size} bytes",
                    endpoint, bytes.Length);

                return bytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GET bytes request to {Endpoint}", endpoint);
                return Array.Empty<byte>();
            }
        }

        public async Task<byte[]> DownloadFileAsync(string endpoint)
        {
            try
            {
                var client = await GetClientAsync();
                _logger.LogInformation("Downloading file from: {Endpoint}", endpoint);

                var response = await client.GetAsync(endpoint);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException("Acceso no autorizado");
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Download failed: {StatusCode} - {Endpoint}", response.StatusCode, endpoint);
                    return Array.Empty<byte>();
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                _logger.LogInformation("Download successful: {Endpoint}, size: {Size} bytes", endpoint, bytes.Length);

                return bytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file from {Endpoint}", endpoint);
                throw;
            }
        }

        public async Task<T> PostAsync<T>(string endpoint, object data)
        {
            try
            {
                var client = await GetClientAsync();
                _logger.LogInformation("Making POST request to {Endpoint}", endpoint);

                var response = await client.PostAsJsonAsync(endpoint, data);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized access to {Endpoint}", endpoint);
                    throw new UnauthorizedAccessException("Acceso no autorizado");
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("POST request failed: {StatusCode} - {Endpoint} - {Error}",
                        response.StatusCode, endpoint, errorContent);
                    throw new ApplicationException($"Error en la API ({response.StatusCode}): {errorContent}");
                }

                if (response.StatusCode == HttpStatusCode.NoContent ||
                    response.Content.Headers.ContentLength == 0)
                {
                    return default(T);
                }

                var result = await response.Content.ReadFromJsonAsync<T>();
                _logger.LogInformation("POST request successful to {Endpoint}", endpoint);
                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request error to {Endpoint}", endpoint);
                throw new ApplicationException($"Error de conexión con la API: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in POST request to {Endpoint}", endpoint);
                throw new ApplicationException($"Error inesperado: {ex.Message}");
            }
        }

        public async Task<T> PostFormDataAsync<T>(string endpoint, MultipartFormDataContent formData)
        {
            try
            {
                var client = await GetClientAsync();
                _logger.LogInformation("Making POST FormData request to {Endpoint}", endpoint);

                // ✅ CORREGIDO: Usar PostAsync directamente con el formData
                var response = await client.PostAsync(endpoint, formData);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized access to {Endpoint}", endpoint);
                    throw new UnauthorizedAccessException("Acceso no autorizado");
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("POST FormData request failed: {StatusCode} - {Endpoint} - {Error}",
                        response.StatusCode, endpoint, errorContent);

                    // ✅ MEJORAR: Lanzar excepción con detalles del error
                    throw new ApplicationException($"Error en la API ({response.StatusCode}): {errorContent}");
                }

                // ✅ CORREGIDO: Manejar respuesta vacía
                if (response.StatusCode == HttpStatusCode.NoContent ||
                    response.Content.Headers.ContentLength == 0)
                {
                    return default(T);
                }

                var result = await response.Content.ReadFromJsonAsync<T>();
                _logger.LogInformation("POST FormData request successful to {Endpoint}", endpoint);
                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request error to {Endpoint}", endpoint);
                throw new ApplicationException($"Error de conexión con la API: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in POST FormData request to {Endpoint}", endpoint);
                throw new ApplicationException($"Error inesperado: {ex.Message}");
            }
        }

        public async Task<byte[]> PostAsyncBytes(string endpoint, object data)
        {
            try
            {
                var client = await GetClientAsync();
                var response = await client.PostAsJsonAsync(endpoint, data);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException("Acceso no autorizado");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new ApplicationException($"Error en la API: {response.StatusCode}");
                }

                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in POST bytes request to {Endpoint}", endpoint);
                throw;
            }
        }

        public async Task<T> PutAsync<T>(string endpoint, object data)
        {
            try
            {
                var client = await GetClientAsync();
                var response = await client.PutAsJsonAsync(endpoint, data);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException("Acceso no autorizado");
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("PUT request failed: {StatusCode} - {Endpoint} - {Error}",
                        response.StatusCode, endpoint, errorContent);
                    return default(T);
                }

                if (response.StatusCode == HttpStatusCode.NoContent ||
                    response.Content.Headers.ContentLength == 0)
                {
                    return default(T);
                }

                return await response.Content.ReadFromJsonAsync<T>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PUT request to {Endpoint}", endpoint);
                return default(T);
            }
        }

        public async Task<bool> DeleteAsync(string endpoint)
        {
            try
            {
                var client = await GetClientAsync();
                var response = await client.DeleteAsync(endpoint);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException("Acceso no autorizado");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DELETE request to {Endpoint}", endpoint);
                throw;
            }
        }
    }
}