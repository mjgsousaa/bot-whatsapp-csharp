using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Linq;

namespace BotWhatsappCSharp.Services
{
    public class WebhookServer
    {
        private HttpListener? _listener;
        private readonly int _porta;
        private bool _ativo = false;
        private readonly Action<string, string, byte[]?, string, string> _onMensagem;
        private readonly Action<string> _onLog;

        public WebhookServer(int porta, Action<string, string, byte[]?, string, string> onMensagem, Action<string> onLog)
        {
            _porta = porta;
            _onMensagem = onMensagem;
            _onLog = onLog;
        }

        public bool IsAtivo() => _ativo;

        public void Iniciar()
        {
            if (_ativo) return;
            try
            {
                _listener = new HttpListener();
                // O prefixo '+' permite escutar em todos os IPs vinculados à máquina na porta especificada
                _listener.Prefixes.Add($"http://+:{_porta}/webhook/");
                _listener.Start();
                _ativo = true;
                _onLog($"[SISTEMA] Webhook Ativo: http://seu-ip:{_porta}/webhook/");
                Task.Run(() => Loop());
            }
            catch (Exception ex)
            {
                _ativo = false;
                _onLog($"[ERRO CRÍTICO] Falha ao iniciar Webhook: {ex.Message}");
                if (ex.Message.Contains("Acesso negado"))
                    _onLog("DICA: Tente rodar o programa como Administrador ou use: netsh http add urlacl url=http://+:{_porta}/webhook/ user=Todos");
            }
        }

        private async Task Loop()
        {
            while (_ativo && _listener != null)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync();
                    _ = Task.Run(() => ProcessarRequisicao(ctx));
                }
                catch (Exception ex) when (_ativo)
                {
                    _onLog("Erro loop webhook: " + ex.Message);
                }
            }
        }

        private void ProcessarRequisicao(HttpListenerContext ctx)
        {
            try
            {
                using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                string body = reader.ReadToEnd();
                
                ctx.Response.StatusCode = 200;
                ctx.Response.Close();
                
                if (string.IsNullOrWhiteSpace(body)) return;

                // Log do JSON para diagnóstico
                _onLog($"[WEBHOOK] JSON Recebido: {body}");
                
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                
                if (!root.TryGetProperty("event", out var evtEl)) return;
                string? evt = evtEl.GetString();
                
                // Suporta "messages.upsert" (v1) e "MESSAGES_UPSERT" (v2)
                if (evt?.ToLower() != "messages.upsert" && evt?.ToLower() != "message") return;
                
                if (!root.TryGetProperty("data", out var data)) return;

                // Em v2 o dado vem dentro de um array ou direto. Evolution v2 costuma enviar array em data.
                JsonElement messageElement = data;
                if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
                    messageElement = data[0];
                
                if (messageElement.TryGetProperty("key", out var key))
                {
                    if (key.TryGetProperty("fromMe", out var fromMe) && fromMe.GetBoolean()) {
                        _onLog("[WEBHOOK] Ignorando mensagem enviada por mim mesmo.");
                        return;
                    }
                }
                
                string numero = "";
                if (messageElement.TryGetProperty("key", out var keyEl) && keyEl.TryGetProperty("remoteJid", out var jidEl))
                {
                    string? jid = jidEl.GetString();
                    if (jid != null && jid.Contains("@g.us")) {
                        _onLog("[WEBHOOK] Ignorando mensagem de grupo.");
                        return;
                    }
                    numero = jid?.Split('@')[0] ?? "";
                }
                
                if (string.IsNullOrEmpty(numero)) return;

                string nomeContato = "Cliente";
                if (messageElement.TryGetProperty("pushName", out var pnEl))
                    nomeContato = pnEl.GetString() ?? "Cliente";
                
                string texto = "";
                byte[]? audioData = null;
                
                if (messageElement.TryGetProperty("message", out var msg))
                {
                    if (msg.TryGetProperty("conversation", out var conv))
                        texto = conv.GetString() ?? "";
                    else if (msg.TryGetProperty("extendedTextMessage", out var ext) && ext.TryGetProperty("text", out var extText))
                        texto = extText.GetString() ?? "";
                    else if (msg.TryGetProperty("extendedTextMessage", out var ext2) && ext2.TryGetProperty("conversation", out var extConv))
                        texto = extConv.GetString() ?? "";
                    else if (msg.TryGetProperty("audioMessage", out var audio) || msg.TryGetProperty("pttMessage", out var ptt))
                    {
                        if (messageElement.TryGetProperty("message", out var msgA) && 
                           (msgA.TryGetProperty("base64", out var b64) || msgA.TryGetProperty("audio", out var b64_2)))
                        {
                            string? b64String = null;
                            if (msgA.TryGetProperty("base64", out var b64_val)) b64String = b64_val.GetString();
                            else if (msgA.TryGetProperty("audio", out var b64_val2)) b64String = b64_val2.GetString();

                            if (!string.IsNullOrEmpty(b64String))
                            {
                                try { audioData = Convert.FromBase64String(b64String); }
                                catch { }
                            }
                        }
                        texto = "[ÁUDIO]";
                    }
                    else if (msg.TryGetProperty("imageMessage", out var img) && img.TryGetProperty("caption", out var cap))
                        texto = cap.GetString() ?? "";
                }
                
                if (string.IsNullOrWhiteSpace(texto) && audioData == null) {
                    _onLog("[WEBHOOK] Mensagem vazia ou tipo não suportado.");
                    return;
                }
                
                _onLog($"[WEBHOOK] Mensagem identificada de {numero}: {texto}");

                string contexto = "";
                string txtMinusculo = texto.ToLower();
                if (txtMinusculo.Contains("agendar") || txtMinusculo.Contains("horário") || txtMinusculo.Contains("disponível"))
                {
                    contexto = "AGENDAMENTO_TRIGGER";
                }
                
                _onMensagem(numero, texto, audioData, contexto, nomeContato);
            }
            catch (Exception ex)
            {
                _onLog("Erro ao processar webhook: " + ex.Message);
            }
        }

        public void Parar()
        {
            _ativo = false;
            try { _listener?.Stop(); } catch { }
        }
    }
}
