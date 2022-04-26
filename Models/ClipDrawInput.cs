using System.Text.Json.Serialization;

namespace NightmareBot.Models;

public class ClipDrawInput : IGeneratorInput
{
    [JsonPropertyName("prompt")]
    public string prompt { get; set; }

    [JsonPropertyName("num_paths")] public int NumPaths { get; set; } = 256;
    [JsonPropertyName("num_iterations")] public int NumIterations { get; set; } = 20;
    [JsonPropertyName("display_frequency")] public int DisplayFrequency = 10;
}