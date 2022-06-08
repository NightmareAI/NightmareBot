using System.Text.RegularExpressions;

namespace NightmareBot.Models;

public class MajestyDiffusionInput : IGeneratorInput
{
    public string[] clip_prompts { get; set; } = { "nightmarebot loves you" };
    public string[] latent_prompts { get; set; } = { "nightmarebot loves you" };
    public string[] latent_negatives { get; set; } = { };
    public string[] image_prompts { get; set; } = { };

    public int n_samples { get; set; } = 1;
    public int height { get; set; } = 640;
    public int width { get; set; } = 448;
    public float latent_diffusion_guidance_scale { get; set; } = 8.82f;
    public int clip_guidance_scale { get; set; } = 5000;
    public int how_many_batches { get; set; } = 1;
    public int aesthetic_loss_scale { get; set; } = 200;
    public bool augment_cuts { get; set; } = true;
    public string? init_image { get; set; } = "";
    public float starting_timestep { get; set; } = 0.9f;
    public string? init_mask { get; set; }
    public int init_scale { get; set; } = 0;
    public float init_brightness { get; set; } = 0.0f;
    public float init_noise { get; set; } = 0.6f;
    public string[] clip_load_list { get; set; } = {
//        "[clip - mlfoundations - ViT-B-32--openai]",
        "[clip - mlfoundations - ViT-B-16--openai]",
//        "[clip - mlfoundations - ViT-B-16--laion400m_e32]",
        "[clip - mlfoundations - ViT-L-14--openai]",
//        "[clip - mlfoundations - RN50x4--openai]",
//        "[clip - mlfoundations - RN50x64--openai]",
//        "[clip - mlfoundations - RN50x16--openai]",
//        "[clip - mlfoundations - ViT-L-14-336--openai]",
//        "[clip - mlfoundations - ViT-B-16-plus-240--laion400m_e32]",
        "[clip - mlfoundations - ViT-B-32--laion2b_e16]",
//        "[clip - sajjjadayobi - clipfa]",
//        "[clip - navervision - kelip_ViT-B/32]",
//        "[cloob - crowsonkb - cloob_laion_400m_vit_b_16_32_epochs]"
};
    public bool use_cond_fn { get; set; } = true;
    public string custom_schedule_setting { get; set; } = @"[
        [50, 1000, 8],
        [5,200,5],
#        'gfpgan:1.0',
#        'latent:1.5',
#        [1, 50, 5]
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

    #Init image settings
    starting_timestep = {starting_timestep}
    init_scale = {init_scale} 
    init_brightness = {init_brightness}
    init_noise = {init_noise}

    [advanced_settings]
    #Add CLIP Guidance and all the flavors or just run normal Latent Diffusion
    use_cond_fn = {use_cond_fn}

    #Custom schedules for cuts. Check out the schedules documentation here
    custom_schedule_setting = {custom_schedule_setting}

    #Cut settings
    clamp_index = [2, 1.4]
    cut_overview = [8]*500 + [4]*500
    cut_innercut = [0]*500 + [4]*500
    cut_ic_pow = 0.1
    cut_icgray_p = [0.1]*300 + [0]*1000
    cutn_batches = 1
    cut_blur_n = [0] * 400 + [0] * 600
    cut_blur_kernel = 3
    range_index = [0]*1000
    active_function = 'softsign'
    ths_method = 'softsign'
    tv_scales = [600] * 1 + [50] * 1 + [0] * 2
    latent_tv_loss = True

    #If you uncomment this line you can schedule the CLIP guidance across the steps. Otherwise the clip_guidance_scale will be used
    clip_guidance_schedule = [5000]*1000
    
    #Apply symmetric loss (force simmetry to your results)
    symmetric_loss_scale = 0 

    #Latent Diffusion Advanced Settings
    #Use when latent upscale to correct satuation problem
    scale_div = 1
    #Magnify grad before clamping by how many times
    opt_mag_mul = 15
    opt_ddim_eta = 1.5
    opt_eta_end = 1.2
    opt_temperature = 0.95

    #Grad advanced settings
    grad_center = False
    #Lower value result in more coherent and detailed result, higher value makes it focus on more dominent concept
    grad_scale=0.5
    score_modifier = True
    threshold_percentile = 0.9
    threshold = 1.2
    var_index = [0] * 1000

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
    compress_steps = 0
    compress_factor = 0.1
    punish_steps = 0
    punish_factor = 0.8
";
}