using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EvolutionBot.Api.Configuration;
using EvolutionBot.Api.DTOs.EvolutionApi;

namespace EvolutionBot.Api.Services
{
    public class EvolutionApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EvolutionApiService> _logger;
        private readonly EvolutionApiSettings _settings;

        public EvolutionApiService(HttpClient httpClient, ILogger<EvolutionApiService> logger, IOptions<EvolutionApiSettings> settings)
        {
            _httpClient = httpClient;
            _logger = logger;
            _settings = settings.Value;

            _httpClient.BaseAddress = new System.Uri(_settings.BaseUrl);
            _httpClient.DefaultRequestHeaders.Add("apikey", _settings.ApiKey);
        }

        public async Task<CreateInstanceResponse> CreateInstance(string instanceName)
        {
            try
            {
                var request = new { instanceName = instanceName };
                var jsonContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                
                // Assuming the endpoint for creating an instance is something like /instance/create
                var response = await _httpClient.PostAsync("/instance/create", jsonContent);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                var createInstanceResponse = JsonSerializer.Deserialize<CreateInstanceResponse>(responseBody);
                
                _logger.LogInformation("Instância criada com sucesso: {InstanceId}", createInstanceResponse?.InstanceId);
                return createInstanceResponse;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro de requisição HTTP ao criar instância: {Message}", ex.Message);
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Erro de desserialização JSON ao criar instância: {Message}", ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao criar instância: {Message}", ex.Message);
                return null;
            }
        }

        public async Task<bool> SendMessage(string instanceId, SendMessageRequest messageRequest)
        {
            try
            {
                var jsonContent = new StringContent(JsonSerializer.Serialize(messageRequest), Encoding.UTF8, "application/json");

                // Assuming the endpoint for sending a message is something like /message/send/{instanceId}
                var response = await _httpClient.PostAsync($"/message/send/{instanceId}", jsonContent);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation("Mensagem enviada com sucesso para {Number} na instância {InstanceId}", messageRequest.Number, instanceId);
                return true;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro de requisição HTTP ao enviar mensagem: {Message}", ex.Message);
                return false;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Erro de serialização JSON ao enviar mensagem: {Message}", ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao enviar mensagem: {Message}", ex.Message);
                return false;
            }
        }
    }
}
