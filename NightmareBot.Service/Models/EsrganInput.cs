using System.Text.Json.Serialization;

namespace NightmareBot.Models
{
    public class EsrganInput : IGeneratorInput
    {
        [JsonPropertyName("images")]
        public string[] images { get; set; }

        [JsonPropertyName("face_enhance")]
        public bool face_enhance { get; set; } = true;

        [JsonPropertyName("outscale")]
        public int outscale { get; set; } = 4;
    }
}
