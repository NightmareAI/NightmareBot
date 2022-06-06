using Discord.Commands;
using Discord.WebSocket;
using System.Text.Json.Serialization;
using Discord;
using Discord.Interactions;
using NightmareBot.Common;

namespace NightmareBot.Models;

public class PredictionRequest<T> where T : IGeneratorInput, new()
{
    [JsonPropertyName("input")]
    public T input { get; set; } 

    [JsonPropertyName("context")]
    public DiscordContext context { get; set; } = new DiscordContext();
    
    [JsonPropertyName("id")]
    public Guid id { get; set; }

    [JsonPropertyName("sample_filenames")]
    public string[] sample_filenames { get; set; } = new string[0];

    [JsonPropertyName("request_time")]
    public DateTime request_time { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("start_time")]
    public DateTime? start_time { get; set; } = null;

    [JsonPropertyName("complete_time")]
    public DateTime? complete_time { get; set; } = null;

    // weird way to deal with json serialization issues
    [JsonPropertyName("request_type")]
    public string request_type { get { return _request_type; } set {} }

    [JsonIgnore]
    public RequestState request_state { get; set; } = new RequestState();

    private string _request_type
    {
        get
        {
            switch (input)
            {
                case LatentDiffusionInput:
                    return "latent-diffusion";
                case PixrayInput:
                    return "pixray";
                case SwinIRInput:
                    return "swinir";
                case EsrganInput:
                    return "esrgan";
                case MajestyDiffusionInput:
                    return "majesty-diffusion";
                default:
                    return "unknown";
            }
        }
    }
    
    public PredictionRequest()
    {
        this.request_state.created_at = this.request_time;
        this.request_state.last_updated = DateTime.UtcNow;
        this.request_state.request_type = this.request_type;
        this.input = new T();
    }

    public PredictionRequest(T input, Guid id) : this()
    {
        this.id = id;
        this.input = input;
        this.request_state.request_id = this.id.ToString();
    }

    public PredictionRequest(SocketInteractionContext context, T input, Guid id) : this(input, id)
    {        
        this.context.guild = context.Guild.Id.ToString();
        this.context.channel = context.Channel.Id.ToString();
        this.context.user = context.User.Id.ToString();
        this.context.interaction = context.Interaction.Id.ToString();
        this.context.token = context.Interaction.Token.ToString();
        this.request_state.discord_context = this.context;
    }

    public PredictionRequest(SocketCommandContext context, T input, Guid id) : this(input, id)
    {        
        this.context.guild = context.Guild.Id.ToString();
        this.context.channel = context.Channel.Id.ToString();
        this.context.user = context.User.Id.ToString();
        this.context.message = context.Message.Id.ToString();
        this.request_state.discord_context = this.context;
    }
    
    public PredictionRequest(IInteractionContext context, T input, Guid id) : this(input, id)
    {        
        this.context.guild = context.Guild.Id.ToString();
        this.context.channel = context.Channel.Id.ToString();
        this.context.user = context.User.Id.ToString();
        this.context.interaction = context.Interaction.Id.ToString();
        this.context.token = context.Interaction.Token.ToString();
        this.request_state.discord_context = this.context;
    }

    public PredictionRequest(SocketSlashCommand command, T input, Guid id) : this(input, id)
    {        
        this.context.channel = command.Channel.Id.ToString();
        this.context.user = command.User.Id.ToString();
        this.context.token = command.Token;
        this.request_state.discord_context = this.context;
    }
}