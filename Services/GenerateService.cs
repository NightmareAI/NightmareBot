using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Discord;
using Discord.WebSocket;
using NightmareBot.Models;

namespace NightmareBot.Services;

public class GenerateService : BackgroundService
{
    public readonly IServiceScopeFactory ScopeFactory;
    public ConcurrentQueue<PredictionRequest<Laionidev3Input>> Laionidev3RequestQueue;
    public ConcurrentQueue<PredictionRequest<PixrayInput>> PixrayRequestQueue;
    public ConcurrentQueue<PredictionRequest<Laionidev4Input>> Laionidev4RequestQueue;
    public ConcurrentQueue<PredictionRequest<SwinIRInput>> SwinIRRequestQueue;
    public ConcurrentQueue<PredictionRequest<LatentDiffusionInput>> LatentDiffusionQueue;
    public ConcurrentQueue<PredictionRequest<DeepMusicInput>> DeepMusicQueue;
    public ConcurrentQueue<PredictionRequest<VRTInput>> VRTQueue;

    public GenerateService(IServiceScopeFactory scopeFactory)
    {
        ScopeFactory = scopeFactory;
        Laionidev3RequestQueue = new ConcurrentQueue<PredictionRequest<Laionidev3Input>>();
        PixrayRequestQueue = new ConcurrentQueue<PredictionRequest<PixrayInput>>();
        Laionidev4RequestQueue = new ConcurrentQueue<PredictionRequest<Laionidev4Input>>();
        SwinIRRequestQueue = new ConcurrentQueue<PredictionRequest<SwinIRInput>>();
        LatentDiffusionQueue = new ConcurrentQueue<PredictionRequest<LatentDiffusionInput>>();        
        DeepMusicQueue = new ConcurrentQueue<PredictionRequest<DeepMusicInput>>();
        VRTQueue = new ConcurrentQueue<PredictionRequest<VRTInput>>();
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = ScopeFactory.CreateScope();
            if (Laionidev3RequestQueue.TryDequeue(out var request))
            {
                var discordClient = scope.ServiceProvider.GetRequiredService<DiscordSocketClient>();
                await this.GenerateLaionideV3(request, discordClient);
            }

            if (PixrayRequestQueue.TryDequeue(out var pixrayRequest))
            {
                var discordClient = scope.ServiceProvider.GetRequiredService<DiscordSocketClient>();
                await this.GeneratePixray(pixrayRequest, discordClient);
            }

            if (Laionidev4RequestQueue.TryDequeue(out var v4request))
            {
                var discordClient = scope.ServiceProvider.GetRequiredService<DiscordSocketClient>();
                await this.GenerateLaionideV4(v4request, discordClient);
            }

            if (LatentDiffusionQueue.TryDequeue(out var ldmRequest))
            {
                var discordClient = scope.ServiceProvider.GetRequiredService<DiscordSocketClient>();
                await this.GenerateLatentDiffusion(ldmRequest, discordClient);
            }

            if (DeepMusicQueue.TryDequeue(out var deepMusicRequest))
            {
                var discordClient = scope.ServiceProvider.GetRequiredService<DiscordSocketClient>();
                await this.GenerateDeepMusicViz(deepMusicRequest, discordClient);
            }

            if (VRTQueue.TryDequeue(out var vrtRequest))
            {
                var discordClient = scope.ServiceProvider.GetRequiredService<DiscordSocketClient>();
                await this.ProcessVRT(vrtRequest, discordClient);
            }

            if (SwinIRRequestQueue.TryDequeue(out var swinirRequest))
            {
                var discordClient = scope.ServiceProvider.GetRequiredService<DiscordSocketClient>();
                await this.ProcessSwinIR(swinirRequest, discordClient);
            }
        }
    }
    
    private async Task GenerateLaionideV3(PredictionRequest<Laionidev3Input> request, DiscordSocketClient client)
    {
        var guild_id = ulong.Parse(request.context.guild);
        var guild = client.GetGuild(guild_id);
        var channel_id = ulong.Parse(request.context.channel);
        var channel = guild.GetTextChannel(channel_id);
        var messageReference = new MessageReference(ulong.Parse(request.context.message), channel_id, guild_id);

        await client.SetGameAsync(request.input.prompt, null, ActivityType.Playing);
        
        var httpClient = new HttpClient();
        try
        {
            var result = await httpClient.PostAsJsonAsync("http://localhost:5000/predictions", request);
            if (!result.IsSuccessStatusCode)
            {
                await channel.SendMessageAsync($"Sorry, it's fucked. [{result.StatusCode}]");
                return;
            }
        }
        catch (Exception ex)
        {
            await channel.SendMessageAsync($"Help, I'm exploding. {ex.Message}");
            return;
        }

        var attachmentPath =
            $"{Directory.GetCurrentDirectory()}/result/{request.id}/upsample_predictions.png";

        await client.SetGameAsync("the art critics", null, ActivityType.Listening);
        await channel.SendFileAsync(attachmentPath, $"> {request.input.prompt} (Seed: {request.input.seed})", messageReference: messageReference);
    }

    private async Task GenerateLaionideV4(PredictionRequest<Laionidev4Input> request, DiscordSocketClient client)
    {
        var guild_id = ulong.Parse(request.context.guild);
        var guild = client.GetGuild(guild_id);
        var channel_id = ulong.Parse(request.context.channel);
        var channel = guild.GetTextChannel(channel_id);
        var messageReference = new MessageReference(ulong.Parse(request.context.message), channel_id, guild_id);

        await client.SetGameAsync(request.input.prompt, null, ActivityType.Playing);
        
        var httpClient = new HttpClient();
        try
        {
            var result = await httpClient.PostAsJsonAsync("http://localhost:5000/predictions", request, new JsonSerializerOptions(new JsonSerializerOptions(){ DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }));
            if (!result.IsSuccessStatusCode)
            {
                await channel.SendMessageAsync($"Sorry, it's fucked. [{result.StatusCode}]");
                return;
            }
        }
        catch (Exception ex)
        {
            await channel.SendMessageAsync($"Help, I'm exploding. {ex.Message}");
            return;
        }

        var attachmentPath =
            $"{Directory.GetCurrentDirectory()}/result/{request.id}/sr_predictions.png";

        await client.SetGameAsync("the art critics", null, ActivityType.Listening);
        await channel.SendFileAsync(attachmentPath, $"> {request.input.prompt} (Seed: {request.input.seed})", messageReference: messageReference);
    }

    
    private async Task GeneratePixray(PredictionRequest<PixrayInput> request, DiscordSocketClient client)
    {
        var startTime = DateTime.UtcNow;
        var guild_id = ulong.Parse(request.context.guild);
        var guild = client.GetGuild(guild_id);
        var channel_id = ulong.Parse(request.context.channel);
        var channel = guild.GetTextChannel(channel_id);
        var messageReference = new MessageReference(ulong.Parse(request.context.message), channel_id, guild_id);
        var attachmentPath = $"/home/palp/NightmareBot/result/{request.id}";
        
        if (string.IsNullOrWhiteSpace(request.input.seed))
            request.input.seed = request.id.ToString();
        string config = string.Empty;
        try {

            // re-host images locally
            Directory.CreateDirectory(attachmentPath);
            if (!string.IsNullOrWhiteSpace(request.input.init_image))
            {
                using var httpClient = new HttpClient();
                var imageData = await httpClient.GetByteArrayAsync(request.input.init_image);
                await File.WriteAllBytesAsync($"{attachmentPath}/init_image.png", imageData);
                request.input.init_image = $"http://localhost:49257/result/{request.id}/init_image.png";
            }

            // create pixray config
            using (var writer = new StringWriter()) 
            {
                writer.WriteLine($"prompts: {request.input.prompts}");
                writer.WriteLine($"drawer: {request.input.drawer}");
                if (!string.IsNullOrWhiteSpace(request.input.vqgan_model))
                    writer.WriteLine($"vqgan_model: {request.input.vqgan_model}");                            
                writer.WriteLine($"seed: {request.input.seed}");        
                if (!string.IsNullOrWhiteSpace(request.input.init_image))
                    writer.WriteLine($"init_image: {request.input.init_image}");
                if (!string.IsNullOrWhiteSpace(request.input.init_noise))
                    writer.WriteLine($"init_noise: {request.input.init_noise}");
                if (request.input.init_image_alpha.HasValue)
                    writer.WriteLine($"init_image_alpha: {request.input.init_image_alpha}");
                if (request.input.size != null)
                    writer.WriteLine($"size: [{string.Join(',', request.input.size)}]");
                if (request.input.num_cuts.HasValue)
                    writer.WriteLine($"num_cuts: {request.input.num_cuts}");
                writer.WriteLine($"quality: {request.input.quality}");
                if (request.input.iterations.HasValue)
                    writer.WriteLine($"iterations: {request.input.iterations}");
                writer.WriteLine($"batches: {request.input.batches}");
                if (!string.IsNullOrWhiteSpace(request.input.clip_models))
                    writer.WriteLine($"clip_models: {request.input.clip_models}");
                writer.WriteLine($"learning_rate: {request.input.learning_rate}");
                writer.WriteLine($"learning_rate_drops: [{string.Join(',', request.input.learning_rate_drops)}]");
                writer.WriteLine($"auto_stop: {request.input.auto_stop}");
                if (!string.IsNullOrWhiteSpace(request.input.filters))
                    writer.WriteLine($"filters: {request.input.filters}");
                if (!string.IsNullOrWhiteSpace(request.input.palette))
                    writer.WriteLine($"palette: {request.input.palette}");

                if (!string.IsNullOrWhiteSpace(request.input.custom_loss))
                {
                    writer.WriteLine($"custom_loss: {request.input.custom_loss}");                
                    if (request.input.custom_loss.Contains("saturation") || request.input.custom_loss.Contains("symmetry"))
                        writer.WriteLine($"saturation_weight: {request.input.saturation_weight}");
                    if (request.input.custom_loss.Contains("smoothness"))
                        writer.WriteLine($"smoothness_weight: {request.input.smoothness_weight}");
                    if (request.input.custom_loss.Contains("palette"))
                        writer.WriteLine($"palette_weight: {request.input.palette_weight}");
                }

                if (!string.IsNullOrWhiteSpace(request.input.image_prompts))
                {
                    writer.WriteLine($"image_prompts: {request.input.image_prompts}");
                    if (request.input.image_prompt_weight.HasValue)
                        writer.WriteLine($"image_prompt_weight: {request.input.image_prompt_weight}");
                    writer.WriteLine($"image_prompt_shuffle: {request.input.image_prompt_shuffle}");
                }

                if (!string.IsNullOrWhiteSpace(request.input.target_images))
                    writer.WriteLine($"target_images: {request.input.target_images}");
                config = writer.ToString();                
            }
            
            File.WriteAllText($"{attachmentPath}/config.yaml", config);
            Console.WriteLine(config);
            await client.SetGameAsync(request.input.prompts, null, ActivityType.Playing);
            await channel.SendMessageAsync($"Now processing: ```{config}```");
        } 
        catch (Exception ex) {
            await channel.SendMessageAsync($"You're Boned: {ex.Message}", messageReference: messageReference);
            return;
        }
        try
        {
            var process = new Process() 
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "./pixray.sh",
                    WorkingDirectory = "/home/palp/NightmareBot",
                    Arguments = $"\"{attachmentPath}\"",
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    UseShellExecute = true
                }
            };
            Console.WriteLine(process.StartInfo.Arguments);
            process.Start();
            string lastLine = string.Empty;
