using System.Text.Json.Serialization;
using Discord.Commands;
using Discord.WebSocket;

namespace NightmareBot.Models;

public class PredictionRequest<T> where T : IGeneratorInput
{
    [JsonPropertyName("input")]
    public T input { get; set; }

    [JsonPropertyName("output_file_prefix")]
    public string output_file_prefix { get; set; }

    [JsonIgnore] public ulong GuildId { get; set; }
    [JsonIgnore] public ulong ChannelId { get; set; }

    [JsonIgnore] public ulong UserId { get; set; }
    
    [JsonIgnore] public ulong MessageId { get; set; }
    
    [JsonIgnore] public Guid Id { get; set; }
    
    public PredictionRequest(SocketCommandContext context, T input, Guid id)
    {
        this.Id = id;
        this.GuildId = context.Guild.Id;
        this.ChannelId = context.Channel.Id;
        this.UserId = context.User.Id;
        this.MessageId = context.Message.Id;
        this.input = input;
        this.output_file_prefix = $"http://10.40.5.128:5224/Result/{id}/";
    }
}