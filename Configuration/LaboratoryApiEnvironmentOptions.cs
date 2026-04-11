using System.ComponentModel.DataAnnotations;

namespace TotemBff.Configuration;

public sealed class LaboratoryApiEnvironmentOptions
{
    [Required]
    public string BaseUrl { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
