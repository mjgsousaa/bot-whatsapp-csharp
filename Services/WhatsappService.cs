using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BotWhatsappCSharp.Models;

namespace BotWhatsappCSharp.Services
{
    public class WhatsappService
    {
        private static readonly HttpClient _client = new HttpClient();
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly string _instanceName;
        private bool _conectado = false;

        public WhatsappService(string baseUrl, string apiKey, string instanceName)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _apiKey = apiKey;
            _instanceName = instanceName;

            _client.DefaultRequestHeaders.Clear();
            _client.DefaultRequestHeaders.Add("apikey", _apiKey);
        }

        public bool IsDriverAtivo() => _conectado;

        public async Task IniciarAsync(Action<string> onStatus)
        {
            onStatus("Verificando conexão com Evolution API...");
            int tentativas = 0;

            while (tentativas < 20)
            {
                try
                {
                    var response = await _client.GetAsync($"{_baseUrl}/instance/connectionState/{_instanceName}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string body = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(body);
                        string? state = doc.RootElement.GetProperty("instance").GetProperty("state").GetString();

                        if (state == "open")
                        {
                            _conectado = true;
                            onStatus("✅ Conectado!");
                            return;
                        }
                        else if (state == "connecting" || state == "qr")
                        {
                            onStatus($"📱 Aguardando QR Code ({tentativas + 1}/20)...");
                        }
                        else
                        {
                            onStatus("📱 Criando/Resetando instância...");
                            await CriarInstanciaAsync();
                        }
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        onStatus("📱 Instância não encontrada. Criando...");
                        await CriarInstanciaAsync();
                    }
                }
                catch (Exception ex)
                {
                    onStatus($"❌ Erro: {ex.Message}");
                }

                tentativas++;
                await Task.Delay(3000);
            }

            onStatus("❌ Tempo limite excedido. Verifique o painel da Evolution API.");
        }

        private async Task CriarInstanciaAsync()
        {
            var body = new
            {
                instanceName = _instanceName,
                qrcode = true,
                integration = "WHATSAPP-BAILEYS"
            };

            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            await _client.PostAsync($"{_baseUrl}/instance/create", content);
        }

        public async Task ConfigurarWebhookManualAsync(string webhookUrl)
        {
            var body = new
            {
                enabled = true,
                url = webhookUrl,
                webhookByEvents = false,
                webhookBase64 = true,
                events = new[] { "messages.upsert", "connection.update" }
            };

            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync($"{_baseUrl}/webhook/set/{_instanceName}", content);
            Logger.Log("Webhook configurado: " + response.StatusCode);
        }

        public void EnviarMensagem(string numero, string mensagem)
        {
            try
            {
                string numLimpo = LimparNumero(numero);
                var body = new { number = numLimpo, text = mensagem };
                var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

                var response = _client.PostAsync($"{_baseUrl}/message/sendText/{_instanceName}", content).GetAwaiter().GetResult();
                
                if (response.IsSuccessStatusCode)
                {
                    Logger.Log($"[SISTEMA] Resposta enviada com sucesso para {numero}");
                }
                else
                {
                    string error = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    Logger.Log($"[ERRO API] Falha ao enviar para {numero}: {response.StatusCode} - {error}");
                }

                Random rand = new Random();
                Thread.Sleep(rand.Next(1200, 3500));
            }
            catch (Exception ex) { Logger.Log($"Erro EnviarMensagem: {ex.Message}"); }
        }

        public void EnviarAnexo(string numero, string caminhoArquivo, string legenda = "")
        {
            try
            {
                if (!File.Exists(caminhoArquivo)) return;
                string numLimpo = LimparNumero(numero);
                string extensao = Path.GetExtension(caminhoArquivo).ToLower();
                string mediaType = "document";
                string endpoint = "sendMedia";

                if (new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(extensao)) mediaType = "image";
                else if (new[] { ".mp4", ".avi", ".mov" }.Contains(extensao)) mediaType = "video";
                else if (new[] { ".mp3", ".ogg", ".aac", ".opus" }.Contains(extensao)) 
                { 
                    mediaType = "audio"; 
                    endpoint = "sendPtv";
                }

                byte[] bytes = File.ReadAllBytes(caminhoArquivo);
                string base64 = Convert.ToBase64String(bytes);

                object body;
                if (mediaType == "audio")
                {
                    body = new { number = numLimpo, audio = base64, encoding = true };
                }
                else
                {
                    body = new {
                        number = numLimpo,
                        mediatype = mediaType,
                        mimetype = DetectarMimeType(caminhoArquivo),
                        caption = legenda,
                        media = base64,
                        fileName = Path.GetFileName(caminhoArquivo)
                    };
                }

                var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                var response = _client.PostAsync($"{_baseUrl}/message/{endpoint}/{_instanceName}", content).GetAwaiter().GetResult();
                
                if (response.IsSuccessStatusCode)
                {
                    Logger.Log($"[SISTEMA] Anexo ({mediaType}) enviado para {numero}");
                }
                else
                {
                    string error = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    Logger.Log($"[ERRO API] Falha ao enviar anexo: {response.StatusCode} - {error}");
                }
                
                Thread.Sleep(2000);
            }
            catch (Exception ex) { Logger.Log($"Erro EnviarAnexo: {ex.Message}"); }
        }

