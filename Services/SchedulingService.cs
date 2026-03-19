using System;
using System.Collections.Generic;
using System.Linq;
using BotWhatsappCSharp.Models;

namespace BotWhatsappCSharp.Services
{
    public class SchedulingService
    {
        private readonly DatabaseService _db;

        public SchedulingService(DatabaseService db)
        {
            _db = db;
        }

        public List<DateTime> GetHorariosDisponiveis(int ano, int mes)
        {
            return _db.GetHorariosDisponiveisMes(ano, mes);
        }

        public bool Agendar(string nome, string telefone, DateTime dataHora, string servico = "Consulta")
        {
            if (_db.ExisteConflito(dataHora)) return false;

            var ag = new AgendamentoModel
            {
                ClienteNome = nome,
                ClienteTelefone = telefone,
                DataHora = dataHora,
                Servico = servico
            };

            return _db.SalvarAgendamento(ag);
        }

        public List<AgendamentoModel> ListarAgendamentos() => _db.ListarAgendamentos();
    }
}
