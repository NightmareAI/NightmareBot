﻿using Discord.Commands;
using Discord.WebSocket;
using System.Text.Json.Serialization;
using Discord;
using Discord.Interactions;
using NightmareBot.Common;

namespace NightmareBot.Models;

public class PredictionRequest<T> where T : IGeneratorInput
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
    
    private string _request_type { get {
        switch (input) {
            case DeepMusicInput:
                return "deep-music";
            case Laionidev3Input:
                return "laionide-v3";
            case Laionidev4Input:
                return "laionide-v4";
            case LatentDiffusionInput:
                return "latent-diffusion";
            case PixrayInput:
                return "pixray";
            case SwinIRInput:
                return "swinir";
            case VRTInput:
                return "vrt";
            default:
                return "unknown";
        }
    }
    }
    
    public PredictionRequest()
    {
        
    }

    public PredictionRequest(SocketInteractionContext context, T input, Guid id)
    {
        this.id = id;
        this.context.guild = context.Guild.Id.ToString();
        this.context.channel = context.Channel.Id.ToString();
        this.context.user = context.User.Id.ToString();
        this.context.interaction = context.Interaction.Id.ToString();
        this.context.token = context.Interaction.Token.ToString();
        this.input = input;
    }

    public PredictionRequest(SocketCommandContext context, T input, Guid id)
    {
        this.id = id;
        this.context.guild = context.Guild.Id.ToString();
        this.context.channel = context.Channel.Id.ToString();
        this.context.user = context.User.Id.ToString();
        this.context.message = context.Message.Id.ToString();
        this.input = input;
    }
    
    public PredictionRequest(IInteractionContext context, T input, Guid id)
    {
        this.id = id;
        this.context.guild = context.Guild.Id.ToString();
        this.context.channel = context.Channel.Id.ToString();
        this.context.user = context.User.Id.ToString();
        this.context.interaction = context.Interaction.Id.ToString();
        this.context.token = context.Interaction.Token.ToString();
        this.input = input;
    }

    public PredictionRequest(SocketSlashCommand command, T input, Guid id)
    {
        this.id = id;
        this.context.channel = command.Channel.Id.ToString();
        this.context.user = command.User.Id.ToString();
        this.context.token = command.Token;
        this.input = input;
    }
}