/*
            while (!process.StandardOutput.EndOfStream) 
            {
                lastLine = process.StandardOutput.ReadLine();
                Console.WriteLine(lastLine);
            }*/
            process.WaitForExit();

            //if (process.ExitCode != 0)
            if (!File.Exists($"{attachmentPath}/output.png"))
            {
                await channel.SendMessageAsync($"Sorry, it's fucked. [{lastLine}]");
                return;
            }
        }
        catch (Exception ex)
        {
            await channel.SendMessageAsync($"Help, I'm exploding. {ex.Message}");
            return;
        }        

        await client.SetGameAsync("the art critics", null, ActivityType.Listening);
        var title = $"> {request.input.prompts}\n({request.input.drawer}, {(DateTime.UtcNow - startTime).TotalSeconds} seconds)";
        if (File.Exists($"{attachmentPath}/steps/output.mp4"))
            await channel.SendFileAsync($"{attachmentPath}/steps/output.mp4", title);
        await channel.SendFileAsync($"{attachmentPath}/output.png", title, messageReference: messageReference);
    }


    public async Task GenerateLatentDiffusion(PredictionRequest<LatentDiffusionInput> request, DiscordSocketClient client)
    {
        var startTime = DateTime.UtcNow;
        var guild_id = ulong.Parse(request.context.guild);
        var guild = client.GetGuild(guild_id);
        var channel_id = ulong.Parse(request.context.channel);
        var channel = guild.GetTextChannel(channel_id);
        var messageReference = new MessageReference(ulong.Parse(request.context.message), channel_id, guild_id);
        var outputPath = $"/home/palp/NightmareBot/result/{request.id}";
        Directory.CreateDirectory(outputPath);


        var args = $"--prompt \"{request.input.prompt}\" --ddim_steps {request.input.ddim_steps} --ddim_eta {request.input.ddim_eta} --n_iter {request.input.n_iter} --n_samples {request.input.n_samples} --scale {request.input.scale} --H {request.input.height} --W {request.input.width}";
        if (request.input.plms)
            args += " --plms";        

        await channel.SendMessageAsync($"Latently diffusing: ```{args}```");

        args += $" --outdir \"{outputPath}\"";

        try
        {
            var process = new Process() 
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "./ldm.sh",
                    WorkingDirectory = "/home/palp/NightmareBot",
                    Arguments = $"{args}",
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    UseShellExecute = true
                }
            };
            Console.WriteLine(process.StartInfo.Arguments);
            process.Start();
            string lastLine = string.Empty;
