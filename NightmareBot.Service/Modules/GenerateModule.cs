using Dapr.Client;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Minio;
using NightmareBot.Models;
using NightmareBot.Services;
using OpenAI;
using System.Text;

namespace NightmareBot.Modules;

public class GenerateModel : ModuleBase<SocketCommandContext>
{
    private readonly GenerateService _generateService;
    private readonly DaprClient _daprClient;
    private readonly ILogger<GenerateModel> _logger;
    private readonly InteractionService _handler;
    private readonly MinioClient _minioClient;
    private readonly OpenAIClient _openAI;

    public GenerateModel(GenerateService generateService, DaprClient daprClient, InteractionService handler, MinioClient minioClient, OpenAIClient openAI)
    {
        _generateService = generateService;
        _daprClient = daprClient;
        _handler = handler;
        this._minioClient = minioClient;
        _openAI = openAI;
    }

    [Command("reg")]
    [Discord.Commands.Summary("Registers guild commands")]
    public async Task Register()
    {
        try
        {
            await _handler.RegisterCommandsToGuildAsync(Context.Guild.Id);
        }
        catch(Exception exception)
        {
            _logger.LogError(exception, exception.Message);
        }

        await Context.Message.AddReactionAsync(new Emoji("✔️"));
    }

    [Command("gen")]
    [Discord.Commands.Summary("Generates a nightmare using the default model (currently latent diffusion, or pixray if an image is supplied).")]
    public async Task GenerateAsync([Remainder] [Discord.Commands.Summary("The prediction text")] string text)
    {
        if (Context.Message.Attachments.Any() || (Context.Message.ReferencedMessage?.Attachments.Any() ?? false)) 
        {            
            var input = new PixrayInput();
            await PixrayAsync(text, input);
        } 
        else 
        {
            var input = new LatentDiffusionInput();    
            await LatentDiffusionAsync(text, input);  
        }        
    }

