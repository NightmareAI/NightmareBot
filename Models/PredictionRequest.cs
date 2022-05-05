using Discord.Commands;
using Discord.WebSocket;

namespace NightmareBot.Models;

public class PredictionRequest<T> where T : IGeneratorInput
{
    public T input { get; set; }

    public ulong guild_id { get; set; }
    public ulong channel_id { get; set; }

    public ulong user_id { get; set; }
    
    public ulong message_id { get; set; }
    
    public Guid id { get; set; }
    
    public PredictionRequest(SocketCommandContext context, T input, Guid id)
    {
        this.Id = id;
        this.GuildId = context.Guild.Id;
        this.ChannelId = context.Channel.Id;
        this.UserId = context.User.Id;
        this.MessageId = context.Message.Id;
        this.input = input;
    }
}