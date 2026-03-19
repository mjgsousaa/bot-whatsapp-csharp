using System;

namespace BotWhatsappCSharp.Models
{
    public class AgendamentoModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ClienteNome { get; set; } = string.Empty;
        public string ClienteTelefone { get; set; } = string.Empty;
        public DateTime DataHora { get; set; }
        public string Servico { get; set; } = "Consulta/Aula";
        public string Status { get; set; } = "Confirmado"; // Confirmado, Cancelado, Concluído
    }

    public class SlotDisponivel
    {
        public DayOfWeek DiaSemana { get; set; }
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFim { get; set; }
    }

    public class SlotConfigurado
    {
        public DateTime DataHora { get; set; }
        public bool Disponivel { get; set; } = true;
        public string AgendadoPor { get; set; } = "";
        public string TelefoneCliente { get; set; } = "";
    }
}
