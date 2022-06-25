using System.Text.RegularExpressions;

namespace NightmareBot.Models;

public class MajestyDiffusionInput : IGeneratorInput
{
    public string[] clip_prompts { get; set; } = { "nightmarebot loves you" };
    public string[] latent_prompts { get; set; } = { "nightmarebot loves you" };
    public string[] latent_negatives { get; set; } = { };
    public string[] image_prompts { get; set; } = { };

    public string latent_diffusion_model { get; set; } = "finetuned";

    public int n_samples { get; set; } = 1;
    public int height { get; set; } = 384;
    public int width { get; set; } = 256;
    public float latent_diffusion_guidance_scale { get; set; } = 12f;
    public int clip_guidance_scale { get; set; } = 16000;
    public string? clip_guidance_schedule { get; set; }
    public int how_many_batches { get; set; } = 1;
    public int aesthetic_loss_scale { get; set; } = 400;
    public bool augment_cuts { get; set; } = true;
    public string? init_image { get; set; } = "";
    public float starting_timestep { get; set; } = 0.9f;
    public string? init_mask { get; set; }
    public int init_scale { get; set; } = 1000;
    public float init_brightness { get; set; } = 0.0f;
    public string clamp_index { get; set; } = "[2.4, 2.1]";
    public string cut_overview { get; set;  } ="[8]*500 + [4]*500";
    public string cut_innercut { get; set; } = "[0]*500 + [4]*500";
    public float cut_ic_pow { get; set; } = 0.2f;
    public string cut_icgray_p { get; set; } = "[0.1]*300 + [0]*1000";
    public string cut_blur_n { get; set; } = "[0] * 300 + [0] * 1000";
    public int cut_blur_kernel { get; set; } = 3;
    public string range_index { get; set; } = "[0]*200+[5e4]*400+[0]*1000";
    public string ths_method { get; set; } = "softsign";
    public string active_function { get; set; } = "softsign";
    public string tv_scales { get; set; } = "[600] * 1 + [50] * 1 + [0] * 2";
    public float symmetric_loss_scale { get; set; } = 0f;
    public int cutn_batches { get; set; } = 1;
    public int opt_mag_mul { get; set; } = 20;
    public bool opt_plms { get; set; } = false;
    public float opt_ddim_eta { get; set; } = 1.3f;
    public float opt_eta_end { get; set; } = 1.1f;
    public float scale_div { get; set; } = 1;
    public float opt_temperature { get; set; } = 0.98f;
    public float grad_scale { get; set; } = 0.25f;
    public float threshold_percentile { get; set; } = 0.85f;
    public int threshold { get; set; } = 1;
    public string var_index { get; set; } = "[2]*300+[0]*700";
    public float var_range { get; set; } = 0.5f;
    public string mean_index { get; set; } = "[0]*400+[0]*600";
    public float mean_range { get; set; } = 0.75f;
    public string[] clip_load_list { get; set; } = {
//        "[clip - mlfoundations - ViT-B-32--openai]",
        "[clip - mlfoundations - ViT-B-16--openai]",
//        "[clip - mlfoundations - ViT-B-16--laion400m_e32]",
//        "[clip - mlfoundations - ViT-L-14--openai]",
//        "[clip - mlfoundations - RN50x4--openai]",
//        "[clip - mlfoundations - RN50x64--openai]",
//        "[clip - mlfoundations - RN50x16--openai]",
        "[clip - mlfoundations - ViT-L-14-336--openai]",
//        "[clip - mlfoundations - ViT-B-16-plus-240--laion400m_e32]",
        "[clip - mlfoundations - ViT-B-32--laion2b_e16]",
//        "[clip - sajjjadayobi - clipfa]",
//        "[clip - navervision - kelip_ViT-B/32]",
//        "[cloob - crowsonkb - cloob_laion_400m_vit_b_16_32_epochs]"
};
    public bool use_cond_fn { get; set; } = true;
    public string custom_schedule_setting { get; set; } = @"[
        [50, 1000, 8],
        'gfpgan:2.0','scale:.9','noise:.75',
        [5,200,4],
        ]";

    public string settings => @$"
    #This settings file can be loaded back to Latent Majesty Diffusion. If you like your setting consider sharing it to the settings library at https://github.com/multimodalart/MajestyDiffusion
    [clip_list]
    perceptors = ['{string.Join("', '",clip_load_list)}']
    
    [basic_settings]
    #Perceptor things
    clip_prompts = ['{string.Join("', '",clip_prompts.Select(p => p.Replace("'", "\\'")))}']
    latent_prompts = ['{string.Join("', '",latent_prompts.Select(p => p.Replace("'", "\\'")))}']
    latent_negatives = ['{string.Join("', '",latent_negatives.Select(p => p.Replace("'", "\\'")))}']
    {(image_prompts.Length > 0 ? @$"image_prompts = ['{string.Join("', '",image_prompts.Select(p => p.Replace("'", "\\'")))}']" : "")}
    width = {width}
    height = {height}
    latent_diffusion_guidance_scale = {latent_diffusion_guidance_scale}
    clip_guidance_scale = {clip_guidance_scale}
    aesthetic_loss_scale = {aesthetic_loss_scale}
    augment_cuts={augment_cuts}
    latent_diffusion_model= '{latent_diffusion_model}'


    #Init image settings
    starting_timestep = {starting_timestep}
    init_scale = {init_scale} 
    init_brightness = {init_brightness}

    [advanced_settings]
    #Add CLIP Guidance and all the flavors or just run normal Latent Diffusion
    use_cond_fn = {use_cond_fn}

    #Custom schedules for cuts. Check out the schedules documentation here
    custom_schedule_setting = {custom_schedule_setting}

    #Cut settings
    clamp_index = {clamp_index}
    cut_overview = {cut_overview}
    cut_innercut = {cut_innercut}
    cut_ic_pow = {cut_ic_pow}
    cut_icgray_p = {cut_icgray_p}
    cutn_batches = {cutn_batches}
    cut_blur_n = {cut_blur_n}
    cut_blur_kernel = {cut_blur_kernel}
    range_index = {range_index}
    active_function = '{active_function}'
    ths_method = '{ths_method}'
    tv_scales = {tv_scales}
    
    {(!string.IsNullOrEmpty(clip_guidance_schedule) ? $"clip_guidance_schedule = {clip_guidance_schedule}" : "")}
    
    #Apply symmetric loss (force simmetry to your results)
    symmetric_loss_scale = {symmetric_loss_scale} 

    #Latent Diffusion Advanced Settings
    #Use when latent upscale to correct satuation problem
    scale_div = {scale_div}
    #Magnify grad before clamping by how many times
    opt_mag_mul = {opt_mag_mul}
    opt_plms = {opt_plms}
    opt_ddim_eta = {opt_ddim_eta}
    opt_eta_end = {opt_eta_end}
    opt_temperature = {opt_temperature}

    #Grad advanced settings
    grad_center = False
    #Lower value result in more coherent and detailed result, higher value makes it focus on more dominent concept
    grad_scale={grad_scale}
    score_modifier = True
    threshold_percentile = {threshold_percentile}
    threshold = {threshold}
    var_index = {var_index}
    var_range = {var_range}
    mean_index = {mean_index}
    mean_range = {mean_range}

    #Init image advanced settings
    init_rotate=False
    mask_rotate=False
    init_magnitude = 0.15

    #More settings
    RGB_min = -0.95
    RGB_max = 0.95
    #How to pad the image with cut_overview
    padargs = {{'mode': 'constant', 'value': -1}} 
    flip_aug=False
    
    #Experimental aesthetic embeddings, work only with OpenAI ViT-B/32 and ViT-L/14
    experimental_aesthetic_embeddings = True
    #How much you want this to influence your result
    experimental_aesthetic_embeddings_weight = 0.3
    #9 are good aesthetic embeddings, 0 are bad ones
    experimental_aesthetic_embeddings_score = 8

    # For fun dont change except if you really know what your are doing
    grad_blur = False
    compress_steps = 200
    compress_factor = 0.1
    punish_steps = 200
    punish_factor = 0.5
".Replace("%","%%");
}