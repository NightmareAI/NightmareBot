using Discord.Interactions;
using NightmareBot.Models;

namespace NightmareBot.Modals
{
    public class MajestyDiffusionModal : IModal
    {
        public string Title => "Majesty Diffusion Dream";

        [ModalTextInput("prompt", Discord.TextInputStyle.Paragraph, placeholder: "nightmarebot loves you, oil on canvas", minLength: 1, maxLength: 200)]
        public string? Prompt { get; set; }

        [ModalTextInput("latent_diffusion_model")]
        public string Model { get; set; } = "finetuned";

        [ModalTextInput("init_image")]
        public string? InitImage { get; set; } = null;

    }
}
