using System.Text.Json.Serialization;

namespace NightmareBot.Models;

public class Laionidev3Input : IGeneratorInput
{
    [JsonPropertyName("prompt")]
    public string prompt { get; set; } = "";
    [JsonPropertyName("batch_size")]
    public int batch_size { get; set; } = 2;
    [JsonPropertyName("side_x")]
    public int side_x { get; set; } = 64;
    [JsonPropertyName("side_y")]
    public int side_y { get; set; } = 64;
    [JsonPropertyName("upsample_stage")]
    public bool upsample_stage { get; set; } = true;
    [JsonPropertyName("guidance_scale")]
    public int guidance_scale { get; set; } = 12;
    [JsonPropertyName("upsample_temp")]
    public float upsample_temp { get; set; } = 0.998f;
    [JsonPropertyName("timestep_respacing")]
    public string timestep_respacing { get; set; } = "50";
    [JsonPropertyName("sr_timestep_respacing")]
    public string sr_timestep_respacing { get; set; } = "35";
    [JsonPropertyName("seed")]
    public long seed { get; set; } = 0;

    public Laionidev3Input(string prompt, long seed)
    {
        this.prompt = prompt;
        this.seed = seed;
    }
}