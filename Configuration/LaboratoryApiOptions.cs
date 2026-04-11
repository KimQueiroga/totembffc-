using System.ComponentModel.DataAnnotations;

namespace TotemBff.Configuration;

public sealed class LaboratoryApiOptions
{
    public const string SectionName = "LaboratoryApi";

    [Required]
    public string ActiveEnvironment { get; init; } = "Dev";

    [Range(0, 3600)]
    public int TokenCacheSafetySeconds { get; init; } = 60;

    [Range(60, 86400)]
    public int TokenFallbackTtlSeconds { get; init; } = 1500;

    [Required]
    public Dictionary<string, LaboratoryApiEnvironmentOptions> Environments { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
