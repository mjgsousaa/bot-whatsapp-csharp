using System;

namespace BotWhatsappCSharp.Models
{
    public class ChamadoModel
    {
        public string Numero { get; set; } = string.Empty;
        public string Nome { get; set; } = "Cliente";
        public string UltimaMensagem { get; set; } = string.Empty;
        public DateTime Horario { get; set; } = DateTime.Now;
    }
}
