using System.Text.Json;
using Microsoft.Extensions.Options;
using TotemBff.Configuration;
using TotemBff.Services;

namespace TotemBff.Endpoints;

public static class TerminalEndpoints
{
    private const string InvalidHostNameMessage = "hostName deve conter apenas letras, numeros, ponto, hifen ou underline.";

    public static IEndpointRouteBuilder MapTerminalEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/health", (IOptions<LaboratoryApiOptions> options) => Results.Ok(new
        {
            status = "ok",
            service = "Totem BFF C#",
            environment = options.Value.ActiveEnvironment,
        }));

        api.MapGet("/terminal-visual", async (
            string hostName,
            ILaboratoryApiClient laboratoryApi,
            CancellationToken cancellationToken) =>
        {
            return await ForwardLaboratoryResponseAsync(
                hostName,
                laboratoryApi.GetVisualIdentityAsync,
                cancellationToken);
        });

        api.MapGet("/terminal-context", async (
            string hostName,
            ILaboratoryApiClient laboratoryApi,
            CancellationToken cancellationToken) =>
        {
            return await ForwardLaboratoryResponseAsync(
                hostName,
                laboratoryApi.GetTerminalContextAsync,
                cancellationToken);
        });

        return app;
    }

    private static async Task<IResult> ForwardLaboratoryResponseAsync(
        string hostName,
        Func<string, CancellationToken, Task<JsonDocument>> request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hostName) || !TerminalHostNameValidator.IsValid(hostName))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["hostName"] = new[] { InvalidHostNameMessage },
            });
        }

        try
        {
            using var response = await request(hostName, cancellationToken);
            var json = response.RootElement.GetRawText();

            return Results.Content(json, "application/json");
        }
        catch (LaboratoryApiConfigurationException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (LaboratoryApiException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
