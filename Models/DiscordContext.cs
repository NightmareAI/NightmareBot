using System.Text.Json.Serialization;

namespace NightmareBot.Models;

public class DiscordContext
{
    [JsonPropertyName("guild")]
    public string? guild { get; set; }

    [JsonPropertyName("channel")]
    public string? channel { get; set; }

    [JsonPropertyName("user")]
    public string? user { get; set; }
    
    [JsonPropertyName("message")]
    public string? message { get; set; }
    
    [JsonPropertyName("interaction")]
    public string? interaction { get; set; }

    [JsonPropertyName("token")]
    public string? token { get; set; }
}