    [Command("gptgen")]
    public async Task AiGenerateAsync([Remainder][Discord.Commands.Summary("The prediction text")] string text)
    {
        var prompt = $"Describe an AI generated artwork with the title \"{text}\":\n\n";
        var generated = await _openAI.CompletionEndpoint.CreateCompletionAsync(prompt, max_tokens: 75, temperature: 0.7, presencePenalty: 0, frequencyPenalty: 0, engine: new Engine("text-davinci-002"));
        var newPrompt = generated.Completions.First().Text.Trim();        
        var input = new LatentDiffusionInput();
        var id = Guid.NewGuid();
        var request = new PredictionRequest<LatentDiffusionInput>(Context, input, id);
        input.prompt = newPrompt;
        request.request_state.prompt = newPrompt;
        request.request_state.gpt_prompt = text;

        var inputBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(input));
        var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));
        var promptBytes = Encoding.UTF8.GetBytes(input.prompt);
        var idBytes = Encoding.UTF8.GetBytes(id.ToString());
        var putObjectArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/input.json").WithStreamData(new MemoryStream(inputBytes)).WithObjectSize(inputBytes.Length).WithContentType("application/json");
        await _minioClient.PutObjectAsync(putObjectArgs);
        var putContextArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
        await _minioClient.PutObjectAsync(putContextArgs);
        var promptArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
        await _minioClient.PutObjectAsync(promptArgs);
        var idArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
        await _minioClient.PutObjectAsync(idArgs);


        await _daprClient.SaveStateAsync("cosmosdb", $"prompts-{id}", input.prompt);
        //_generateService.LatentDiffusionQueue.Enqueue(request);
        await Enqueue(request);
        await Context.Message.AddReactionAsync(new Emoji("✔️"));        
    }

    [Command("ldm")]
    [Discord.Commands.Summary("Generates a nightmare using the latent diffusion model")]
    public async Task LatentDiffusionAsync([Discord.Commands.Summary("The prediction text")] string text, [Discord.Commands.Summary("Latent diffusion settings")] LatentDiffusionInput input = default) 
    {
        var id = Guid.NewGuid();
        var request = new PredictionRequest<LatentDiffusionInput>(Context, input, id);
        if (!string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(input.prompt))
            input.prompt = text;
        request.request_state.prompt = text;

        var inputBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(input));
        var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));
        var promptBytes = Encoding.UTF8.GetBytes(input.prompt);
        var idBytes = Encoding.UTF8.GetBytes(id.ToString());
        var putObjectArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/input.json").WithStreamData(new MemoryStream(inputBytes)).WithObjectSize(inputBytes.Length).WithContentType("application/json");
        await _minioClient.PutObjectAsync(putObjectArgs);
        var putContextArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
        await _minioClient.PutObjectAsync(putContextArgs);
        var promptArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
        await _minioClient.PutObjectAsync(promptArgs);
        var idArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
        await _minioClient.PutObjectAsync(idArgs);


        await _daprClient.SaveStateAsync("cosmosdb", $"prompts-{id}", input.prompt);
        //_generateService.LatentDiffusionQueue.Enqueue(request);
        await Enqueue(request);
        await Context.Message.AddReactionAsync(new Emoji("✔️"));        
    }

    [Command("viz")]
    [Discord.Commands.Summary("Deep music visualizer")]
    public async Task DeepMusicVizAsync([Discord.Commands.Summary("Deep Music Settings")] DeepMusicInput input = default)
    {
        var id = Guid.NewGuid();
        var request = new PredictionRequest<DeepMusicInput>(Context, input, id);
        
        IAttachment song = null;
        if (Context.Message.Attachments.Any()) {
            song = Context.Message.Attachments.First();
        } else if (Context.Message.ReferencedMessage?.Attachments.Any() ?? false)
        {
            song = Context.Message.ReferencedMessage.Attachments.First();
        }

        if (song == null)
            await Context.Message.AddReactionAsync(new Emoji("❌"));
        
        request.input.song = song.Url;

        _generateService.DeepMusicQueue.Enqueue(request);
        await Enqueue(request);
        await Context.Message.AddReactionAsync(new Emoji("✔️"));        

    }

    [Command("pixray")]
    [Discord.Commands.Summary("Raw access to the Pixray engine")]
    public async Task PixrayAsync([Discord.Commands.Summary("The prediction text")] string text, [Discord.Commands.Summary("Extra Pixray settings")] PixrayInput input) 
    {
        var id = Guid.NewGuid();
        var request = new PredictionRequest<PixrayInput>(Context, input, id);
        if (!string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(input.prompts))
            input.prompts = text;

        IAttachment initImage = null;
        if (Context.Message.Attachments.Any()) {
            // First attachment is init image, that's all for now
            initImage = Context.Message.Attachments.First();
        } else if (Context.Message.ReferencedMessage?.Attachments.Any() ?? false)
        {
            initImage = Context.Message.ReferencedMessage.Attachments.First();
        }

        if (initImage != null)
        {
            request.input.init_image = initImage.Url;
            if (initImage.Width.HasValue && initImage.Height.HasValue) 
            {
                double width = initImage.Width.Value;
                double height = initImage.Height.Value;

                while (width > 1024 || height > 1024)
                {
                    width = width *0.75;
                    height = height *0.75;
                }

                request.input.size = new int[] { (int)width, (int)height };
            }
        }

        await _daprClient.SaveStateAsync("cosmosdb", $"prompts-{id}", input.prompts);
        await Enqueue(request);
        _generateService.PixrayRequestQueue.Enqueue(request);
        await Context.Message.AddReactionAsync(new Emoji("✔️"));        
    }

    [Command("fenhance")]
    public async Task FaceEnhanceAsync()
    {
        var images = new List<string>();
        var id = Guid.NewGuid();

        foreach (var attachment in Context.Message.Attachments)
        {
            images.Add(attachment.Url);
        }

        if (Context.Message.ReferencedMessage != null)
        {
            foreach (var attachment in Context.Message.ReferencedMessage.Attachments)
            {
                images.Add(attachment.Url);
            }
        }

        var image = images.FirstOrDefault();

        if (image != null)
        {
            var httpClient = new HttpClient();
            var input = new EsrganInput { images = images.ToArray(), face_enhance = true, outscale = 2 };
            var request = new PredictionRequest<EsrganInput>(Context, input, id);
            var imageBytes = await httpClient.GetByteArrayAsync(image);
            request.request_state.prompt = "Enhanced image";
            var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));
            var idBytes = Encoding.UTF8.GetBytes(request.id.ToString());
            var promptBytes = Encoding.UTF8.GetBytes("Enhanced image");
            var putContextArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{request.id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putContextArgs);
            var idArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{request.id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(idArgs);
            var promptArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{request.id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(promptArgs);
            var imageArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{request.id}/input.png").WithStreamData(new MemoryStream(imageBytes)).WithObjectSize(imageBytes.Length).WithContentType("image/png");
            await _minioClient.PutObjectAsync(imageArgs);

            await Enqueue(request);
            //_generateService.SwinIRRequestQueue.Enqueue(request);
            await Context.Message.AddReactionAsync(new Emoji("✔️"));
        }
    }

    [Command("enhance")]
    public async Task EnhanceAsync()
    {
        var images = new List<string>();
        var id = Guid.NewGuid();

        foreach (var attachment in Context.Message.Attachments) 
        {
            images.Add(attachment.Url);
        }

        if (Context.Message.ReferencedMessage != null)
        {
            foreach (var attachment in Context.Message.ReferencedMessage.Attachments)
            {
                images.Add(attachment.Url);
            }
        }

        var image = images.FirstOrDefault();

        if (image != null) {
            var httpClient = new HttpClient();
            var input = new SwinIRInput { images = images.ToArray() };
            var request = new PredictionRequest<SwinIRInput>(Context, input, id);
            var imageBytes = await httpClient.GetByteArrayAsync(image);
            request.request_state.prompt = "Enhanced image";
            var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));
            var idBytes = Encoding.UTF8.GetBytes(request.id.ToString());
            var promptBytes = Encoding.UTF8.GetBytes("Enhanced image");
            var putContextArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{request.id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putContextArgs);
            var idArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{request.id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(idArgs);
            var promptArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{request.id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(promptArgs);
            var imageArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{request.id}/input.png").WithStreamData(new MemoryStream(imageBytes)).WithObjectSize(imageBytes.Length).WithContentType("image/png");
            await _minioClient.PutObjectAsync(imageArgs);

            await Enqueue(request);
            //_generateService.SwinIRRequestQueue.Enqueue(request);
            await Context.Message.AddReactionAsync(new Emoji("✔️"));
        }
    }

    [Command("venhance")]
    public async Task VideoEnhanceAsync()
    {
        var images = new List<string>();
        var id = Guid.NewGuid();


        IAttachment video = null;
        if (Context.Message.Attachments.Any()) {
            video = Context.Message.Attachments.First();
        } else if (Context.Message.ReferencedMessage?.Attachments.Any() ?? false)
        {
            video = Context.Message.ReferencedMessage.Attachments.First();
        }

        if (video == null)
            await Context.Message.AddReactionAsync(new Emoji("❌"));
        
        var input = new VRTInput { video = video.Url };
        var request = new PredictionRequest<VRTInput>(Context, input, id); 
        Enqueue(request);
        _generateService.VRTQueue.Enqueue(request);
        await Context.Message.AddReactionAsync(new Emoji("✔️"));
    }

    [Command("style")]
    public async Task Generate4Async([Discord.Commands.Summary("Style tags")] string style, [Remainder][Discord.Commands.Summary("The prediction text")] string text)
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
        Enqueue(request);
        _generateService.Laionidev4RequestQueue.Enqueue(request);
        await Context.Message.AddReactionAsync(new Emoji("✔️"));
    }

    [Command("draw")]
    public async Task DrawAsync([Discord.Commands.Summary("The drawer engine")] string drawer, [Remainder][Discord.Commands.Summary("The prompt")] string prompt) 
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
        Enqueue(request);
        await Context.Message.AddReactionAsync(new Emoji("✔️"));
        
    }
    
    [Command("regen")]
    [Discord.Commands.Summary("Generates a nightmare using the default model and a specific seed with higher settings.")]
    public async Task RegenerateAsync([Discord.Commands.Summary("The seed")] long seed,
        [Remainder] [Discord.Commands.Summary("The prediction text")] string text)
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
        Enqueue(request);
        await Context.Message.AddReactionAsync(new Emoji("✔️"));
    }

    [Command("clipdraw")]
    [Discord.Commands.Summary("Generates a nightmare using the clipdraw model, which may suck.")]
    public async Task ClipdrawAsync([Remainder] [Discord.Commands.Summary("The prediction text")] string text)
    {
        /*
        var id = Guid.NewGuid();
        var request = new PredictionRequest<ClipDrawInput>(Context, new ClipDrawInput() { prompt = text }, id);
        _generateService.ClipdrawRequestQueue.Enqueue(request);*/
        await Context.Message.AddReactionAsync(new Emoji("❌"));
    }

    private async Task Enqueue<T>(PredictionRequest<T> request) where T : IGeneratorInput
    {
        await _daprClient.PublishEventAsync("jetstream-pubsub", $"request.{request.request_type}", request);
        await _daprClient.SaveStateAsync("cosmosdb", request.id.ToString(), request);
    }
    
}