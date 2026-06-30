using System;
using Newtonsoft.Json;

namespace EvolutionBot.Api.DTOs.EvolutionApi
{
    public class WebhookPayload
    {
        [JsonProperty("instance")]
        public string Instance { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("data")]
        public WebhookData Data { get; set; }
    }

    public class WebhookData
    {
        [JsonProperty("key")]
        public WebhookKey Key { get; set; }

        [JsonProperty("messageTimestamp")]
        public long MessageTimestamp { get; set; }

        [JsonProperty("message")]
        public WebhookMessage Message { get; set; }

        [JsonProperty("pushName")]
        public string PushName { get; set; }

        [JsonProperty("from")]
        public string From { get; set; }

        [JsonProperty("to")]
        public string To { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; } // For status webhooks

        [JsonProperty("ack")]
        public int Ack { get; set; } // For status webhooks (0 = pending, 1 = sent, 2 = delivered, 3 = read, 4 = failed)
    }

    public class WebhookKey
    {
        [JsonProperty("remoteJid")]
        public string RemoteJid { get; set; }

        [JsonProperty("fromMe")]
        public bool FromMe { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("participant")]
        public string Participant { get; set; }
    }

    public class WebhookMessage
    {
        [JsonProperty("conversation")]
        public string Conversation { get; set; }

        [JsonProperty("extendedTextMessage")]
        public ExtendedTextMessage ExtendedTextMessage { get; set; }
    }

    public class ExtendedTextMessage
    {
        [JsonProperty("text")]
        public string Text { get; set; }
    }
}
