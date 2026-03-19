using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BotWhatsappCSharp.Services;

namespace BotWhatsappCSharp.Views
{
    public partial class QrCodeWindow : Window
    {
        private readonly EvolutionSetupService _evoService;
        private readonly string _instancia;
        private CancellationTokenSource _cts;
        private bool _conectado = false;

        public bool Conectado => _conectado;

        public QrCodeWindow(string instancia)
        {
            InitializeComponent();
            _instancia = instancia;
            _evoService = new EvolutionSetupService();
            Loaded += async (s, e) => await IniciarFluxo();
        }

        private async Task IniciarFluxo()
        {
            _cts = new CancellationTokenSource();

            // Passo 1: garante que instância existe
            AtualizarStatus("Preparando instância...", "#F59E0B");
            bool criou = await _evoService.CriarInstanciaSeNecessario(_instancia);

            if (!criou)
            {
                AtualizarStatus("Erro ao criar instância. Verifique conexão.", "#EF4444");
                return;
            }

            // Passo 2: polling — busca QR ou detecta conexão
            await PollingConexao(_cts.Token);
        }

        private async Task PollingConexao(CancellationToken token)
        {
            int tentativas = 0;
            int qrFalhas = 0;
            int maxTentativas = 120; // 10 min (5s por tentativa)

            while (!token.IsCancellationRequested && tentativas < maxTentativas)
            {
                tentativas++;

                string estado = await _evoService.VerificarEstado(_instancia);

                if (estado == "open")
                {
                    _conectado = true;
                    await _evoService.ConfigurarWebhook(_instancia, 3001);

                    Dispatcher.Invoke(() =>
                    {
                        AtualizarStatus("✅ WhatsApp conectado com sucesso!", "#10B981");
                        ImgQrCode.Visibility = Visibility.Collapsed;
                        PnlLoading.Visibility = Visibility.Visible;
                        
                        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                        timer.Tick += (s, e) => { timer.Stop(); DialogResult = true; Close(); };
                        timer.Start();
                    });
                    return;
                }

                if (estado == "qr" || estado == "connecting" || estado == "error")
                {
                    string base64 = await _evoService.BuscarQrCode(_instancia);

                    if (!string.IsNullOrEmpty(base64))
                    {
                        qrFalhas = 0;
                        Dispatcher.Invoke(() => ExibirQrCode(base64));
                        AtualizarStatus("Aguardando escaneamento...", "#F59E0B");
                    }
                    else
                    {
                        qrFalhas++;
                        if (qrFalhas >= 3) // Se falhar 3x seguidas em gerar QR, reseta a instância
                        {
                            AtualizarStatus("🔄 Resetando instância para novo QR...", "#3B82F6");
                            await _evoService.ReiniciarInstancia(_instancia);
                            qrFalhas = 0;
                            await Task.Delay(2000, token);
                        }
                        else
                        {
                            AtualizarStatus("Gerando QR Code...", "#9CA3AF");
                        }
                    }
                }

                try { await Task.Delay(5000, token); } catch { break; }
            }

            if (!_conectado && !token.IsCancellationRequested)
                AtualizarStatus("Tempo limite. Tente novamente.", "#EF4444");
        }

        private void ExibirQrCode(string base64)
        {
            try
            {
                // Remove prefixo data:image/png;base64, se houver
                string b64 = base64.Contains(",") ? base64.Split(',')[1] : base64;

                byte[] bytes = Convert.FromBase64String(b64);
                using var ms = new MemoryStream(bytes);
                
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                ImgQrCode.Source = bitmap;
                ImgQrCode.Visibility = Visibility.Visible;
                PnlLoading.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Logger.Error("ExibirQrCode", ex);
            }
        }

        private void AtualizarStatus(string msg, string corHex)
        {
            Dispatcher.Invoke(() =>
            {
                TxtStatus.Text = msg;
                var cor = (Color)ColorConverter.ConvertFromString(corHex);
                StatusDot.Fill = new SolidColorBrush(cor);
            });
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            DialogResult = false;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _cts?.Cancel();
            base.OnClosed(e);
        }
    }
}