/*
            while (!process.StandardOutput.EndOfStream) 
            {
                lastLine = process.StandardOutput.ReadLine();
                Console.WriteLine(lastLine);
            }*/
            process.WaitForExit();
        }
        catch (Exception ex)
        {
            await channel.SendMessageAsync($"Help, I'm exploding. {ex.Message}");
            return;
        }        


        await client.SetGameAsync("the art critics", null, ActivityType.Listening);
        var title = $"> {request.input.prompt}\n(latent-diffusion, {(DateTime.UtcNow - startTime).TotalSeconds} seconds)";
        
        foreach (var file in Directory.EnumerateFiles($"{outputPath}/samples")) 
        {
          await channel.SendFileAsync($"{file}", title, messageReference: messageReference);
        }         
    }

    public async Task GenerateDeepMusicViz(PredictionRequest<DeepMusicInput> request, DiscordSocketClient client)
    {
        var startTime = DateTime.UtcNow;
        var guild_id = ulong.Parse(request.context.guild);
        var guild = client.GetGuild(guild_id);
        var channel_id = ulong.Parse(request.context.channel);
        var channel = guild.GetTextChannel(channel_id);
        var messageReference = new MessageReference(ulong.Parse(request.context.message), channel_id, guild_id);
        var outputPath = $"/var/www/html/result/{request.id}";
        Directory.CreateDirectory(outputPath);

        // Download audio file
        if (string.IsNullOrWhiteSpace(request.input.song))
        {
            await channel.SendMessageAsync("No input audio.", messageReference: messageReference);
            return;
        }

        try 
        {
            using var httpClient = new HttpClient();
            var fileData = await httpClient.GetByteArrayAsync(request.input.song);
            var fileName = request.input.song.Substring(request.input.song.LastIndexOf('/') + 1);
            await File.WriteAllBytesAsync($"{outputPath}/{fileName}", fileData);
            request.input.song = $"{outputPath}/{fileName}";
        }
        catch (Exception ex) 
        {
            await channel.SendMessageAsync($"Could not download audio file: {ex.Message}", messageReference: messageReference);
            return;
        }

        var args = $"--song \"{request.input.song}\" --resolution {request.input.resolution} --pitch_sensitivity {request.input.pitch_sensitivity} --tempo_sensitivity {request.input.tempo_sensitivity} --depth {request.input.depth} --jitter {request.input.jitter} --truncation {request.input.truncation} --smooth_factor {request.input.smooth_factor} --batch_size {request.input.batch_size}";
        if (request.input.duration.HasValue)
            args += $" --duration {request.input.duration}";
        if (!string.IsNullOrWhiteSpace(request.input.classes))
            args += $" --classes {request.input.classes}";
        if (request.input.num_classes.HasValue)
            args += $" --num_classes {request.input.num_classes}";
        if (request.input.sort_classes_by_power)
            args += $" --sort_classes_by_power 1";

        await channel.SendMessageAsync($"Dreaming of a song: ```{args}```");

        args += $" --output_file \"{outputPath}/output.mp4\"";

        try
        {
            var process = new Process() 
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "./deepmusic.sh",
                    WorkingDirectory = "/home/palp/NightmareBot",
                    Arguments = $"{args}",
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    UseShellExecute = true
                }
            };
            Console.WriteLine(process.StartInfo.Arguments);
            process.Start();
            string lastLine = string.Empty;
