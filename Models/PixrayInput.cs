using System.Text.Json.Serialization;

namespace NightmareBot.Models;

public class PixrayInput : IGeneratorInput
{
    [JsonPropertyName("prompts")] public string prompts { get; set; } = "";

    [JsonPropertyName("drawer")] public string drawer { get; set; } = "vqgan";

    [JsonPropertyName("settings")] public string settings { get; set; } = @"# random number seed can be a word or number
seed: reference
# higher quality than default
quality: better
# smooth out the result a bit
custom_loss: smoothness:0.5
# enable transparency in image
transparent: true
# how much to encourage transparency (can also be negative) 
transparent_weight: 0.1";
}