using Discord.Interactions;
using NightmareBot.Models;

namespace NightmareBot.Modals
{
    public class PixrayModal : IModal
    {
        public string Title => "Pixray Dream";
        
        [ModalTextInput("prompts", Discord.TextInputStyle.Paragraph, placeholder: "nightmarebot loves you | oil on canvas", minLength: 1, maxLength: 200)]
        public string? Prompts { get; set; }

        [ModalTextInput("drawer", placeholder: "vqgan")]
        public string? Drawer { get; set; }

        [ModalTextInput("init_image", placeholder: "png image URL", minLength: 0, initValue: "")]
        public string? InitImage { get; set; }

        [ModalTextInput("seed")]
        public string? Seed { get; set; }

        [ModalTextInput("settings")]
        public string? Settings { get; set; }

    }
}
