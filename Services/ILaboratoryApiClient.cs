using System.Text.Json;

namespace TotemBff.Services;

public interface ILaboratoryApiClient
{
    Task<JsonDocument> GetVisualIdentityAsync(string hostName, CancellationToken cancellationToken);

    Task<JsonDocument> GetTerminalContextAsync(string hostName, CancellationToken cancellationToken);

    Task<JsonDocument> AuthenticateClientAsync(
        string cpf,
        string password,
        string birthDate,
        CancellationToken cancellationToken);

    Task<JsonDocument> UpdateClientAsync(
        string clientId,
        JsonElement payload,
        CancellationToken cancellationToken);
}
