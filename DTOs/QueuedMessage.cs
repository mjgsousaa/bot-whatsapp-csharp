using System;

namespace EvolutionBot.Api.DTOs
{
    public class QueuedMessage
    {
        public string InstanceId { get; set; }
        public string Number { get; set; }
        public string TextMessage { get; set; }
        public DateTime QueuedTime { get; set; }
    }
}
