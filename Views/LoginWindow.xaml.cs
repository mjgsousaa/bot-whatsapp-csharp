using System.Windows;
using BotWhatsappCSharp.Services;

namespace BotWhatsappCSharp.Views
{
    public partial class LoginWindow : Window
    {
        private readonly KeyManager _keyManager = new KeyManager();
        private readonly string _keyFilePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BotZapAI", "license.key");

        public LoginWindow()
        {
            InitializeComponent();
            CarregarChaveSalva();
        }

        private void CarregarChaveSalva()
        {
            if (System.IO.File.Exists(_keyFilePath))
            {
                try
                {
                    string savedKey = System.IO.File.ReadAllText(_keyFilePath).Trim();
                    KeyEntry.Text = savedKey;
                    
                    // Se a chave já estiver preenchida, vamos deixar a MsgLabel um pouco mais amigável
                    if (!string.IsNullOrEmpty(savedKey))
                    {
                        MsgLabel.Text = "Chave carregada! Clique em ENTRAR.";
                    }
                }
                catch { /* Ignorar erros de leitura */ }
            }
        }

        private async void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            string key = KeyEntry.Text.Trim();
            var result = _keyManager.ValidarChave(key);

            if (result.Valido)
            {
                MsgLabel.Text = "Validando licença...";
                MsgLabel.Foreground = System.Windows.Media.Brushes.Green;
                LoginBtn.IsEnabled = false;

                try
                {
                    // Garantir que a pasta existe
                    string? dir = System.IO.Path.GetDirectoryName(_keyFilePath);
                    if (dir != null) System.IO.Directory.CreateDirectory(dir);
                    
                    // Salvar chave localmente
                    System.IO.File.WriteAllText(_keyFilePath, key);

                    // Gera nome único da instância para esta licença
                    string instancia = KeyManager.GerarNomeInstancia(key);

                    // Salva instância no banco
                    var db = new DatabaseService();
                    db.SalvarConfiguracao("evo_instance", instancia);
                    db.SalvarConfiguracao("evo_base_url", KeyManager.EvoBaseUrl);
                    db.SalvarConfiguracao("evo_api_key",  KeyManager.EvoApiKey);
                    db.SalvarConfiguracao("plano", result.Plano);
                    db.SalvarConfiguracao("instancias_max", result.Plano switch {
                        "pro"     => "3",
                        "premium" => "10",
                        _         => "1"
                    });

                    // Verifica se já estava conectado
                    var evoSvc = new EvolutionSetupService();
                    string estado = await evoSvc.VerificarEstado(instancia);

                    if (estado != "open")
                    {
                        // Primeira vez ou desconectado — mostra QR
                        MsgLabel.Text = "Conectando WhatsApp...";
                        var qrWin = new QrCodeWindow(instancia);
                        qrWin.Owner = this;
                        bool conectou = qrWin.ShowDialog() == true;

                        if (!conectou)
                        {
                            // Usuário cancelou — permite entrar mesmo assim mas sem WhatsApp
                            var opcao = MessageBox.Show(
                                "WhatsApp não conectado.\nDeseja entrar mesmo assim?\nVocê poderá conectar depois pela aba Conexão.",
                                "Atenção", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                            if (opcao == MessageBoxResult.No)
                            {
                                LoginBtn.IsEnabled = true;
                                MsgLabel.Text = "Faça login para continuar.";
                                MsgLabel.Foreground = System.Windows.Media.Brushes.Gray;
                                return;
                            }
                        }
                    }

                    // Abrir Dashboard
                    MainWindow dashboard = new MainWindow();
                    dashboard.Show();
                    this.Close();
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("Erro ao iniciar: " + ex.Message, "Erro de Inicialização");
                    LoginBtn.IsEnabled = true;
                }
            }
            else
            {
                MsgLabel.Text = "Erro: " + result.Mensagem;
                MsgLabel.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void TermsBtn_Click(object sender, RoutedEventArgs e)
        {
            string termsPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "TERMOS_DE_USO.txt");
            if (System.IO.File.Exists(termsPath))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = termsPath,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    MessageBox.Show("Não foi possível abrir o arquivo de termos.", "Erro");
                }
            }
            else
            {
                MessageBox.Show("Arquivo TERMOS_DE_USO.txt não encontrado.", "Aviso");
            }
        }
    }
}
