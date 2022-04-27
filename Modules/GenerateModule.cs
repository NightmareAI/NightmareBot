using Discord;
using Discord.Commands;
using NightmareBot.Models;
using NightmareBot.Services;

namespace NightmareBot.Modules;

public class GenerateModel : ModuleBase<SocketCommandContext>
{
    private readonly GenerateService _generateService;

    public GenerateModel(GenerateService generateService)
    {
        _generateService = generateService;
    } 
    
    [Command("gen")]
    [Summary("Generates a nightmare using the default model (currently laionide-v4) and a random seed.")]
    public async Task GenerateAsync([Remainder] [Summary("The prediction text")] string text)
    {
        var seed = Random.Shared.NextInt64();
        var id = Guid.NewGuid();
        var request =
            new PredictionRequest<Laionidev4Input>(Context, new Laionidev4Input(text, null, seed), id);
        _generateService.Laionidev4RequestQueue.Enqueue(request);
        await Context.Message.AddReactionAsync(new Emoji("✔️"));
    }

    [Command("style")]
    public async Task Generate4Async([Summary("Style tags")] string style, [Remainder][Summary("The prediction text")] string text)
    {
        string styleTags = "";
        switch (style)
        {
            case "pixelart":
                case "cc12m":
                case "pokemon":
                case "country211":
                case "openimages":
                case "ffhq":
                case "coco":
                case "vaporwave":
                case "virtualgenome":
                case "imagenet":
                styleTags = $"<{style}>";
                break;
            default:
                await Context.Channel.SendMessageAsync(
                    "Please start with a valid style tags. Valid tags are: pixelart, cc12m, pokemon, country211, openimages, ffhq, coco, vaporwave, virtualgenome, imagenet");
                return;
        }
        
        
        
        var seed = Random.Shared.NextInt64();
        var id = Guid.NewGuid();
        var request = new PredictionRequest<Laionidev4Input>(Context, new Laionidev4Input(text, styleTags, seed), id);
        _generateService.Laionidev4RequestQueue.Enqueue(request);
        await Context.Message.AddReactionAsync(new Emoji("✔️"));
    }

    [Command("draw")]
    public async Task DrawAsync([Summary("The drawer engine")] string drawer, [Remainder][Summary("The prompt")] string prompt) 
    {
        switch (drawer)
        {
            case "pixel":
                case "vqgan":
                case "vdiff":
                case "fft":
                case "fast_pixel":
                case "line_sketch":
                case "clipdraw":
                break;
            default:
                await Context.Channel.SendMessageAsync(
                    "Please start with a drawer name. Drawers are: pixel vqgan vdiff fft fast_pixel line_sketch clipdraw");
                return;
        }
        
        var seed = Random.Shared.NextInt64();
        var id = Guid.NewGuid();
        var request = new PredictionRequest<PixrayInput>(Context, new PixrayInput { drawer = drawer, prompts = prompt, }, id);
        _generateService.PixrayRequestQueue.Enqueue(request);
        await Context.Message.AddReactionAsync(new Emoji("✔️"));
        
    }
    
    [Command("regen")]
    [Summary("Generates a nightmare using the default model and a specific seed with higher settings.")]
    public async Task RegenerateAsync([Summary("The seed")] long seed,
        [Remainder] [Summary("The prediction text")] string text)
    {
        var id = Guid.NewGuid();
        var request = new PredictionRequest<Laionidev3Input>(Context, new Laionidev3Input(text, seed), id)
        {
            input =
            {
                batch_size = 6,
                sr_timestep_respacing = "20",
                timestep_respacing = "75"
            }
        };
        _generateService.Laionidev3RequestQueue.Enqueue(request);
        await Context.Message.AddReactionAsync(new Emoji("✔️"));
    }

    [Command("clipdraw")]
    [Summary("Generates a nightmare using the clipdraw model, which may suck.")]
    public async Task ClipdrawAsyns([Remainder] [Summary("The prediction text")] string text)
    {
        /*
        var id = Guid.NewGuid();
        var request = new PredictionRequest<ClipDrawInput>(Context, new ClipDrawInput() { prompt = text }, id);
        _generateService.ClipdrawRequestQueue.Enqueue(request);*/
        await Context.Message.AddReactionAsync(new Emoji("❌"));
    }
    
}