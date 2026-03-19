using System;
using System.Windows;
using BotWhatsappCSharp.Services;

namespace BotWhatsappCSharp.Views
{
    public partial class EvolutionSetupWindow : Window
    {
        private readonly DatabaseService _db;

        public EvolutionSetupWindow(DatabaseService db)
        {
            InitializeComponent();
            _db = db;
            CarregarDados();
        }

        private void CarregarDados()
        {
            TxtBaseUrl.Text = _db.ObterConfiguracao("evo_base_url", "http://localhost:8080");
            TxtApiKey.Text = _db.ObterConfiguracao("evo_api_key", "");
            TxtInstance.Text = _db.ObterConfiguracao("evo_instance", "botzap");
            TxtWebhookPort.Text = _db.ObterConfiguracao("evo_webhook_port", "3001");
        }

        private void Salvar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtBaseUrl.Text) || string.IsNullOrWhiteSpace(TxtApiKey.Text))
            {
                StatusLabel.Text = "Preencha URL e API Key!";
                return;
            }

            _db.SalvarConfiguracao("evo_base_url", TxtBaseUrl.Text.Trim());
            _db.SalvarConfiguracao("evo_api_key", TxtApiKey.Text.Trim());
            _db.SalvarConfiguracao("evo_instance", TxtInstance.Text.Trim());
            _db.SalvarConfiguracao("evo_webhook_port", TxtWebhookPort.Text.Trim());

            DialogResult = true;
            Close();
        }
    }
}
