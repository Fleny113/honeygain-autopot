namespace HoneyGainAutoRewardPot;

public sealed record HoneyGainApplicationSettings
{
    public const string SectionName = "HoneyGain";

    public required string Token { get; set; }
    public required string WebhookUrl { get; set; }
}
