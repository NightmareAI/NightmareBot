using Discord.Interactions;
using NightmareBot.Models;

namespace NightmareBot.Modals
{
    public class MajestyAdvancedModal : IModal
    {
        public string Title => "Majesty Diffusion";

        [ModalTextInput("prompt", Discord.TextInputStyle.Paragraph, placeholder: "nightmarebot loves you, oil on canvas", minLength: 1, maxLength: 200)]
        public string? Prompt { get; set; }

        [ModalTextInput("settings")]
        public string? Settings { get; set; }
    }
}
