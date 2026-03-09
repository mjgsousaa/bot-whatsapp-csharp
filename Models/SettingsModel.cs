using System;

namespace BotWhatsappCSharp.Models
{
    public class SettingsModel
    {
        public string ApiKey { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = "Você é um assistente virtual útil e educado.";
        public string AiMediaFile { get; set; } = string.Empty;
        public string BulkMediaFile { get; set; } = string.Empty;
    }
}
