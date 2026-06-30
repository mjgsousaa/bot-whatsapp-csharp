using System;

namespace EvolutionBot.Api.Models
{
    public class Appointment
    {
        public Guid Id { get; set; }
        public string PatientPhoneNumber { get; set; }
        public string StatusAgendamento { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string Details { get; set; }
    }
}
