using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BotWhatsappCSharp.Services
{
    public class AiAssistant
    {
        private readonly string _apiKey;
        private readonly string _systemPrompt;
        private static readonly HttpClient _client = new HttpClient();

        public AiAssistant(string apiKey, string systemPrompt)
        {
            _apiKey = apiKey;
            _systemPrompt = systemPrompt;
        }

        public async Task<string> GerarRespostaAsync(string userMessage, string context = "")
        {
            var messages = new System.Collections.Generic.List<object>
            {
                new { role = "system", content = _systemPrompt + (string.IsNullOrEmpty(context) ? "" : "\n\nCONTEXTO ATUAL:\n" + context) }
            };

            // Adiciona histórico ou contexto se necessário no futuro
            messages.Add(new { role = "user", content = userMessage });

            var requestBody = new
            {
                model = "llama-3.3-70b-versatile",
                messages = messages.ToArray(),
                temperature = 0.7,
                max_tokens = 1024
            };

            string json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = content;

            var response = await _client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Erro na API Groq: {response.StatusCode} - {error}");
            }

            string responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }

        public async Task<string> TranscreverAudioAsync(byte[] audioData)
        {
            using var formData = new MultipartFormDataContent();
            var audioContent = new ByteArrayContent(audioData);
            audioContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/mpeg");
            
            formData.Add(audioContent, "file", "audio.mp3");
            formData.Add(new StringContent("whisper-large-v3"), "model");
            formData.Add(new StringContent("pt"), "language");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/audio/transcriptions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = formData;

            var response = await _client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            return doc.RootElement.GetProperty("text").GetString();
        }
    }
}
