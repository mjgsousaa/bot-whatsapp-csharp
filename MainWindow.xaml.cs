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
using BotWhatsappCSharp.Views;
using System.Collections.Generic;
using System.Windows.Input;

namespace BotWhatsappCSharp
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly DatabaseService _databaseService;
        private readonly WhatsappService _whatsappService;
        private readonly SchedulingService _schedulingService;
        private readonly WebhookServer _webhookServer;
        private AiAssistant? _aiAssistant; 
        private bool _isConnecting = false;
        private bool _botLoopActive = false;
        
        public ObservableCollection<GatilhoModel> Gatilhos { get; set; } = new ObservableCollection<GatilhoModel>();
        public ObservableCollection<ChamadoModel> ChamadosAbertos { get; set; } = new ObservableCollection<ChamadoModel>();
        public ObservableCollection<AgendamentoModel> Agendamentos { get; set; } = new ObservableCollection<AgendamentoModel>();

        private List<string> _pendingTriggerFiles = new List<string>();
        private string _pendingAiFile = "";
        private string _bulkMidiaPath = "";
        private TipoMidia _bulkTipoMidia = TipoMidia.Nenhum;
        private bool _temNovoChamado = false;
        private readonly Dictionary<string, string> _ultimaMensagemRespondida = new Dictionary<string, string>();
        
        private DateTime _mesAtual = DateTime.Today;
        private List<SlotConfigurado> _slotsDoMes = new();
        
        public bool TemNovoChamado 
        { 
            get => _temNovoChamado; 
            set { _temNovoChamado = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            
            _databaseService = new DatabaseService();

            string baseUrl = _databaseService.ObterConfiguracao("evo_base_url", "https://evolutionapi.vps7841.panel.icontainer.cloud");
            string apiKey = _databaseService.ObterConfiguracao("evo_api_key", "wFRfS5EhXJkbC6FKJi7fDYDsFwsbWkYD");
            string instance = _databaseService.ObterConfiguracao("evo_instance", "botzap");
            string webhookPortStr = _databaseService.ObterConfiguracao("evo_webhook_port", "3001");
            int webhookPort = int.TryParse(webhookPortStr, out int p) ? p : 3001;
            AddLog($"[SISTEMA] Iniciando Webhook na porta: {webhookPort}");

            _whatsappService = new WhatsappService(baseUrl, apiKey, instance);
            _schedulingService = new SchedulingService(_databaseService);
            
            _webhookServer = new WebhookServer(webhookPort, (numero, texto, audio, ctx, nome) => 
            {
                if (!_botLoopActive) return;

                _whatsappService.ProcessarMensagemWebhook(numero, texto, audio, ctx, (n, m, audioData, c) =>
                {
                    try
                    {
                        string mensagemRecebida = m;
                        if (audioData != null && _aiAssistant != null)
                        {
                            AddLog("Transcrevendo áudio recebido...");
                            mensagemRecebida = _aiAssistant.TranscreverAudioAsync(audioData).GetAwaiter().GetResult();
                            AddLog($"[TRANSCRICAO] {mensagemRecebida}");
                        }

                        if (string.IsNullOrWhiteSpace(mensagemRecebida)) return ("", "");

                        string msgHash = $"{n}_{mensagemRecebida?.Trim().GetHashCode()}";
                        if (_ultimaMensagemRespondida.TryGetValue(n, out string? ultimoHash) && ultimoHash == msgHash) return ("", "");
                        _ultimaMensagemRespondida[n] = msgHash;

                        AddLog($"[{n}] {mensagemRecebida}");
                        
                        string[] termosAtendente = { "atendente", "humano", "pessoa", "falar com alguém", "atendimento", "suporte", "ajuda", "vendedor" };
                        if (termosAtendente.Any(t => mensagemRecebida.ToLower().Contains(t)))
                        {
                            AddLog($"Pedido de atendente detectado de {n}!");
                            Application.Current.Dispatcher.Invoke(() => {
                                if (!ChamadosAbertos.Any(cham => cham.Numero == n)) {
                                    ChamadosAbertos.Add(new ChamadoModel { 
                                        Numero = n, Nome = nome, UltimaMensagem = mensagemRecebida, Horario = DateTime.Now
                                    });
                                    TemNovoChamado = true;
                                }
                            });
                            return ("Um atendente humano foi notificado. Aguarde um momento.", "");
                        }

                        string msgLimpa = mensagemRecebida.Trim().ToLower();
                        var gatilho = Gatilhos.FirstOrDefault(g => {
                            string cmd = g.Comando.Trim().ToLower();
                            if (cmd.StartsWith("/")) {
                                // Comandos com barra: correspondência exata ou início (ex: /ajuda agora)
                                return msgLimpa == cmd || msgLimpa.StartsWith(cmd + " ");
                            }
                            // Palavras-chave: se a mensagem contém o termo
                            return msgLimpa.Contains(cmd);
                        });
                        
                        if (gatilho != null)
                        {
                            AddLog($"[GATILHO ATIVADO] Comando: {gatilho.Comando} para {n}");
                            
                            if (!string.IsNullOrWhiteSpace(gatilho.Resposta) && !gatilho.TemAnexo)
                            {
                                _whatsappService.EnviarMensagem(n, gatilho.Resposta);
                            }

                            if (gatilho.TemAnexo)
                            {
                                for (int i = 0; i < gatilho.CaminhosArquivos.Count; i++)
                                {
                                    string legenda = (i == 0) ? gatilho.Resposta : "";
                                    _whatsappService.EnviarAnexoGatilho(n, gatilho.CaminhosArquivos[i], legenda, gatilho.TipoMidiaAnexo);
                                    if (gatilho.CaminhosArquivos.Count > 1) Thread.Sleep(1000); 
                                }
                            }
                            return ("", "");
                        }

                        AddLog($"[IA] Sem gatilho para: '{msgLimpa}'. Enviando para Groq...");

                        if (_aiAssistant == null) return ("", "");

                        string aiContext = "";
                        if (c == "AGENDAMENTO_TRIGGER")
                        {
                            var agora = DateTime.Now;
                            var slots = _databaseService.GetHorariosDisponiveisMes(agora.Year, agora.Month)
                                .Concat(_databaseService.GetHorariosDisponiveisMes(agora.AddMonths(1).Year, agora.AddMonths(1).Month))
                                .Where(s => s > agora)
                                .Take(8)
                                .ToList();

                            var fmt = slots.Select(s => $"• {s:dddd, dd/MM} às {s:HH:mm}").ToList();

                            aiContext = "O cliente quer agendar. HORÁRIOS DISPONÍVEIS:\n" +
                                        string.Join("\n", fmt) +
                                        "\n\nSe o cliente confirmar um horário, responda estritamente com:\n" +
                                        "[AGENDAR|nome:NOME|horario:YYYY-MM-DD HH:mm|servico:CONSULTA]";
                        }

                        AddLog("Consultando IA...");
                        string respostaIA = _aiAssistant.GerarRespostaAsync(mensagemRecebida, aiContext, n).GetAwaiter().GetResult();
                        return (respostaIA, _pendingAiFile);
                    }
                    catch (Exception ex) { AddLog($"Erro no Webhook Processor: {ex.Message}"); return ("", ""); }
                }, AddLog);
            }, AddLog);

            LoadSettings();
            LoadTriggers();
            _mesAtual = DateTime.Today;
            SelectView("Connection");
        }

        private void LoadAgendamentos()
        {
            Agendamentos.Clear();
            foreach (var a in _schedulingService.ListarAgendamentos()) Agendamentos.Add(a);
        }

        private async void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isConnecting) return;
            _isConnecting = true;
            StartBtn.IsEnabled = false;
            StartBtn.Content = "CONECTANDO...";

            await _whatsappService.IniciarAsync(status =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusLabel.Text = "Status: " + status;
                    if (status.Contains("Conectado"))
                    {
                        _whatsappService.SetConectado(true);
                        StartBtn.Content = "CONECTADO À API";
                        StartBtn.Background = (Brush)FindResource("SuccessGreen");
                        StartBtn.IsEnabled = true;
                        _isConnecting = false;
                        
                        if (!_botLoopActive) ToggleBot(); 
                    }
                    else if (status.Contains("Erro") || status.Contains("limite"))
                    {
                        StartBtn.Content = "TENTAR NOVAMENTE";
                        StartBtn.Background = (Brush)FindResource("DangerRed");
                        StartBtn.IsEnabled = true;
                        _isConnecting = false;
                    }
                });
            });
        }

        private void NavEvoSetup_Click(object sender, RoutedEventArgs e)
        {
            var win = new Views.EvolutionSetupWindow(_databaseService);
            if (win.ShowDialog() == true)
            {
                MessageBox.Show("Configurações salvas! Reinicie o app para aplicar.");
            }
        }

        private void ToggleBot()
        {
            _botLoopActive = !_botLoopActive;
            Application.Current.Dispatcher.Invoke(() => {
                if (_botLoopActive)
                {
                    AiToggleBtn.Content = "PARAR ATENDIMENTO";
                    AiToggleBtn.Background = (Brush)FindResource("DangerRed");
                    AiStatusLabel.Text = "STATUS: ATIVO E ESCUTANDO WEBHOOK";
                    AiStatusLabel.Foreground = (Brush)FindResource("SuccessGreen");
                    AddLog("Atendimento inteligente ativado.");
                }
                else
                {
                    AiToggleBtn.Content = "LIGAR ATENDIMENTO AUTOMÁTICO";
                    AiToggleBtn.Background = (Brush)FindResource("BrandYellow");
                    AiStatusLabel.Text = "STATUS: PARADO";
                    AiStatusLabel.Foreground = (Brush)FindResource("DangerRed");
                    AddLog("Atendimento inteligente desativado.");
                }

                if (_botLoopActive && !_webhookServer.IsAtivo())
                {
                    _webhookServer.Iniciar();
                }
            });
        }

        private void AiToggle_Click(object sender, RoutedEventArgs e)
        {
            if (!_whatsappService.IsDriverAtivo()) { MessageBox.Show("Conecte à Evolution API primeiro."); return; }
            ToggleBot();
        }

        private void SelectView(string viewName)
        {
            BtnNavConnect.Background = Brushes.Transparent; BtnNavConnect.Foreground = (Brush)FindResource("TextGray");
            BtnNavAI.Background = Brushes.Transparent; BtnNavAI.Foreground = (Brush)FindResource("TextGray");
            BtnNavBulk.Background = Brushes.Transparent; BtnNavBulk.Foreground = (Brush)FindResource("TextGray");
            BtnNavTriggers.Background = Brushes.Transparent; BtnNavTriggers.Foreground = (Brush)FindResource("TextGray");
            BtnNavTickets.Background = Brushes.Transparent; BtnNavTickets.Foreground = (Brush)FindResource("TextGray");
            BtnNavEvoSetup.Background = Brushes.Transparent; BtnNavEvoSetup.Foreground = (Brush)FindResource("TextGray");
            if (BtnNavSchedule != null) { BtnNavSchedule.Background = Brushes.Transparent; BtnNavSchedule.Foreground = (Brush)FindResource("TextGray"); }

            ViewConnection.Visibility = Visibility.Collapsed;
            ViewAI.Visibility = Visibility.Collapsed;
            ViewBulk.Visibility = Visibility.Collapsed;
            ViewTriggers.Visibility = Visibility.Collapsed;
            ViewTickets.Visibility = Visibility.Collapsed;
            if (ViewSchedule != null) ViewSchedule.Visibility = Visibility.Collapsed;

            switch (viewName)
            {
                case "Connection": ViewConnection.Visibility = Visibility.Visible; BtnNavConnect.Background = (Brush)FindResource("BorderColor"); break;
                case "AI": ViewAI.Visibility = Visibility.Visible; BtnNavAI.Background = (Brush)FindResource("BorderColor"); break;
                case "Bulk": ViewBulk.Visibility = Visibility.Visible; BtnNavBulk.Background = (Brush)FindResource("BorderColor"); break;
                case "Triggers": ViewTriggers.Visibility = Visibility.Visible; BtnNavTriggers.Background = (Brush)FindResource("BorderColor"); break;
                case "Tickets": ViewTickets.Visibility = Visibility.Visible; BtnNavTickets.Background = (Brush)FindResource("BorderColor"); break;
                case "Schedule": ViewSchedule.Visibility = Visibility.Visible; BtnNavSchedule.Background = (Brush)FindResource("BorderColor"); RenderizarCalendario(); break;
                case "Evo": BtnNavEvoSetup.Background = (Brush)FindResource("BorderColor"); break;
            }
        }

        private void NavConnect_Click(object sender, RoutedEventArgs e) => SelectView("Connection");
        private void NavAI_Click(object sender, RoutedEventArgs e) => SelectView("AI");
        private void NavBulk_Click(object sender, RoutedEventArgs e) => SelectView("Bulk");
        private void NavTriggers_Click(object sender, RoutedEventArgs e) => SelectView("Triggers");
        private void NavSchedule_Click(object sender, RoutedEventArgs e) => SelectView("Schedule");
        private void NavTickets_Click(object sender, RoutedEventArgs e) { SelectView("Tickets"); TemNovoChamado = false; }

        private void AddLog(string msg) { Application.Current.Dispatcher.Invoke(() => { LogList.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}"); if (LogList.Items.Count > 100) LogList.Items.RemoveAt(100); }); Logger.Log(msg); }

        private void LoadSettings()
        {
            var settings = _databaseService.CarregarSettings();
            ApiKeyInput.Text = settings.ApiKey; SystemPromptInput.Text = settings.SystemPrompt;
            _pendingAiFile = settings.AiMediaFile ?? ""; TxtAiFileName.Text = string.IsNullOrEmpty(_pendingAiFile) ? "Nenhum arquivo" : Path.GetFileName(_pendingAiFile);
            _aiAssistant = new AiAssistant(settings.ApiKey, settings.SystemPrompt, _databaseService);
        }

        private void SaveAI_Click(object sender, RoutedEventArgs e) { _aiAssistant = new AiAssistant(ApiKeyInput.Text.Trim(), SystemPromptInput.Text.Trim(), _databaseService); SaveSettings(); MessageBox.Show("Configurações salvas!"); }
        private void SaveSettings() { try { var settings = new SettingsModel { ApiKey = ApiKeyInput.Text, SystemPrompt = SystemPromptInput.Text, AiMediaFile = _pendingAiFile }; _databaseService.SalvarSettings(settings); } catch { } }

        private void PickTriggerFile_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { Multiselect = true };
            int tipo = CmbTipoMidia.SelectedIndex;
            ofd.Filter = tipo switch {
                1 => "Imagens|*.jpg;*.jpeg;*.png;*.gif;*.webp",
                2 => "PDF|*.pdf",
                3 => "Áudio|*.mp3;*.ogg;*.opus;*.aac",
                4 => "Vídeo|*.mp4;*.avi;*.mov",
                _ => "Todos os arquivos|*.*"
            };
            
            if (ofd.ShowDialog() == true)
            {
                _pendingTriggerFiles.Clear();
                _pendingTriggerFiles.AddRange(ofd.FileNames);
                TxtFileName.Text = _pendingTriggerFiles.Count == 1 ? Path.GetFileName(_pendingTriggerFiles[0]) : $"{_pendingTriggerFiles.Count} arquivo(s)";
            }
        }

        private void PickAiFile_Click(object sender, RoutedEventArgs e) { OpenFileDialog ofd = new OpenFileDialog(); if (ofd.ShowDialog() == true) { _pendingAiFile = ofd.FileName; TxtAiFileName.Text = Path.GetFileName(_pendingAiFile); SaveSettings(); } }
        
        private void AddTrigger_Click(object sender, RoutedEventArgs e) 
        { 
            string cmd = TriggerCmdInput.Text;
            string resp = TriggerRespInput.Text;
            var novo = new GatilhoModel { 
                Comando = cmd, 
                Resposta = resp, 
                CaminhosArquivos = new List<string>(_pendingTriggerFiles),
                TipoMidiaAnexo = CmbTipoMidia.SelectedIndex switch {
                    1 => TipoMidia.Imagem,
                    2 => TipoMidia.PDF,
                    3 => TipoMidia.Audio,
                    4 => TipoMidia.Video,
                    _ => TipoMidia.Nenhum
                }
            }; 
            if (_databaseService.SalvarGatilho(novo)) { 
                Gatilhos.Add(novo); 
                TriggerCmdInput.Clear(); 
                TriggerRespInput.Clear(); 
                _pendingTriggerFiles.Clear(); 
                TxtFileName.Text = "Nenhum arquivo"; 
                CmbTipoMidia.SelectedIndex = 0;
            } 
        }

        private void LoadTriggers() { var list = _databaseService.ListarGatilhos(); Gatilhos.Clear(); foreach (var item in list) Gatilhos.Add(item); }
        private void RemoveTrigger_Click(object sender, RoutedEventArgs e) { if (sender is Button btn && btn.DataContext is GatilhoModel g) { if (MessageBox.Show($"Excluir '{g.Comando}'?", "Confirmação", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { if (_databaseService.RemoverGatilho(g.Comando)) Gatilhos.Remove(g); } } }

        public void ResponderTicket_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            string numero = btn.Tag?.ToString() ?? "";
            if (string.IsNullOrEmpty(numero)) return;

            var parent = btn.Parent as Grid;
            if (parent == null) return;
            
            var txtBox = parent.Children.OfType<TextBox>().FirstOrDefault();
            string mensagem = txtBox?.Text.Trim() ?? "";

            if (string.IsNullOrEmpty(mensagem)) { MessageBox.Show("Digite uma mensagem para enviar."); return; }
            if (!_whatsappService.IsDriverAtivo()) { MessageBox.Show("WhatsApp não está conectado."); return; }

            try
            {
                _whatsappService.EnviarMensagem(numero, mensagem);
                AddLog($"[ATENDENTE] Respondeu {numero}: {mensagem.Substring(0, Math.Min(30, mensagem.Length))}...");
                if (txtBox != null) txtBox.Clear();
                var chamado = ChamadosAbertos.FirstOrDefault(c => c.Numero == numero);
                if (chamado != null) chamado.Respondido = true;
            }
            catch (Exception ex) { MessageBox.Show($"Erro ao enviar: {ex.Message}"); }
        }

        private void ResolveTicket_Click(object sender, RoutedEventArgs e) 
        { 
            if (sender is not Button btn) return;
            string numero = btn.Tag?.ToString() ?? "";
            var chamado = ChamadosAbertos.FirstOrDefault(c => c.Numero == numero);
            if (chamado == null) return;

            var result = MessageBox.Show($"Concluir atendimento de {chamado.Nome} ({numero})?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                ChamadosAbertos.Remove(chamado);
                AddLog($"[TICKET] Atendimento de {numero} concluído.");
                if (ChamadosAbertos.Count == 0) TemNovoChamado = false;
            }
        }

        private void ImportContacts_Click(object sender, RoutedEventArgs e) 
        { 
            try {
                OpenFileDialog ofd = new OpenFileDialog { Filter = "Arquivos de Texto (*.txt;*.csv)|*.txt;*.csv" }; 
                if (ofd.ShowDialog() == true) { BulkNumbersInput.Text = File.ReadAllText(ofd.FileName); } 
            } catch (Exception ex) { MessageBox.Show("Erro ao importar contatos: " + ex.Message); }
        }

        private void DownloadTemplate_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Formato esperado: Um número de telefone por linha.\nExemplo:\n5511999999999\n5511888888888");
        }

        private CancellationTokenSource? _bulkCts;

        private void BtnBulkImagem_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { Filter = "Imagens|*.jpg;*.jpeg;*.png;*.gif;*.webp", Title = "Selecionar imagem da campanha" };
            if (ofd.ShowDialog() == true) { _bulkMidiaPath = ofd.FileName; _bulkTipoMidia = TipoMidia.Imagem; AtualizarBulkMidiaUI(); }
        }

        private void BtnBulkPDF_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { Filter = "PDF|*.pdf", Title = "Selecionar PDF da campanha" };
            if (ofd.ShowDialog() == true) { _bulkMidiaPath = ofd.FileName; _bulkTipoMidia = TipoMidia.PDF; AtualizarBulkMidiaUI(); }
        }

        private void BtnBulkAudio_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { Filter = "Áudio|*.mp3;*.ogg;*.opus;*.aac", Title = "Selecionar áudio da campanha" };
            if (ofd.ShowDialog() == true) { _bulkMidiaPath = ofd.FileName; _bulkTipoMidia = TipoMidia.Audio; AtualizarBulkMidiaUI(); }
        }

        private void BtnBulkRemoverMidia_Click(object sender, RoutedEventArgs e)
        {
            _bulkMidiaPath = ""; _bulkTipoMidia = TipoMidia.Nenhum; TxtBulkMidiaInfo.Text = "Nenhuma mídia selecionada";
            TxtBulkMidiaInfo.FontStyle = FontStyles.Italic; BtnBulkRemoverMidia.Visibility = Visibility.Collapsed;
        }

        private void AtualizarBulkMidiaUI()
        {
            string icone = _bulkTipoMidia switch { TipoMidia.Imagem => "🖼️", TipoMidia.PDF => "📄", TipoMidia.Audio => "🎵", _ => "📎" };
            string nome = Path.GetFileName(_bulkMidiaPath);
            long tamanho = new FileInfo(_bulkMidiaPath).Length / 1024;
            TxtBulkMidiaInfo.Text = $"{icone} {nome} ({tamanho} KB)";
            TxtBulkMidiaInfo.FontStyle = FontStyles.Normal; BtnBulkRemoverMidia.Visibility = Visibility.Visible;
        }

        private async void StartBulk_Click(object sender, RoutedEventArgs e)
        {
            string msg = BulkMessageInput.Text.Trim();
            string[] numbers = BulkNumbersInput.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (numbers.Length == 0) { MessageBox.Show("Adicione pelo menos um número."); return; }
            if (string.IsNullOrWhiteSpace(msg) && string.IsNullOrEmpty(_bulkMidiaPath)) { MessageBox.Show("Preencha a mensagem ou selecione uma mídia."); return; }

            if (!int.TryParse(BulkDelayMin.Text, out int delayMin)) delayMin = 5;
            if (!int.TryParse(BulkDelayMax.Text, out int delayMax)) delayMax = 15;

            bool restartBot = false;
            if (_botLoopActive) { AddLog("Pausando atendimento para disparo..."); ToggleBot(); restartBot = true; await Task.Delay(2000); }

            StartBulkBtn.IsEnabled = false;
            BulkProgressBar.Maximum = numbers.Length;
            BulkProgressBar.Value = 0;
            _bulkCts = new CancellationTokenSource();

            await Task.Run(async () =>
            {
                int count = 0;
                var rand = new Random();
                foreach (var numero in numbers)
                {
                    if (_bulkCts.Token.IsCancellationRequested) break;
                    string num = numero.Trim();
                    if (string.IsNullOrEmpty(num)) continue;

                    Application.Current.Dispatcher.Invoke(() => {
                        BulkStatusLabel.Text = $"Enviando {count + 1}/{numbers.Length} → {num}";
                        BulkProgressBar.Value = count;
                        BulkProgressLabel.Text = $"{count + 1} de {numbers.Length} ({(int)((count + 1.0) / numbers.Length * 100)}%)";
                    });

                    try
                    {
                        if (!string.IsNullOrEmpty(_bulkMidiaPath)) _whatsappService.EnviarAnexoGatilho(num, _bulkMidiaPath, msg, _bulkTipoMidia);
                        else _whatsappService.EnviarMensagem(num, msg);
                        count++;
                    }
                    catch (Exception ex) { AddLog($"Erro ao enviar para {num}: {ex.Message}"); }

                    if (count < numbers.Length)
                    {
                        int delay = rand.Next(delayMin * 1000, delayMax * 1000);
                        if (count % 10 == 0) { Application.Current.Dispatcher.Invoke(() => BulkStatusLabel.Text = "Pausa de segurança anti-ban..."); delay += rand.Next(10000, 20000); }
                        await Task.Delay(delay, _bulkCts.Token).ContinueWith(_ => { });
                    }
                }

                Application.Current.Dispatcher.Invoke(() => {
                    BulkStatusLabel.Text = $"✅ Concluído! {count}/{numbers.Length} enviados.";
                    BulkProgressBar.Value = numbers.Length;
                    BulkProgressLabel.Text = "100% concluído";
                    StartBulkBtn.IsEnabled = true;
                    if (restartBot) { AddLog("Reativando atendimento automático..."); ToggleBot(); }
                });
            }, _bulkCts.Token);
        }

        private void StopBulk_Click(object sender, RoutedEventArgs e) => _bulkCts?.Cancel();

        // --- LÓGICA DO CALENDÁRIO ---
        private void RenderizarCalendario()
        {
            if (CalendarioGrid == null) return;
            CalendarioGrid.Children.Clear();

            // Atualiza label do mês
            TxtMesAtual.Text = _mesAtual.ToString("MMMM yyyy", new System.Globalization.CultureInfo("pt-BR")).ToUpper();

            // Cabeçalho dos dias da semana
            string[] dias = { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb" };
            foreach (var d in dias)
            {
                CalendarioGrid.Children.Add(new TextBlock
                {
                    Text = d,
                    FontWeight = FontWeights.Bold,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8),
                    Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128))
                });
            }

            // Carrega slots do banco
            _slotsDoMes = _databaseService.ListarSlotsMes(_mesAtual.Year, _mesAtual.Month);

            // Primeiro dia do mês e deslocamento
            var primeiroDia = new DateTime(_mesAtual.Year, _mesAtual.Month, 1);
            int offsetInicio = (int)primeiroDia.DayOfWeek;

            // Células vazias antes do dia 1
            for (int i = 0; i < offsetInicio; i++)
                CalendarioGrid.Children.Add(new Border());

            // Dias do mês
            int totalDias = DateTime.DaysInMonth(_mesAtual.Year, _mesAtual.Month);

            for (int dia = 1; dia <= totalDias; dia++)
            {
                var dataAtual = new DateTime(_mesAtual.Year, _mesAtual.Month, dia);
                bool isPast = dataAtual.Date < DateTime.Today;
                bool isHoje = dataAtual.Date == DateTime.Today;

                // Conta slots do dia
                var slotsDia = _slotsDoMes.Where(s => s.DataHora.Date == dataAtual.Date).ToList();
                int disponiveis = slotsDia.Count(s => s.Disponivel);
                int agendados = slotsDia.Count(s => !s.Disponivel && !string.IsNullOrEmpty(s.AgendadoPor));

                // Cor do card do dia
                string bgHex = "#F9FAFB"; // Padrão vazio
                if (agendados > 0 && disponiveis == 0)
                    bgHex = "#FEE2E2"; // Vermelho — Tudo agendado
                else if (disponiveis > 0)
                    bgHex = "#D1FAE5"; // Verde — Tem disponível
                else if (agendados > 0)
                    bgHex = "#FEF3C7"; // Amarelo — Misto/Agendado s/ disponibilidade aberta

                var cardDia = new Border
                {
                    Margin = new Thickness(2),
                    Padding = new Thickness(4),
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex)),
                    BorderThickness = new Thickness(isHoje ? 2 : 0.5),
                    BorderBrush = new SolidColorBrush(isHoje ? Color.FromRgb(37, 99, 235) : Color.FromRgb(229, 231, 235)),
                    Cursor = isPast ? Cursors.Arrow : Cursors.Hand,
                    Tag = dataAtual
                };

                var stackDia = new StackPanel();
                stackDia.Children.Add(new TextBlock
                {
                    Text = dia.ToString(),
                    FontWeight = isHoje ? FontWeights.Bold : FontWeights.Normal,
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = new SolidColorBrush(isPast ? Color.FromRgb(156, 163, 175) : Color.FromRgb(17, 24, 39))
                });

                if (slotsDia.Count > 0)
                {
                    stackDia.Children.Add(new TextBlock
                    {
                        Text = $"{disponiveis}✓ {agendados}●",
                        FontSize = 9,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                        Margin = new Thickness(0, 2, 0, 0)
                    });
                }

                cardDia.Child = stackDia;

                if (!isPast)
                {
                    cardDia.MouseLeftButtonUp += (s, e) => AbrirSlotsDia((DateTime)((Border)s!).Tag);
                }

                CalendarioGrid.Children.Add(cardDia);
            }
        }

        private void AbrirSlotsDia(DateTime data)
        {
            var win = new Views.SlotsDiaWindow(data, _databaseService);
            win.Owner = this;
            if (win.ShowDialog() == true) RenderizarCalendario();
        }

        private void BtnMesAnterior_Click(object sender, RoutedEventArgs e)
        {
            _mesAtual = _mesAtual.AddMonths(-1);
            RenderizarCalendario();
        }

        private void BtnProximoMes_Click(object sender, RoutedEventArgs e)
        {
            _mesAtual = _mesAtual.AddMonths(1);
            RenderizarCalendario();
        }

        private void ReconectarWA_Click(object sender, RoutedEventArgs e)
        {
            string instancia = _databaseService.ObterConfiguracao("evo_instance", "");

            if (string.IsNullOrEmpty(instancia))
            {
                MessageBox.Show("Instância não configurada. Faça logout e entre novamente.");
                return;
            }

            var qrWin = new QrCodeWindow(instancia);
            qrWin.Owner = this;

            if (qrWin.ShowDialog() == true)
            {
                _whatsappService.SetConectado(true);
                StatusLabel.Text = "✅ WhatsApp Conectado";
                StartBtn.Content = "BOT LIGADO";
                StartBtn.Background = (Brush)FindResource("SuccessGreen");
                AddLog("WhatsApp reconectado com sucesso.");
            }
        }

        private void ClearSession_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Use o painel da Evolution API para gerenciar sessões.");
        protected override void OnClosed(EventArgs e) { _bulkCts?.Cancel(); _webhookServer?.Parar(); _whatsappService?.Fechar(); base.OnClosed(e); }
    }

    // --- Conversores de Status para Tickets ---

    public class BooleanToStatusBrushConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool respondido && respondido)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1FAE5")); // Verde claro
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7")); // Amarelo claro
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }

    public class BooleanToStatusTextConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (value is bool respondido && respondido) ? "Respondido" : "Aguardando";
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }

    public class BooleanToStatusForegroundConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool respondido && respondido)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#065F46")); // Verde escuro
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#92400E")); // Amarelo escuro
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }
}