        public void EnviarAnexoGatilho(string numero, string caminhoArquivo, string legenda, TipoMidia tipo)
        {
            string mediaType = tipo switch {
                TipoMidia.Imagem => "image",
                TipoMidia.PDF    => "document",
                TipoMidia.Audio  => "audio",
                TipoMidia.Video  => "video",
                _                => "document"
            };
            
            if (!File.Exists(caminhoArquivo))
            {
                Logger.Error($"Arquivo não encontrado: {caminhoArquivo}");
                return;
            }
            
            try
            {
                byte[] bytes = File.ReadAllBytes(caminhoArquivo);
                string base64 = Convert.ToBase64String(bytes);
                string mime = DetectarMimeType(caminhoArquivo);
                string nomeArq = Path.GetFileName(caminhoArquivo);
                string numLimpo = LimparNumero(numero);
                
                object body;
                string endpoint;
                
                if (tipo == TipoMidia.Audio)
                {
                    endpoint = $"{_baseUrl}/message/sendPtv/{_instanceName}";
                    body = new { number = numLimpo, audio = base64, encoding = true };
                }
                else
                {
                    endpoint = $"{_baseUrl}/message/sendMedia/{_instanceName}";
                    body = new {
                        number = numLimpo,
                        mediatype = mediaType,
                        mimetype = mime,
                        caption = legenda,
                        media = base64,
                        fileName = nomeArq
                    };
                }

                var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                var response = _client.PostAsync(endpoint, content).GetAwaiter().GetResult();
                
                if (!response.IsSuccessStatusCode)
                {
                    string err = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    Logger.Error($"Erro ao enviar mídia ({tipo}): {err}");
                }
                
                var rand = new Random();
                Thread.Sleep(rand.Next(1200, 3000));
            }
            catch (Exception ex) { Logger.Error("Exceção ao enviar mídia", ex); }
        }

        public void ProcessarMensagemWebhook(
            string numeroRemetente, 
            string texto, 
            byte[]? audioData,
            string contexto,
            Func<string, string, byte[]?, string, (string Resposta, string Anexo)> gerarResposta,
            Action<string> onLog)
        {
            var (resposta, anexo) = gerarResposta(numeroRemetente, texto, audioData, contexto);
            
            if (!string.IsNullOrWhiteSpace(anexo))
            {
                var files = anexo.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (files.Length == 1)
                {
                    EnviarAnexo(numeroRemetente, files[0], resposta);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(resposta)) EnviarMensagem(numeroRemetente, resposta);
                    foreach (var file in files)
                    {
                        EnviarAnexo(numeroRemetente, file);
                        Thread.Sleep(1000);
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(resposta))
            {
                EnviarMensagem(numeroRemetente, resposta);
            }
        }

        private string DetectarMimeType(string caminho)
        {
            switch (Path.GetExtension(caminho).ToLower())
            {
                case ".jpg": case ".jpeg": return "image/jpeg";
                case ".png": return "image/png";
                case ".gif": return "image/gif";
                case ".webp": return "image/webp";
                case ".mp4": return "video/mp4";
                case ".mp3": return "audio/mpeg";
                case ".ogg": case ".opus": return "audio/ogg";
                case ".aac": return "audio/aac";
                case ".pdf": return "application/pdf";
                default: return "application/octet-stream";
            }
        }

        private string LimparNumero(string numero)
        {
            if (string.IsNullOrEmpty(numero)) return "";
            string num = numero.Split('@')[0];
            num = new string(num.Where(char.IsDigit).ToArray());
            if (num.Length <= 11 && !num.StartsWith("55")) num = "55" + num;
            return num;
        }

        public void SetConectado(bool conectado) { _conectado = conectado; }
        public void LigarBot() { _conectado = true; }
        public void DesligarBot() { _conectado = false; }
        public void Fechar() { _conectado = false; }
    }
}
