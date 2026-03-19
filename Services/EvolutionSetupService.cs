using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace BotWhatsappCSharp.Services
{
  public class EvolutionSetupService
  {
    private static readonly HttpClient _client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    private readonly string _baseUrl;
    private readonly string _apiKey;

    public EvolutionSetupService()
    {
      _baseUrl = KeyManager.EvoBaseUrl;
      _apiKey  = KeyManager.EvoApiKey;
      _client.DefaultRequestHeaders.Clear();
      _client.DefaultRequestHeaders.Add("apikey", _apiKey);
    }

    // Retorna: "open" | "qr" | "connecting" | "error"
    public async Task<string> VerificarEstado(string instancia)
    {
      try
      {
        var r = await _client.GetAsync($"{_baseUrl}/instance/connectionState/{instancia}");
        if (!r.IsSuccessStatusCode) return "error";
        
        string json = await r.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        
        if (doc.RootElement.TryGetProperty("instance", out var inst) &&
            inst.TryGetProperty("state", out var state))
          return state.GetString() ?? "error";
        
        return "error";
      }
      catch { return "error"; }
    }

    // Cria instância se não existir
    public async Task<bool> CriarInstanciaSeNecessario(string instancia)
    {
      try
      {
        // Verifica se já existe
        var fetchR = await _client.GetAsync($"{_baseUrl}/instance/fetchInstances");
        if (fetchR.IsSuccessStatusCode)
        {
          string fetchJson = await fetchR.Content.ReadAsStringAsync();
          if (fetchJson.Contains($"\"instanceName\":\"{instancia}\"") || fetchJson.Contains($"\"{instancia}\""))
            return true;
        }

        // Cria nova instância
        var body = new {
          instanceName = instancia,
          qrcode = true,
          integration = "WHATSAPP-BAILEYS",
          rejectCall = false,
          groupsIgnore = true,
          alwaysOnline = true,
          readMessages = true,
          readStatus = false,
          syncFullHistory = false
        };

        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var r = await _client.PostAsync($"{_baseUrl}/instance/create", content);

        return r.IsSuccessStatusCode;
      }
      catch (Exception ex)
      {
        Logger.Error("CriarInstancia", ex);
        return false;
      }
    }

    // Busca QR Code como base64
    public async Task<string> BuscarQrCode(string instancia)
    {
      try
      {
        var r = await _client.GetAsync($"{_baseUrl}/instance/connect/{instancia}");
        if (!r.IsSuccessStatusCode) return null;

        string json = await r.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("base64", out var b64))
          return b64.GetString();

        if (doc.RootElement.TryGetProperty("qrcode", out var qr) &&
            qr.TryGetProperty("base64", out var qrb64))
          return qrb64.GetString();

        return null;
      }
      catch { return null; }
    }

        public async Task<string> GetExternalIP()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                return await client.GetStringAsync("https://api.ipify.org");
            }
            catch { return "127.0.0.1"; }
        }

        public async Task ConfigurarWebhook(string instancia, int webhookPort)
        {
            try
            {
                string ipExterno = await GetExternalIP();
                
                // URL que a Evolution API (na VPS) tentará chamar
                string webhookUrl = $"http://{ipExterno}:{webhookPort}/webhook/{instancia}"; 
                
                Logger.Log($"[SISTEMA] Solicitando que Evolution envie mensagens para: {webhookUrl}");

                var body = new {
                    enabled = true,
                    url = webhookUrl,
                    errorWebhook = "",
                    webhookByEvents = false,
                    webhookBase64 = true,
                    events = new[] { "MESSAGES_UPSERT", "MESSAGES_UPDATE", "CONNECTION_UPDATE" }
                };

                var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                var response = await _client.PostAsync($"{_baseUrl}/webhook/set/{instancia}", content);
                
                if (response.IsSuccessStatusCode)
                    Logger.Log($"[SISTEMA] Webhook configurado com sucesso no IP: {ipExterno}");
                else
                {
                    string err = await response.Content.ReadAsStringAsync();
                    Logger.Error($"[ERRO API] Falha ao configurar Webhook: {response.StatusCode} - {err}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("ConfigurarWebhook", ex);
            }
        }

        public async Task<bool> ReiniciarInstancia(string instancia)
        {
            try
            {
                await _client.DeleteAsync($"{_baseUrl}/instance/delete/{instancia}");
                await Task.Delay(1000);
                return await CriarInstanciaSeNecessario(instancia);
            }
            catch { return false; }
        }

        public async Task<bool> RemoverInstancia(string instancia)
    {
      try
      {
        var r = await _client.DeleteAsync($"{_baseUrl}/instance/delete/{instancia}");
        return r.IsSuccessStatusCode;
      }
      catch { return false; }
    }
  }
}
