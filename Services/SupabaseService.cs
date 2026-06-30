using Supabase;
using EvolutionBot.Api.Models;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace EvolutionBot.Api.Services
{
    public class SupabaseService
    {
        private readonly Client _supabaseClient;
        private readonly ILogger<SupabaseService> _logger;

        public SupabaseService(Client supabaseClient, ILogger<SupabaseService> logger)
        {
            _supabaseClient = supabaseClient;
            _logger = logger;
        }

        public async Task<bool> SaveMessage(Message message)
        {
            try
            {
                await _supabaseClient.From<Message>().Insert(message);
                _logger.LogInformation("Mensagem salva no Supabase: {MessageId}", message.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar mensagem no Supabase: {MessageId}", message.Id);
                return false;
            }
        }

        public async Task<Appointment> GetAppointmentByPhoneNumber(string phoneNumber)
        {
            try
            {
                var response = await _supabaseClient.From<Appointment>()
                                                    .Where(x => x.PatientPhoneNumber == phoneNumber)
                                                    .Get();
                return response.Models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar agendamento por telefone {PhoneNumber}", phoneNumber);
                return null;
            }
        }

        public async Task<bool> UpdateAppointmentStatus(Guid appointmentId, string newStatus)
        {
            try
            {
                await _supabaseClient.From<Appointment>()
                                    .Where(x => x.Id == appointmentId)
                                    .Set(x => x.StatusAgendamento, newStatus)
                                    .Update();
                _logger.LogInformation("Status do agendamento {AppointmentId} atualizado para {NewStatus}", appointmentId, newStatus);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar status do agendamento {AppointmentId}", appointmentId);
                return false;
            }
        }
    }
}
