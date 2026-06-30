namespace EvolutionBot.Api.DTOs.EvolutionApi
{
    public class CreateInstanceResponse
    {
        public string QrCodeBase64 { get; set; }
        public string InstanceId { get; set; }
        public string InstanceName { get; set; }
        public string ConnectionStatus { get; set; }
    }
}
