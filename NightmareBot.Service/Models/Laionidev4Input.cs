using System.Text.Json.Serialization;

namespace NightmareBot.Models;

[Obsolete("Model no longer implemented")]
public class Laionidev4Input : IGeneratorInput
{
    [JsonPropertyName("prompt")]
    public string prompt { get; set; } = "";

    [JsonPropertyName("style_prompt")]
    public string style_prompt { get; set; } = null;
    [JsonPropertyName("batch_size")]
    public int batch_size { get; set; } = 1;
    [JsonPropertyName("side_x")]
    public string side_x { get; set; } = "64";
    [JsonPropertyName("side_y")]
    public string side_y { get; set; } = "64";
    [JsonPropertyName("upsample_stage")]
    public bool upsample_stage { get; set; } = true;
    [JsonPropertyName("guidance_scale")]
    public int guidance_scale { get; set; } = 10;

    [JsonPropertyName("style_guidance_scale")]
    public int style_guidance_scale { get; set; } = 4;
    [JsonPropertyName("upsample_temp")]
    public string upsample_temp { get; set; } = "0.997";
    [JsonPropertyName("timestep_respacing")]
    public string timestep_respacing { get; set; } = "50";
    [JsonPropertyName("sr_timestep_respacing")]
    public string sr_timestep_respacing { get; set; } = "17";
    [JsonPropertyName("seed")]
    public long seed { get; set; } = 0;

    public Laionidev4Input(string prompt, string stylePrompt, long seed)
    {
        this.prompt = prompt;
        this.style_prompt = stylePrompt;
        this.seed = seed;
    }
}