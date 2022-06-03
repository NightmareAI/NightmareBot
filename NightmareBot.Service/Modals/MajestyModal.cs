using Discord.Interactions;
using NightmareBot.Models;

namespace NightmareBot.Modals
{
    public class MajestyDiffusionModal : IModal
    {
        public string Title => "Latent Diffusion Dream";

        [ModalTextInput("prompt", Discord.TextInputStyle.Paragraph, placeholder: "nightmarebot loves you, oil on canvas", minLength: 1, maxLength: 200)]
        public string? Prompt { get; set; }

        [ModalTextInput("negative_prompt")]
        public string NegativePrompt { get; set; } = "low quality image";

        [ModalTextInput("init_image")]
        public string? InitImage { get; set; } = null;

    }
}
