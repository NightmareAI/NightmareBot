using Discord.Interactions;
using NightmareBot.Models;

namespace NightmareBot.Modals
{
    public class LatentDiffusionModal : IModal
    {
        public string Title => "Latent Diffusion Dream";

        [ModalTextInput("prompt", Discord.TextInputStyle.Paragraph, placeholder: "nightmarebot loves you, oil on canvas", minLength: 1, maxLength: 200)]
        public string? Prompt { get; set; }
    }
}
