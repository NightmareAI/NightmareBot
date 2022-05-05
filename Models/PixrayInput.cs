using Discord.Commands;
using System.Text.Json.Serialization;

namespace NightmareBot.Models;

[NamedArgumentType]
public class PixrayInput : IGeneratorInput
{
    public string prompts { get; set; } = "";

    public string drawer { get; set; } = "vqgan";

    public string seed { get; set; }
    
    public string image_prompts {get; set; }

    public string init_image { get; set; }

    public int? init_image_alpha { get; set; }

    public string init_noise { get; set; }
    public string quality { get; set; } = "normal";
    public int? num_cuts {get; set; }
    public IEnumerable<int> size { get; set; } = null;
    public float? image_prompt_weight { get; set; }
    public bool image_prompt_shuffle { get; set; } 
    public string target_images { get; set; }
    public int? iterations { get; set; }
    public int batches { get; set; } = 1;
    public double learning_rate {get; set; } = .2;
    public IEnumerable<int> learning_rate_drops {get; set;} = new[] {75};
    public bool auto_stop {get; set;} = true;
    public string clip_models {get; set;}
    public string filters {get; set;}
    public string palette {get; set;}
    public string custom_loss {get; set;}
    public int smoothness_weight {get; set;} = 1;
    public int saturation_weight {get; set;} = 1;
    public int palette_weight {get; set;} = 1; 
    public string vqgan_model {get; set;}
}
