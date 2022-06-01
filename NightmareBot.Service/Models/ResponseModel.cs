using NightmareBot.Common;
using System.Text.Json.Serialization;

namespace NightmareBot.Models;

public class ResponseModel 
{
    [JsonPropertyName("id")]
    public Guid id {get; set;}
    [JsonPropertyName("context")]
    public DiscordContext? context {get; set;}
    [JsonPropertyName("images")]
    public string[]? images {get; set;}

    [JsonPropertyName("error")]
    public string? error {get; set;}
}