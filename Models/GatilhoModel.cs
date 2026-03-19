using System;
using System.Linq; 
using System.Text.Json.Serialization;

namespace BotWhatsappCSharp.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TipoMidia { Nenhum, Imagem, PDF, Audio, Video }

    public class GatilhoModel
    {
        public string Comando { get; set; } = string.Empty;
        public string Resposta { get; set; } = string.Empty;
        public System.Collections.Generic.List<string> CaminhosArquivos { get; set; } = new System.Collections.Generic.List<string>();
        public TipoMidia TipoMidiaAnexo { get; set; } = TipoMidia.Nenhum;
        
        // Propriedade para facilitar exibição na UI
        public bool TemAnexo => CaminhosArquivos != null && CaminhosArquivos.Count > 0;
        public string NomeArquivo => TemAnexo ? string.Join(", ", CaminhosArquivos.Select(System.IO.Path.GetFileName)) : "Nenhum";
    }
}
