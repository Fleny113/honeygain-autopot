namespace HoneyGainAutoRewardPot;

public sealed record HoneyGainApplicationSettings
{
    public const string SectionName = "HoneyGain";

    public string UserAgent { get; set; } = null!;
    public string Token { get; set; } = null!;
    public string WebhookUrl { get; set; } = null!;
}
