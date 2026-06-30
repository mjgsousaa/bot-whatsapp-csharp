using System;

namespace EvolutionBot.Api.Models
{
    public class Message
    {
        public Guid Id { get; set; }
        public string Sender { get; set; }
        public string Receiver { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public string Status { get; set; } // e.g., "received", "sent", "delivered", "read"
        public string MessageType { get; set; } // e.g., "text", "image", "audio"
        public string InstanceId { get; set; }
    }
}
