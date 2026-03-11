using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BotWhatsappCSharp.Models;

namespace BotWhatsappCSharp.Services
{
    public class SchedulingService
    {
        private readonly string _dbPath;
        private List<AgendamentoModel> _agendamentos = new List<AgendamentoModel>();
        private List<SlotDisponivel> _slotsConfigurados = new List<SlotDisponivel>();

        public SchedulingService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _dbPath = Path.Combine(appData, "BotZapAI", "agendamentos.json");
            LoadData();
            
            // Slots padrão se não houver configuração (Seg-Sex, 09:00-17:00)
            if (_slotsConfigurados.Count == 0)
            {
                for (int i = 1; i <= 5; i++)
                {
                    _slotsConfigurados.Add(new SlotDisponivel { 
                        DiaSemana = (DayOfWeek)i, 
                        HoraInicio = new TimeSpan(9, 0, 0), 
                        HoraFim = new TimeSpan(17, 0, 0) 
                    });
                }
            }
        }

        private void LoadData()
        {
            try
            {
                if (File.Exists(_dbPath))
                {
                    string json = File.ReadAllText(_dbPath);
                    var data = JsonSerializer.Deserialize<SchedulingData>(json);
                    if (data != null)
                    {
                        _agendamentos = data.Agendamentos ?? new List<AgendamentoModel>();
                        _slotsConfigurados = data.Slots ?? new List<SlotDisponivel>();
                    }
                }
            }
            catch { }
        }

        public void SaveData()
        {
            try
            {
                var data = new SchedulingData { Agendamentos = _agendamentos, Slots = _slotsConfigurados };
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_dbPath, json);
            }
            catch { }
        }

        public List<DateTime> GetHorariosDisponiveis(DateTime data)
        {
            var slotsDoDia = _slotsConfigurados.Where(s => s.DiaSemana == data.DayOfWeek).ToList();
            var horarios = new List<DateTime>();

            foreach (var slot in slotsDoDia)
            {
                DateTime atual = data.Date.Add(slot.HoraInicio);
                DateTime fim = data.Date.Add(slot.HoraFim);

                while (atual < fim)
                {
                    // Verifica se já existe agendamento nesse horário (intervalo de 1 hora por padrão)
                    bool ocupado = _agendamentos.Any(a => a.DataHora == atual && a.Status == "Confirmado");
                    if (!ocupado && atual > DateTime.Now)
                    {
                        horarios.Add(atual);
                    }
                    atual = atual.AddHours(1);
                }
            }
            return horarios;
        }

        public bool Agendar(string nome, string telefone, DateTime dataHora, string servico = "Consulta")
        {
            // Validação de conflito
            if (_agendamentos.Any(a => a.DataHora == dataHora && a.Status == "Confirmado"))
                return false;

            _agendamentos.Add(new AgendamentoModel
            {
                ClienteNome = nome,
                ClienteTelefone = telefone,
                DataHora = dataHora,
                Servico = servico
            });
            SaveData();
            return true;
        }

        public List<AgendamentoModel> ListarAgendamentos() => _agendamentos.OrderBy(a => a.DataHora).ToList();

        private class SchedulingData
        {
            public List<AgendamentoModel> Agendamentos { get; set; } = new List<AgendamentoModel>();
            public List<SlotDisponivel> Slots { get; set; } = new List<SlotDisponivel>();
        }
    }
}
