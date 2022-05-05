using System.Text.Json.Serialization;

namespace NightmareBot.Models;

public class DiscordContext
{
    [JsonPropertyName("guild")]
    public ulong guild { get; set; }

    [JsonPropertyName("channel")]
    public ulong channel { get; set; }

    [JsonPropertyName("user")]
    public ulong user { get; set; }
    
    [JsonPropertyName("message")]
    public ulong message { get; set; }
}