namespace NightmareBot.Common
{
    public class RequestState
    {
        public string? request_id { get; set; }
        public string? request_type { get; set; }
        public string? preset_name { get; set; }
        public DateTime? created_at { get; set; }
        public DateTime? queued_at { get; set; }
        public DateTime? started_at { get; set; }
        public DateTime? last_updated { get; set; }
        public DateTime? completed_at { get; set; }
        public string? status { get; set; }
        public bool? is_active { get; set; }
        public bool? success { get; set; }
        public string? error { get; set; }
        public string? prompt { get; set; }
        public string? gpt_prompt { get; set; }
        public string[]? selected_images { get; set; }
        public IEnumerable<ResponseImage>? response_images { get; set; }
        public DiscordContext? discord_context { get; set; }
    }
}