/*
            while (!process.StandardOutput.EndOfStream) 
            {
                lastLine = process.StandardOutput.ReadLine();
                Console.WriteLine(lastLine);
            }*/
            process.WaitForExit();
        }
        catch (Exception ex)
        {
            await channel.SendMessageAsync($"Help, I'm exploding. {ex.Message}");
            return;
        }        


        await client.SetGameAsync("the art critics", null, ActivityType.Listening);
        var url = $"https://nightmarebot.com/result/{request.id}/output.mp4";
        var message = $"> {request.input.song}\n(deep-music-viz, {(DateTime.UtcNow - startTime).TotalSeconds} seconds)\n{url}";

        if (File.Exists($"{outputPath}/output.mp4"))
            await channel.SendMessageAsync(message, messageReference: messageReference);
        else
            await channel.SendMessageAsync("Sorry, it broke.", messageReference: messageReference);
    }

    public async Task ProcessSwinIR(PredictionRequest<SwinIRInput> request, DiscordSocketClient client)
    {
        var startTime = DateTime.UtcNow;
        var guild_id = ulong.Parse(request.context.guild);
        var guild = client.GetGuild(guild_id);
        var channel_id = ulong.Parse(request.context.channel);
        var channel = guild.GetTextChannel(channel_id);
        var messageReference = new MessageReference(ulong.Parse(request.context.message), channel_id, guild_id);
        var basePath = $"/home/palp/NightmareBot/enhance/{request.id}";
        var inputPath = basePath + "/lq";
        var outputPath = basePath + "/results";
        Directory.CreateDirectory(inputPath);


        using var httpClient = new HttpClient();

        int ix = 0;
        foreach (var url in request.input.ImageUrls) 
        {
            var imageData = await httpClient.GetByteArrayAsync(url);
            string fileName = $"{ix++}.{url.Substring(url.Length -3)}";
            await File.WriteAllBytesAsync($"{inputPath}/{fileName}", imageData);
        }

        try
        {
            var process = new Process() 
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "./swinir.sh",
                    WorkingDirectory = "/home/palp/NightmareBot",
                    Arguments = $"\"{basePath}\"",
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    UseShellExecute = true
                }
            };
            Console.WriteLine(process.StartInfo.Arguments);
            process.Start();
            string lastLine = string.Empty;
