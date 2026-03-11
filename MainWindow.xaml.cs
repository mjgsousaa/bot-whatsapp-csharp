using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Win32;
using BotWhatsappCSharp.Services;
using BotWhatsappCSharp.Models;
using System.Text.Json;

namespace BotWhatsappCSharp
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly WhatsappService _whatsappService;
        private readonly SchedulingService _schedulingService;
        private readonly string _sessionPath;
        private readonly string _settingsPath;
        private AiAssistant _aiAssistant; 
        private bool _isConnecting = false;
        private bool _botLoopActive = false;
        private CancellationTokenSource _cancellationTokenSource;

        // Dados
        public ObservableCollection<GatilhoModel> Gatilhos { get; set; } = new ObservableCollection<GatilhoModel>();
        public ObservableCollection<ChamadoModel> ChamadosAbertos { get; set; } = new ObservableCollection<ChamadoModel>();
        public ObservableCollection<AgendamentoModel> Agendamentos { get; set; } = new ObservableCollection<AgendamentoModel>();

        private System.Collections.Generic.List<string> _pendingTriggerFiles = new System.Collections.Generic.List<string>();
        private string _pendingAiFile = "";
        private string _pendingBulkFile = "";
        private bool _temNovoChamado = false;
        public bool TemNovoChamado 
        { 
            get => _temNovoChamado; 
            set { _temNovoChamado = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            
            // Definir caminho da sessão no AppData para evitar problemas de permissão
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _sessionPath = System.IO.Path.Combine(appData, "BotZapAI", "zap_session");
            
            // Garantir que o diretório existe (para o WhatsappService)
            if (!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(_sessionPath)))
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_sessionPath));
            
            _whatsappService = new WhatsappService(_sessionPath);
            _schedulingService = new SchedulingService();
            _settingsPath = Path.Combine(Path.GetDirectoryName(_sessionPath)!, "settings.json");
            
            LoadSettings();
            LoadTriggers();
            LoadAgendamentos();

            // Inicia na tela de conexão
            SelectView("Connection");
        }

        private void LoadAgendamentos()
        {
            Agendamentos.Clear();
            foreach (var a in _schedulingService.ListarAgendamentos()) Agendamentos.Add(a);
        }

        // --- CONEXÃO ---
        private async void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isConnecting) return;
            if (StartBtn.Content.ToString() == "BOT LIGADO") { MessageBox.Show("Bot já conectado."); return; }

            _isConnecting = true;
            StartBtn.IsEnabled = false;
            StartBtn.Content = "CONECTANDO...";
            StatusLabel.Text = "Status: Iniciando...";

            await _whatsappService.IniciarAsync(status =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusLabel.Text = "Status: " + status;
                    if (status.Contains("Conectado"))
                    {
                        StartBtn.Content = "BOT LIGADO";
                        StartBtn.Background = (Brush)FindResource("SuccessGreen");
                        StartBtn.IsEnabled = true;
                        _whatsappService.LigarBot();
                        _isConnecting = false;
                    }
                    else if (status.Contains("Erro") || status.Contains("Tempo limite") || status.Contains("aberto"))
                    {
                        StartBtn.Content = "TENTAR NOVAMENTE";
                        StartBtn.Background = (Brush)FindResource("DangerRed");
                        StartBtn.IsEnabled = true;
                        _isConnecting = false;
                    }
                });
            });
        }

        private void ClearSession_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Isso irá desconectar o WhatsApp e limpar todos os dados do navegador local (resolvendo erros de banco de dados). Deseja continuar?", "Limpar Sessão", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _whatsappService.Fechar();
                    StatusLabel.Text = "Limpando processos...";
                    
                    // Encerrar processos que podem estar travando a pasta
                    string[] processesToKill = { "chrome", "chromedriver" };
                    foreach (var procName in processesToKill)
                    {
                        foreach (var process in System.Diagnostics.Process.GetProcessesByName(procName))
                        {
                            try { process.Kill(); process.WaitForExit(1000); } catch { }
                        }
                    }

                    StatusLabel.Text = "Apagando pasta de sessão...";
                    System.Threading.Thread.Sleep(1000); 

                    if (System.IO.Directory.Exists(_sessionPath))
                    {
                        System.IO.Directory.Delete(_sessionPath, true);
                    }
                    
                    MessageBox.Show("Sessão limpa com sucesso!\nO Chrome foi encerrado e a pasta foi resetada. Agora você pode Iniciar o Sistema novamente.");
                    StatusLabel.Text = "Desconectado (Limpo)";
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erro ao limpar sessão: " + ex.Message + "\nCertifique-se de fechar todos os navegadores abertos pelo bot manualmente se o erro persistir.");
                }
            }
        }

        // --- NAVEGAÇÃO ---
        private void NavConnect_Click(object sender, RoutedEventArgs e) => SelectView("Connection");
        private void NavAI_Click(object sender, RoutedEventArgs e) => SelectView("AI");
        private void NavBulk_Click(object sender, RoutedEventArgs e) => SelectView("Bulk");
        private void NavTriggers_Click(object sender, RoutedEventArgs e) => SelectView("Triggers");
        private void NavSchedule_Click(object sender, RoutedEventArgs e) => SelectView("Schedule");
        private void NavTickets_Click(object sender, RoutedEventArgs e) 
        { 
            SelectView("Tickets"); 
            TemNovoChamado = false; 
        }

        private void SelectView(string viewName)
        {
            // Reset Buttons (SaaS Style)
            BtnNavConnect.Background = Brushes.Transparent; BtnNavConnect.Foreground = (Brush)FindResource("TextGray");
            BtnNavAI.Background = Brushes.Transparent; BtnNavAI.Foreground = (Brush)FindResource("TextGray");
            BtnNavBulk.Background = Brushes.Transparent; BtnNavBulk.Foreground = (Brush)FindResource("TextGray");
            BtnNavTriggers.Background = Brushes.Transparent; BtnNavTriggers.Foreground = (Brush)FindResource("TextGray");
            BtnNavTickets.Background = Brushes.Transparent; BtnNavTickets.Foreground = (Brush)FindResource("TextGray");
            if (BtnNavSchedule != null) { BtnNavSchedule.Background = Brushes.Transparent; BtnNavSchedule.Foreground = (Brush)FindResource("TextGray"); }

            // Hide All
            ViewConnection.Visibility = Visibility.Collapsed;
            ViewAI.Visibility = Visibility.Collapsed;
            ViewBulk.Visibility = Visibility.Collapsed;
            ViewTriggers.Visibility = Visibility.Collapsed;
            ViewTickets.Visibility = Visibility.Collapsed;
            if (ViewSchedule != null) ViewSchedule.Visibility = Visibility.Collapsed;

            // Activate Selected
            switch (viewName)
            {
                case "Connection":
                    ViewConnection.Visibility = Visibility.Visible;
                    BtnNavConnect.Background = (Brush)FindResource("BorderColor");
                    BtnNavConnect.Foreground = (Brush)FindResource("BrandPrimary");
                    break;
                case "AI":
                    ViewAI.Visibility = Visibility.Visible;
                    BtnNavAI.Background = (Brush)FindResource("BorderColor");
                    BtnNavAI.Foreground = (Brush)FindResource("BrandPrimary");
                    break;
                case "Bulk":
                    ViewBulk.Visibility = Visibility.Visible;
                    BtnNavBulk.Background = (Brush)FindResource("BorderColor");
                    BtnNavBulk.Foreground = (Brush)FindResource("BrandPrimary");
                    break;
                case "Triggers":
                    ViewTriggers.Visibility = Visibility.Visible;
                    BtnNavTriggers.Background = (Brush)FindResource("BorderColor");
                    BtnNavTriggers.Foreground = (Brush)FindResource("BrandPrimary");
                    break;
                case "Tickets":
                    ViewTickets.Visibility = Visibility.Visible;
                    BtnNavTickets.Background = (Brush)FindResource("BorderColor");
                    BtnNavTickets.Foreground = (Brush)FindResource("BrandPrimary");
                    break;
                case "Schedule":
                    if (ViewSchedule != null) ViewSchedule.Visibility = Visibility.Visible;
                    if (BtnNavSchedule != null) {
                        BtnNavSchedule.Background = (Brush)FindResource("BorderColor");
                        BtnNavSchedule.Foreground = (Brush)FindResource("BrandPrimary");
                    }
                    LoadAgendamentos();
                    break;
            }
        }

        private void AddLog(string msg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                string time = DateTime.Now.ToString("HH:mm:ss");
                LogList.Items.Insert(0, $"[{time}] {msg}");
                if (LogList.Items.Count > 100) LogList.Items.RemoveAt(100);
            });
            // Log to file as well
            Logger.Log(msg);
        }

        private void SaveSettings()
        {
            try
            {
                        var settings = new SettingsModel
                        {
                            ApiKey = ApiKeyInput.Text,
                            SystemPrompt = SystemPromptInput.Text + "\n\nDIRETRIZES DE HUMANIZAÇÃO:\n- Use empatia contextual.\n- Varie o vocabulário.\n- Use confirmação ativa (ex: 'Prontinho! Reservei seu horário...').",
                            AiMediaFile = _pendingAiFile,
                            BulkMediaFile = _pendingBulkFile
                        };
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                AddLog("Erro ao salvar configurações: " + ex.Message);
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    string json = File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<SettingsModel>(json);
                    if (settings != null)
                    {
                        ApiKeyInput.Text = settings.ApiKey;
                        SystemPromptInput.Text = settings.SystemPrompt;
                        
                        _pendingAiFile = settings.AiMediaFile;
                        TxtAiFileName.Text = string.IsNullOrEmpty(_pendingAiFile) ? "Nenhum arquivo" : Path.GetFileName(_pendingAiFile);
                        
                        _pendingBulkFile = settings.BulkMediaFile;
                        TxtBulkFileName.Text = string.IsNullOrEmpty(_pendingBulkFile) ? "Nenhum arquivo" : Path.GetFileName(_pendingBulkFile);

                        if (!string.IsNullOrEmpty(settings.ApiKey))
                        {
                            _aiAssistant = new AiAssistant(settings.ApiKey, settings.SystemPrompt);
                        }
                    }
                }
                
                if (_aiAssistant == null)
                    _aiAssistant = new AiAssistant("", "Você é um assistente virtual útil e educado.");

                // Adiciona gatilho padrão se vazio
                if (Gatilhos.Count == 0)
                {
                    Gatilhos.Add(new GatilhoModel { Comando = "/ajuda", Resposta = "Olá! Digite /preço ou /info." });
                }
            }
            catch (Exception ex)
            {
                AddLog("Erro ao carregar configurações: " + ex.Message);
                _aiAssistant = new AiAssistant("", "Você é um assistente virtual útil e educado.");
            }
        }

        // --- IA & BOT LOOP ---
        private void SaveAI_Click(object sender, RoutedEventArgs e)
        {
            string key = ApiKeyInput.Text.Trim();
            string prompt = SystemPromptInput.Text.Trim();
            
            if (string.IsNullOrEmpty(key)) { MessageBox.Show("Insira uma API Key do Groq."); return; }

            _aiAssistant = new AiAssistant(key, prompt);
            SaveSettings();
            MessageBox.Show("Configurações salvas permanentemente!");
        }

        private void AiToggle_Click(object sender, RoutedEventArgs e)
        {
            ToggleBot();
        }

        private void ToggleBot()
        {
            if (!_whatsappService.IsDriverAtivo()) { MessageBox.Show("Conecte o WhatsApp primeiro."); return; }

            if (!_botLoopActive)
            {
                // Iniciar
                _botLoopActive = true;
                AiToggleBtn.Content = "PARAR ATENDIMENTO";
                AiToggleBtn.Background = (Brush)FindResource("DangerRed");
                AiStatusLabel.Text = "STATUS: ATIVO E OUVINDO";
                AiStatusLabel.Foreground = (Brush)FindResource("SuccessGreen");
                
                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(() => RunBotLoop(_cancellationTokenSource.Token));
            }
            else
            {
                // Parar
                _botLoopActive = false;
                _cancellationTokenSource?.Cancel();
                AiToggleBtn.Content = "LIGAR ATENDIMENTO AUTOMÁTICO";
                AiToggleBtn.Background = (Brush)FindResource("BrandYellow");
                AiStatusLabel.Text = "STATUS: PARADO";
                AiStatusLabel.Foreground = (Brush)FindResource("DangerRed");
            }
        }
        private async Task RunBotLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _botLoopActive)
            {
                try
                {
                    Application.Current.Dispatcher.Invoke(() => AiStatusLabel.Text = "STATUS: MONITORANDO...");
                    
                    await Task.Run(() => 
                    {
	                        _whatsappService.ProcessarMensagensNaoLidas((numeroTelefone, mensagemRecebida, audioData, context) =>
	                        {
	                            try
	                            {
	                                // Se houver áudio, transcreve antes de prosseguir
	                                if (audioData != null && _aiAssistant != null)
	                                {
	                                    AddLog("Transcrita áudio recebido...");
	                                    mensagemRecebida = _aiAssistant.TranscreverAudioAsync(audioData).GetAwaiter().GetResult();
	                                    AddLog($"[TRANSCRICAO] {mensagemRecebida}");
	                                }
	
	                                if (string.IsNullOrWhiteSpace(mensagemRecebida)) return ("", "");
	
	                                AddLog($"[{numeroTelefone}] Mensagem: {mensagemRecebida}");
	                                
	                                // 0. Verifica se é pedido de atendente
	                                string[] termosAtendente = { "atendente", "humano", "pessoa", "falar com alguém", "atendimento", "suporte", "ajuda", "vendedor", "falar com pessoa", "human" };
	                                if (termosAtendente.Any(t => mensagemRecebida.ToLower().Contains(t)))
	                                {
	                                    AddLog($"Pedido de atendente detectado de {numeroTelefone}!");
	                                    Application.Current.Dispatcher.Invoke(() => {
	                                        if (!ChamadosAbertos.Any(c => c.Numero == numeroTelefone)) {
	                                            ChamadosAbertos.Add(new ChamadoModel { 
	                                                Numero = numeroTelefone, 
	                                                UltimaMensagem = mensagemRecebida,
	                                                Horario = DateTime.Now
	                                            });
	                                            TemNovoChamado = true;
	                                        }
	                                    });
	                                    return ("Um atendente humano foi notificado e logo falará com você. Aguarde um momento.", "");
	                                }
	
	                                // 1. Verifica Gatilhos
	                                string msgLimpa = mensagemRecebida.Trim().ToLower();
	                                var gatilho = Gatilhos.FirstOrDefault(g => 
	                                {
	                                    string cmd = g.Comando.Trim().ToLower();
	                                    if (cmd.StartsWith("/")) return msgLimpa == cmd || msgLimpa.StartsWith(cmd + " ");
	                                    return msgLimpa.Contains(cmd);
	                                });
	                                
	                                if (gatilho != null)
	                                {
	                                    AddLog($"[GATILHO] Comando '{gatilho.Comando}' detectado.");
	                                    return (gatilho.Resposta, string.Join("|", gatilho.CaminhosArquivos ?? new System.Collections.Generic.List<string>()));
	                                }
	
	                                // 2. Se não tem gatilho, usa IA
	                                if (_aiAssistant == null) {
	                                    AddLog("IA não configurada!");
	                                    return ("", "");
	                                }
	
	                                string aiContext = "";
	                                if (context == "AGENDAMENTO_TRIGGER")
	                                {
	                                    var slots = _schedulingService.GetHorariosDisponiveis(DateTime.Now.AddDays(1));
	                                    aiContext = "HORÁRIOS DISPONÍVEIS PARA AMANHÃ:\n" + string.Join("\n", slots.Take(5).Select(s => s.ToString("HH:mm")));
	                                    aiContext += "\nSe o usuário escolher, peça o nome para confirmar.";
	                                }
	
	                                AddLog("Consultando IA...");
	                                string resposta = _aiAssistant.GerarRespostaAsync(mensagemRecebida, aiContext).GetAwaiter().GetResult();
	                                
	                                // Lógica de Confirmação de Agendamento
	                                if (resposta.ToLower().Contains("confirmado") && (msgLimpa.Contains(":") || msgLimpa.Contains("h")))
	                                {
	                                    AddLog($"Agendamento detectado para {numeroTelefone}");
	                                }
	
	                                AddLog("IA respondeu com sucesso.");
	                                return (resposta, _pendingAiFile);
	                            }
	                            catch (Exception ex)
	                            {
	                                string errorMsg = ex.InnerException?.Message ?? ex.Message;
	                                AddLog($"ERRO: {errorMsg}");
	                                return ("", ""); 
	                            }
	                        }, AddLog);
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[ERRO LOOP] " + ex.Message);
                }

                await Task.Delay(3000); // Espera antes do próximo ciclo
            }
        }

        // --- DISPARO EM MASSA ---
        private void ImportContacts_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Arquivos de Texto/CSV (*.txt;*.csv)|*.txt;*.csv|Todos os arquivos (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string content = File.ReadAllText(openFileDialog.FileName);
                    // Tenta limpar e formatar
                    var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    BulkNumbersInput.Text = string.Join(Environment.NewLine, lines);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erro ao ler arquivo: " + ex.Message);
                }
            }
        }

        private CancellationTokenSource _bulkCts;

        private void DownloadTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string template = "Telefone;Nome\n5511999999999;João Exemplo\n5511888888888;Maria Exemplo";
                SaveFileDialog sfd = new SaveFileDialog
                {
                    Filter = "Arquivo CSV (*.csv)|*.csv",
                    FileName = "modelo_contatos.csv"
                };

                if (sfd.ShowDialog() == true)
                {
                    File.WriteAllText(sfd.FileName, template);
                    MessageBox.Show("Modelo salvo com sucesso! No disparo, carregue este arquivo ou cole os números (um por linha).");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao criar modelo: " + ex.Message);
            }
        }

        private async void StartBulk_Click(object sender, RoutedEventArgs e)
        {
            string msg = BulkMessageInput.Text;
            string[] numbers = BulkNumbersInput.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (string.IsNullOrWhiteSpace(msg) || numbers.Length == 0) { MessageBox.Show("Preencha a mensagem e a lista de contatos."); return; }

            // Obter delays da UI
            if (!int.TryParse(BulkDelayMin.Text, out int delayMin)) delayMin = 5;
            if (!int.TryParse(BulkDelayMax.Text, out int delayMax)) delayMax = 15;
            
            // BLOQUEIO DE CONCORRÊNCIA: Desativa bot se estiver rodando
            bool restartBot = false;
            if (_botLoopActive) 
            {
                AddLog("⚠ Pausando Bot de Atendimento para iniciar disparo em massa...");
                ToggleBot(); // Uso correto método direto
                restartBot = true;
                await Task.Delay(2000); // Wait for cancellation
            }

            // Bloqueio de UI
            StartBulkBtn.IsEnabled = false;
            ImportContactsBtn.IsEnabled = false;

            _bulkCts = new CancellationTokenSource();
            BulkStatusLabel.Text = "🚀 Iniciando disparo stealth...";

            await Task.Run(async () =>
            {
                int count = 0;
                Random rand = new Random();

                foreach (var num in numbers)
                {
                    if (_bulkCts.Token.IsCancellationRequested) break;

                    Application.Current.Dispatcher.Invoke(() => BulkStatusLabel.Text = $"Enviando {count + 1}/{numbers.Length} para {num.Trim()}...");
                    
                    // Enviar com legenda se houver anexo, senão envia apenas texto
                    if (!string.IsNullOrEmpty(_pendingBulkFile))
                    {
                        _whatsappService.EnviarAnexo(_pendingBulkFile, msg);
                    }
                    else
                    {
                        _whatsappService.EnviarMensagem(num.Trim(), msg);
                    }

                    count++;

                    if (count < numbers.Length)
                    {
                        // Delay randômico entre as mensagens
                        int delay = rand.Next(delayMin * 1000, delayMax * 1000);
                        
                        // Pausa extra a cada 10 mensagens (Anti-Ban)
                        if (count % 10 == 0) {
                            Application.Current.Dispatcher.Invoke(() => BulkStatusLabel.Text = $"Pausa de segurança (Anti-Ban)...");
                            delay += rand.Next(10000, 20000); 
                        }

                        await Task.Delay(delay);
                    }
                }
                Application.Current.Dispatcher.Invoke(() => 
                {
                    BulkStatusLabel.Text = "✅ Disparo Stealth Finalizado!";
                    StartBulkBtn.IsEnabled = true;
                    ImportContactsBtn.IsEnabled = true;

                    if (restartBot)
                    {
                        AddLog("🔄 Reativando Bot de Atendimento...");
                        ToggleBot();
                    }
                });
            });
        }

        private void StopBulk_Click(object sender, RoutedEventArgs e)
        {
            _bulkCts?.Cancel();
            BulkStatusLabel.Text = "Parando...";
        }

        // --- GATILHOS ---
        private void PickTriggerFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = true;
            if (ofd.ShowDialog() == true)
            {
                _pendingTriggerFiles.AddRange(ofd.FileNames);
                TxtFileName.Text = $"{_pendingTriggerFiles.Count} arquivo(s) selecionado(s)";
            }
        }

        private void PickAiFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {
                _pendingAiFile = ofd.FileName;
                TxtAiFileName.Text = Path.GetFileName(_pendingAiFile);
                SaveSettings();
            }
        }

        private void PickBulkFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {
                _pendingBulkFile = ofd.FileName;
                TxtBulkFileName.Text = Path.GetFileName(_pendingBulkFile);
                SaveSettings();
            }
        }

        private void AddTrigger_Click(object sender, RoutedEventArgs e)
        {
            string cmd = TriggerCmdInput.Text.Trim();
            string resp = TriggerRespInput.Text.Trim();

            if (!string.IsNullOrEmpty(cmd) && !string.IsNullOrEmpty(resp))
            {
                // Removida a adição automática de '/' para permitir palavras-chave livres
                Gatilhos.Add(new GatilhoModel { 
                    Comando = cmd, 
                    Resposta = resp, 
                    CaminhosArquivos = new System.Collections.Generic.List<string>(_pendingTriggerFiles) 
                });
                
                SaveTriggers();
                
                TriggerCmdInput.Clear();
                TriggerRespInput.Clear();
                _pendingTriggerFiles.Clear();
                TxtFileName.Text = "Nenhum arquivo";
            }
            else
            {
                MessageBox.Show("Preencha o Comando e a Resposta.");
            }
        }

        private void SaveTriggers()
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BotZapAI", "triggers.json");
                string json = JsonSerializer.Serialize(Gatilhos, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                AddLog("Erro ao salvar gatilhos: " + ex.Message);
            }
        }

        private void LoadTriggers()
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BotZapAI", "triggers.json");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var list = JsonSerializer.Deserialize<System.Collections.Generic.List<GatilhoModel>>(json);
                    if (list != null)
                    {
                        Gatilhos.Clear();
                        foreach (var item in list) Gatilhos.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog("Erro ao carregar gatilhos: " + ex.Message);
            }
        }

        private void RemoveTrigger_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is GatilhoModel gatilho)
            {
                var result = MessageBox.Show($"Deseja realmente excluir o gatilho '{gatilho.Comando}'?", "Confirmar Exclusão", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    Gatilhos.Remove(gatilho);
                    SaveTriggers();
                }
            }
        }

        // --- CHAMADOS ---
        private void ResolveTicket_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ChamadoModel chamado)
            {
                ChamadosAbertos.Remove(chamado);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _whatsappService.Fechar();
            base.OnClosed(e);
        }
    }
}