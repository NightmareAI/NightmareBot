using System.Text.Json.Serialization;

namespace NightmareBot.Models;

public class PixrayInput : IGeneratorInput
{
    [JsonPropertyName("prompts")] public string prompts { get; set; } = "";

    [JsonPropertyName("drawer")] public string drawer { get; set; } = "vqgan";

    [JsonPropertyName("settings")] public string settings { get; set; } = "\n";
}