/*
            while (!process.StandardOutput.EndOfStream) 
            {
                lastLine = process.StandardOutput.ReadLine();
                Console.WriteLine(lastLine);
            }*/
            process.WaitForExit();
        }
        catch (Exception ex)
        {
            await channel.SendMessageAsync($"Help, I'm exploding. {ex.Message}");
            return;
        }        

        await client.SetGameAsync("the art critics", null, ActivityType.Listening);
        var title = $"Restoration took {(DateTime.UtcNow - startTime).TotalSeconds} seconds";

//        List<Embed> embeds = new List<Embed>();
        
        foreach (var file in Directory.EnumerateFiles(outputPath)) 
        {
          await channel.SendFileAsync($"{file}", title, messageReference: messageReference);
        } 
    }

    public async Task ProcessVRT(PredictionRequest<VRTInput> request, DiscordSocketClient client)
    {
        var startTime = DateTime.UtcNow;
        var guild_id = ulong.Parse(request.context.guild);
        var guild = client.GetGuild(guild_id);
        var channel_id = ulong.Parse(request.context.channel);
        var channel = guild.GetTextChannel(channel_id);
        var messageReference = new MessageReference(ulong.Parse(request.context.message), channel_id, guild_id);
        var basePath = $"/home/palp/NightmareBot/enhance/{request.id}";
        var inputPath = basePath + "/lq";
        var outputPath = basePath + "/results";
        Directory.CreateDirectory(inputPath);

        // Download video file
        if (string.IsNullOrWhiteSpace(request.input.video))
        {
            await channel.SendMessageAsync("No input video.", messageReference: messageReference);
            return;
        }

        try 
        {
            using var httpClient = new HttpClient();
            var fileData = await httpClient.GetByteArrayAsync(request.input.video);
            var fileName = request.input.video.Substring(request.input.video.LastIndexOf('/') + 1);
            await File.WriteAllBytesAsync($"{outputPath}/{fileName}", fileData);
            request.input.video = $"{outputPath}/{fileName}";
        }
        catch (Exception ex) 
        {
            await channel.SendMessageAsync($"Could not download audio file: {ex.Message}", messageReference: messageReference);
            return;
        }

        try
        {
            var process = new Process() 
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "./vrt.sh",
                    WorkingDirectory = "/home/palp/NightmareBot",
                    Arguments = $"\"{basePath}\"",
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    UseShellExecute = true
                }
            };
            Console.WriteLine(process.StartInfo.Arguments);
            process.Start();
            string lastLine = string.Empty;
/*
            while (!process.StandardOutput.EndOfStream) 
            {
                lastLine = process.StandardOutput.ReadLine();
                Console.WriteLine(lastLine);
            }*/
            process.WaitForExit();
        }
        catch (Exception ex)
        {
            await channel.SendMessageAsync($"Help, I'm exploding. {ex.Message}");
            return;
        }        

        await client.SetGameAsync("the art critics", null, ActivityType.Listening);
        var title = $"Restoration took {(DateTime.UtcNow - startTime).TotalSeconds} seconds";

//        List<Embed> embeds = new List<Embed>();
        
        foreach (var file in Directory.EnumerateFiles(outputPath)) 
        {
          await channel.SendFileAsync($"{file}", title, messageReference: messageReference);
        } 
    }


}
