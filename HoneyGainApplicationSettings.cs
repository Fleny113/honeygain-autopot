using System.ComponentModel.DataAnnotations;

namespace HoneyGainAutoPot;

public sealed record HoneyGainApplicationSettings
{
    public const string SectionName = "HoneyGain";

    [Required] public required string Token { get; init; }
    [Required] public required string WebhookUrl { get; init; }
    public bool DontLog { get; init; } = false;
}
