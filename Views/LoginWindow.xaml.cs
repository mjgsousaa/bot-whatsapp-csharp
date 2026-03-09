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

        private void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            string key = KeyEntry.Text.Trim();
            var result = _keyManager.ValidarChave(key);

            if (result.Valido)
            {
                MsgLabel.Text = "Sucesso! Carregando...";
                MsgLabel.Foreground = System.Windows.Media.Brushes.Green;

                try
                {
                    // Garantir que a pasta existe
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_keyFilePath));
                    
                    // Salvar chave localmente
                    System.IO.File.WriteAllText(_keyFilePath, key);

                    // Abrir Dashboard
                    MainWindow dashboard = new MainWindow();
                    dashboard.Show();
                    this.Close();
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("Erro ao abrir a tela principal: " + ex.Message + "\n" + ex.StackTrace, "Erro de Inicialização");
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
