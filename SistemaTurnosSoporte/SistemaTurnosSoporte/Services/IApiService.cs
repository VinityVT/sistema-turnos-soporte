using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace SistemaSoporte.Services
{
    public interface IApiService
    {
        Task<T> GetAsync<T>(string endpoint);
        Task<T> PostAsync<T>(string endpoint, object data);
        Task<byte[]> PostAsyncBytes(string endpoint, object data);
        Task<T> PutAsync<T>(string endpoint, object data);
        Task<bool> DeleteAsync(string endpoint);
        Task<T> PostFormDataAsync<T>(string endpoint, MultipartFormDataContent formData);
        Task<byte[]> GetBytesAsync(string endpoint);
        Task<byte[]> DownloadFileAsync(string endpoint);
    }
}