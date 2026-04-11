using System.Text.Json;

namespace TotemBff.Services;

public interface ILaboratoryApiClient
{
    Task<JsonDocument> GetVisualIdentityAsync(string hostName, CancellationToken cancellationToken);
}
