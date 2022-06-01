using Discord.Commands;
using System.Text.Json.Serialization;

namespace NightmareBot.Models;

[NamedArgumentType]
public class PixrayInput : IGeneratorInput
{
    public string prompts { get; set; } = "";

    public string drawer { get; set; } = "vqgan";

    public string seed { get; set; }

    [JsonPropertyName("image_prompts")]
    public string image_prompts { get; set; }

    public string init_image { get; set; }

    public int? init_image_alpha { get; set; }

    public string init_noise { get; set; }
    public string quality { get; set; } = "normal";
    public int? num_cuts { get; set; } = 30;
    public IEnumerable<int> size { get; set; } = new [] { 240, 320 };
    public float? image_prompt_weight { get; set; }
    public bool image_prompt_shuffle { get; set; }
    public string target_images { get; set; }
    public int? iterations { get; set; }
    public int batches { get; set; } = 1;
    public double learning_rate { get; set; } = .2;
    public IEnumerable<int> learning_rate_drops { get; set; } = new[] { 75 };
    public bool auto_stop { get; set; } = false;
    public string clip_models { get; set; } = "RN50x4,ViT-B/16,ViT-B/32";
    public string filters { get; set; }
    public string palette { get; set; }
    public string custom_loss { get; set; }
    public int smoothness_weight { get; set; } = 1;
    public int saturation_weight { get; set; } = 1;
    public int palette_weight { get; set; } = 1;
    public string vqgan_model { get; set; } 

    // static rendered config
    public string settings { get; set; }

    // dynamic rendered config
    public string config
    {
        get
        {
            using (var writer = new StringWriter())
            {

                writer.WriteLine($"prompts: {prompts}");
                writer.WriteLine($"drawer: {drawer}");
                if (!string.IsNullOrWhiteSpace(vqgan_model))
                    writer.WriteLine($"vqgan_model: {vqgan_model}");
                writer.WriteLine($"seed: {seed}");
                if (!string.IsNullOrWhiteSpace(init_image))
                {
                    writer.WriteLine($"init_image: {init_image}");
                    if (init_image_alpha.HasValue)
                        writer.WriteLine($"init_image_alpha: {init_image_alpha}");
                }
                if (!string.IsNullOrWhiteSpace(init_noise))
                    writer.WriteLine($"init_noise: {init_noise}");
                if (size != null)
                    writer.WriteLine($"size: [{string.Join(',', size)}]");
                if (num_cuts.HasValue)
                    writer.WriteLine($"num_cuts: {num_cuts}");
                writer.WriteLine($"quality: {quality}");
                if (iterations.HasValue)
                    writer.WriteLine($"iterations: {iterations}");
                writer.WriteLine($"batches: {batches}");
                if (!string.IsNullOrWhiteSpace(clip_models))
                    writer.WriteLine($"clip_models: {clip_models}");
                writer.WriteLine($"learning_rate: {learning_rate}");
                writer.WriteLine($"learning_rate_drops: [{string.Join(',', learning_rate_drops)}]");
                writer.WriteLine($"auto_stop: {auto_stop}");
                if (!string.IsNullOrWhiteSpace(filters))
                    writer.WriteLine($"filters: {filters}");
                if (!string.IsNullOrWhiteSpace(palette))
                    writer.WriteLine($"palette: {palette}");

                if (!string.IsNullOrWhiteSpace(custom_loss))
                {
                    writer.WriteLine($"custom_loss: {custom_loss}");
                    if (custom_loss.Contains("saturation") || custom_loss.Contains("symmetry"))
                        writer.WriteLine($"saturation_weight: {saturation_weight}");
                    if (custom_loss.Contains("smoothness"))
                        writer.WriteLine($"smoothness_weight: {smoothness_weight}");
                    if (custom_loss.Contains("palette"))
                        writer.WriteLine($"palette_weight: {palette_weight}");
                }

                if (!string.IsNullOrWhiteSpace(image_prompts))
                {
                    writer.WriteLine($"image_prompts: {image_prompts}");
                    if (image_prompt_weight.HasValue)
                        writer.WriteLine($"image_prompt_weight: {image_prompt_weight}");
                    writer.WriteLine($"image_prompt_shuffle: {image_prompt_shuffle}");
                }

                if (!string.IsNullOrWhiteSpace(target_images))
                    writer.WriteLine($"target_images: {target_images}");
                return writer.ToString();

            }

        }
    }
}