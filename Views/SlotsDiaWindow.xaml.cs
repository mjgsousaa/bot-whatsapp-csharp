using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using BotWhatsappCSharp.Models;
using BotWhatsappCSharp.Services;

namespace BotWhatsappCSharp.Views
{
    public partial class SlotsDiaWindow : Window
    {
        private readonly DateTime _data;
        private readonly DatabaseService _db;
        public ObservableCollection<SlotViewModel> Slots { get; set; } = new ObservableCollection<SlotViewModel>();

        public SlotsDiaWindow(DateTime data, DatabaseService db)
        {
            InitializeComponent();
            _data = data;
            _db = db;
            TxtData.Text = data.ToString("dd 'de' MMMM, yyyy", new System.Globalization.CultureInfo("pt-BR"));

            LoadSlots();
            ItemsSlots.ItemsSource = Slots;
        }

        private void LoadSlots()
        {
            var slotsBanco = _db.ListarSlotsMes(_data.Year, _data.Month)
                                .Where(s => s.DataHora.Date == _data.Date)
                                .ToList();

            // Horários padrão de 8h às 18h
            for (int h = 8; h <= 18; h++)
            {
                var dt = _data.Date.AddHours(h);
                var slotExistente = slotsBanco.FirstOrDefault(s => s.DataHora == dt);

                var vm = new SlotViewModel
                {
                    DataHora = dt,
                    TimeDisplay = $"{h:D2}:00",
                    IsAvailable = slotExistente?.Disponivel ?? false,
                    IsBooked = slotExistente != null && !slotExistente.Disponivel && !string.IsNullOrEmpty(slotExistente.AgendadoPor),
                    AgendadoPor = slotExistente?.AgendadoPor ?? "",
                    Telefone = slotExistente?.TelefoneCliente ?? ""
                };
                Slots.Add(vm);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            foreach (var slot in Slots)
            {
                // Só atualiza se mudou o estado ou se for novo marcado como disponível
                // Para simplificar, o ToggleSlot do DatabaseService lida com insert/update
                // Mas aqui precisamos garantir que o estado final seja o que está na UI
                // Vamos ajustar o DatabaseService ou chamar conforme a necessidade.
                
                // Como o ToggleSlot apenas inverte, vamos implementar uma lógica de "SetSlot" no DatabaseService futuramente
                // Por enquanto, vamos usar o ToggleSlot de forma controlada comparando com o banco
                
                var atualNoBanco = _db.ListarSlotsMes(_data.Year, _data.Month)
                                     .FirstOrDefault(s => s.DataHora == slot.DataHora);
                
                bool disponivelNoBanco = atualNoBanco?.Disponivel ?? false;
                
                if (slot.IsAvailable != disponivelNoBanco)
                {
                    _db.ToggleSlot(slot.DataHora);
                }
            }
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }

    public class SlotViewModel
    {
        public DateTime DataHora { get; set; }
        public string TimeDisplay { get; set; } = "";
        public bool IsAvailable { get; set; }
        public bool IsBooked { get; set; }
        public string AgendadoPor { get; set; } = "";
        public string Telefone { get; set; } = "";

        public Visibility IsBookedVisible => IsBooked ? Visibility.Visible : Visibility.Collapsed;
        public string StatusInfo => IsBooked ? $"Agendado para: {AgendadoPor} ({Telefone})" : (IsAvailable ? "Disponível para o bot" : "Não ofertado pelo bot");
        public Brush StatusColor => IsBooked ? Brushes.Red : (IsAvailable ? Brushes.Green : Brushes.Gray);
    }
}
