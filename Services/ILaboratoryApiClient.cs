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

    Task<JsonDocument> AuthenticateClientByCodeAsync(
        string clientCode,
        string password,
        CancellationToken cancellationToken);

    Task<JsonDocument> GetClientAsync(
        string? cpf,
        string? cardNumber,
        CancellationToken cancellationToken);

    Task<JsonDocument> CreateClientAsync(
        JsonElement payload,
        CancellationToken cancellationToken);

    Task<JsonDocument> UpdateClientAsync(
        string clientId,
        JsonElement payload,
        CancellationToken cancellationToken);

    Task<JsonDocument> GetPreAttendanceAsync(
        string clientId,
        string? clientToken,
        CancellationToken cancellationToken);

    Task<JsonDocument> SearchExamsAsync(
        string keyword,
        string? clientToken,
        string? healthPlan,
        string? unit,
        CancellationToken cancellationToken);

    Task<JsonDocument> CheckExamQuestionnaireAsync(
        string examId,
        string? clientToken,
        CancellationToken cancellationToken);

    Task<JsonDocument> GetQuestionnairesAsync(
        string material,
        string exam,
        string gender,
        string birthDate,
        string? clientToken,
        CancellationToken cancellationToken);

    Task<JsonDocument> GetRelationshipsAsync(CancellationToken cancellationToken);

    Task<JsonDocument> PrintResultByBarcodeAsync(
        string barcode,
        string? printer,
        CancellationToken cancellationToken);
}
