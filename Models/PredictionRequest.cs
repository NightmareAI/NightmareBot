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
        this.id = id;
        this.guild_id = context.Guild.Id;
        this.channel_id = context.Channel.Id;
        this.user_id = context.User.Id;
        this.message_id = context.Message.Id;
        this.input = input;
    }
}