using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BotWhatsappCSharp.Services
{
    public class WhatsappService
    {
        private IWebDriver _driver;
        private bool _botAtivo = false;
        private readonly string _sessionPath;

        public WhatsappService(string sessionPath)
        {
            _sessionPath = sessionPath;
        }

        public bool IsDriverAtivo()
        {
            try
            {
                return _driver != null && _driver.WindowHandles.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task IniciarAsync(Action<string> onStatus)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (IsDriverAtivo())
                    {
                        onStatus("⚠ Navegador já está aberto.");
                        return;
                    }

                    onStatus("Iniciando Navegador...");
                    Fechar(); // Garante limpeza anterior

                    var options = new ChromeOptions();
                    options.AddArgument("--user-data-dir=" + _sessionPath);
                    options.AddArgument("--log-level=3");
                    options.AddArgument("--remote-allow-origins=*");
                    options.AddArgument("--disable-blink-features=AutomationControlled");
                    
                    // Stealth & Stability
                    options.AddArgument("--no-sandbox");
                    options.AddArgument("--disable-dev-shm-usage");
                    options.AddArgument("--disable-gpu");
                    options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    
                    options.AddExcludedArgument("enable-automation");
                    options.AddAdditionalOption("useAutomationExtension", false);

                    _driver = new ChromeDriver(options);
                    _driver.Navigate().GoToUrl("https://web.whatsapp.com");

                    onStatus("Aguardando QR Code ou Login...");
                    
                    var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(60));
                    try 
                    {
                        wait.Until(d => d.FindElements(By.Id("side")).Count > 0);
                        onStatus("✅ Conectado!");
                    }
                    catch (WebDriverTimeoutException)
                    {
                        onStatus("⚠ Tempo limite excedido. Tente escanear novamente.");
                    }
                }
                catch (Exception e)
                {
                    onStatus("Erro: " + e.Message);
                    Fechar();
                }
            });
        }

        public void LigarBot()
        {
            _botAtivo = true;
        }

        public void DesligarBot()
        {
            _botAtivo = false;
        }

        public void EnviarMensagem(string numero, string mensagem)
        {
            try
            {
                // Limpar número (apenas dígitos)
                string numLimpo = new string(numero.Where(char.IsDigit).ToArray());
                if (!numLimpo.StartsWith("55") && numLimpo.Length <= 11) numLimpo = "55" + numLimpo;

                string url = $"https://web.whatsapp.com/send?phone={numLimpo}";
                
                if (!IsDriverAtivo()) { Console.WriteLine("Tentativa de envio com driver inativo."); return; }

                _driver.Navigate().GoToUrl(url);

                var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(25));
                
                // Espera a caixa de mensagem aparecer
                var box = wait.Until(d => d.FindElement(By.XPath("//footer//div[@contenteditable='true'] | //div[@contenteditable='true'][@data-tab='10']")));
                
                box.Click();
                Thread.Sleep(500);

                // Digitação Humana
                var rand = new Random();
                foreach (char c in mensagem)
                {
                    box.SendKeys(c.ToString());
                    Thread.Sleep(rand.Next(20, 80)); 
                }

                Thread.Sleep(800);
                box.SendKeys(Keys.Enter);
                
                // Aguarda um pouco antes de considerar "enviado" para evitar fechar antes da hora
                Thread.Sleep(2000); 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao enviar para {numero}: {ex.Message}");
            }
        }

        public void EnviarAnexo(string caminhoArquivo)
        {
            try
            {
                if (!System.IO.File.Exists(caminhoArquivo)) return;
                if (!IsDriverAtivo()) { Console.WriteLine("Tentativa de envio de anexo com driver inativo."); return; }

                // 1. Clique no botão de anexar (+)
                var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
                var attachBtn = wait.Until(d => d.FindElement(By.XPath("//div[@title='Anexar'] | //span[@data-icon='plus'] | //span[@data-icon='add-light']")));
                attachBtn.Click();
                Thread.Sleep(500);

                // 2. O input de arquivo fica oculto. Vamos encontrá-lo e enviar o path.
                var fileInput = _driver.FindElement(By.XPath("//input[@type='file']"));
                fileInput.SendKeys(caminhoArquivo);
                Thread.Sleep(1500); // Aguarda carregar preview

                // 3. Clique no botão de enviar anexo
                var sendBtn = wait.Until(d => d.FindElement(By.XPath("//span[@data-icon='send']")));
                sendBtn.Click();
                Thread.Sleep(2000); // Aguarda envio
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao enviar anexo: " + ex.Message);
            }
        }

        public void ProcessarMensagensNaoLidas(Func<string, byte[], (string Resposta, string Anexo)> gerarResposta, Action<string> onLog = null)
        {
            try
            {
                var unreadBadges = _driver.FindElements(By.XPath("//span[@aria-label]//span[contains(@dir, 'ltr')] | //span[contains(@aria-label, 'não lida')] | //span[contains(@aria-label, 'unread')]"));
                
                if (unreadBadges.Count == 0) return;

                onLog?.Invoke($"[DEBUG] {unreadBadges.Count} conversas com notificação detectadas.");
                onLog?.Invoke($"{unreadBadges.Count} conversas não lidas.");

                foreach (var badge in unreadBadges.ToList())
                {
                    try 
                    {
                        IWebElement chatRow;
                        try {
                            // Tenta encontrar o container do chat que é clicável e contém os dados principais
                            chatRow = badge.FindElement(By.XPath("./ancestor::div[@role='listitem' or contains(@class, 'lhwtv60a') or contains(@class, '_ak8l')]"));
                        } catch {
                            // Fallback mais agressivo subindo até encontrar um div com largura significativa que pareça o card
                            chatRow = badge;
                            for(int i=0; i<10; i++) {
                                try {
                                    if (chatRow.TagName == "div" && chatRow.Size.Width > 200) break;
                                    chatRow = chatRow.FindElement(By.XPath(".."));
                                } catch { break; }
                            }
                        }
                        
                        // Garante visibilidade e clica
                        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView(true);", chatRow);
                        Thread.Sleep(500);
                        chatRow.Click();
                        Thread.Sleep(1500); 

                        var msgs = _driver.FindElements(By.XPath("//div[contains(@class, 'message-in')]"));
                        if (msgs.Count > 0)
                        {
                            var lastMsg = msgs.Last();
                            string texto = "";
                            byte[] audioData = null;

                            // Verifica se é audio (mesma lógica anterior, mas dentro do fluxo otimizado)
                            try {
                                var audioBubbles = lastMsg.FindElements(By.XPath(".//div[contains(@data-testid, 'audio-bubble')] | .//span[@data-testid='audio-play']"));
                                if (audioBubbles.Count > 0)
                                {
                                    onLog?.Invoke("[DEBUG] Áudio detectado. Tentando extrair...");
                                    string script = @"
                                        var audio = arguments[0].querySelector('audio');
                                        if (!audio) return null;
                                        var xhr = new XMLHttpRequest();
                                        xhr.open('GET', audio.src, false);
                                        xhr.responseType = 'arraybuffer';
                                        xhr.send(null);
                                        if (xhr.status === 200) {
                                            var bytes = new Uint8Array(xhr.response);
                                            return Array.from(bytes);
                                        }
                                        return null;";
                                    
                                    var js = (IJavaScriptExecutor)_driver;
                                    var result = js.ExecuteScript(script, lastMsg) as System.Collections.Generic.IEnumerable<object>;
                                    if (result != null)
                                    {
                                        audioData = result.Select(x => Convert.ToByte(x)).ToArray();
                                        onLog?.Invoke($"[DEBUG] Áudio extraído com sucesso ({audioData.Length} bytes).");
                                    }
                                }
                            } catch (Exception ex) {
                                onLog?.Invoke($"[DEBUG] Falha ao capturar áudio: {ex.Message}");
                            }

                            // EXTRAÇÃO DE TEXTO MELHORADA: Busca apenas o conteúdo real da mensagem
                            try {
                                // Tenta pegar o span específico do texto, ignorando adornos e horários
                                var textElement = lastMsg.FindElement(By.XPath(".//span[contains(@class, 'selectable-text') and not(contains(@class, 'lhwtv60a'))] | .//div[contains(@class, 'copyable-text')]//span"));
                                texto = textElement.Text;
                            } catch {
                                // Fallback: pegar o texto da bolha mas filtrar linhas que parecem horário (HH:mm)
                                string fullText = lastMsg.Text;
                                var lines = fullText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                                    .Where(l => !System.Text.RegularExpressions.Regex.IsMatch(l.Trim(), @"^\d{1,2}:\d{2}(\s?[AaPp][Mm])?$"))
                                                    .Where(l => l.Length > 0 && !l.Contains("Lida") && !l.Contains("✅"))
                                                    .ToList();
                                texto = lines.FirstOrDefault() ?? "";
                            }

                            if (!string.IsNullOrWhiteSpace(texto) || audioData != null)
                            {
                                if (!string.IsNullOrWhiteSpace(texto))
                                {
                                    onLog?.Invoke($"[DEBUG] Texto capturado: '{texto}'");
                                    onLog?.Invoke($"Lido: {texto.Substring(0, Math.Min(20, texto.Length))}...");
                                }

                                var (resposta, anexo) = gerarResposta(texto, audioData);
                                
                                if (!string.IsNullOrWhiteSpace(resposta))
                                {
                                    EnviarMensagemTextoAtual(resposta);
                                }

                                 if (!string.IsNullOrWhiteSpace(anexo))
                                {
                                    var files = anexo.Split('|', StringSplitOptions.RemoveEmptyEntries);
                                    foreach (var file in files)
                                    {
                                        EnviarAnexo(file);
                                        Thread.Sleep(1000); // Pequena pausa entre anexos
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        onLog?.Invoke("Erro no Chat: " + ex.Message);
                    }

                    try { new Actions(_driver).SendKeys(Keys.Escape).Perform(); } catch {}
                    Thread.Sleep(500);
                }
            }
            catch (Exception ex)
            {
                onLog?.Invoke("Erro Loop: " + ex.Message);
            }
        }

        public void EnviarMensagemTextoAtual(string mensagem)
        {
            try
            {
                // Seletor mais moderno para a caixa de texto
                var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));
                var box = wait.Until(d => d.FindElement(By.XPath("//footer//div[@contenteditable='true'] | //div[@contenteditable='true'][@data-tab='10']")));
                
                box.Click();
                Thread.Sleep(300);

                // Digitação Humana (Simulando)
                var rand = new Random();
                foreach (char c in mensagem)
                {
                    box.SendKeys(c.ToString());
                    // Atraso randômico entre 30ms e 120ms por letra
                    Thread.Sleep(rand.Next(30, 120));
                }
                
                Thread.Sleep(500);
                box.SendKeys(Keys.Enter);
            }
            catch (Exception e)
            {
                Console.WriteLine("Erro ao responder: " + e.Message);
            }
        }

        public void Fechar()
        {
            _botAtivo = false;
            try { _driver?.Quit(); } catch {}
        }
    }
}
