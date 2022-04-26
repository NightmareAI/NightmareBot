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
    [Summary("Generates a nightmare using the default model (currently laionide-v3) and a random seed.")]
    public async Task GenerateAsync([Remainder] [Summary("The prediction text")] string text)
    {
        var seed = Random.Shared.NextInt64();
        var id = Guid.NewGuid();
        var request = new PredictionRequest<Laionidev3Input>(Context, new Laionidev3Input(text, seed), id)
        {
            input =
            {
                batch_size = 3,
                timestep_respacing = "35",
                sr_timestep_respacing = "20",
                side_x = 128,
                side_y = 64
            }
        };
        _generateService.Laionidev3RequestQueue.Enqueue(request);
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