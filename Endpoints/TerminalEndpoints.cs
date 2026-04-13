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

        api.MapPost("/client-token", async (
            ClientTokenRequest request,
            ILaboratoryApiClient laboratoryApi,
            CancellationToken cancellationToken) =>
        {
            var errors = ValidateClientTokenRequest(request);

            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            try
            {
                using var response = await laboratoryApi.AuthenticateClientAsync(
                    request.Cpf.Trim(),
                    request.Password,
                    request.BirthDate.Trim(),
                    cancellationToken);
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
        });

        api.MapPut("/client", async (
            string id,
            JsonElement payload,
            ILaboratoryApiClient laboratoryApi,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["id"] = new[] { "ID do cliente deve ser informado." },
                });
            }

            try
            {
                using var response = await laboratoryApi.UpdateClientAsync(
                    id.Trim(),
                    payload,
                    cancellationToken);
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
        });

        api.MapGet("/pre-attendance", async (
            string clientId,
            ILaboratoryApiClient laboratoryApi,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clientId"] = new[] { "Codigo do cliente deve ser informado." },
                });
            }

            try
            {
                var json = await laboratoryApi.GetPreAttendanceAsync(
                    clientId.Trim(),
                    cancellationToken);

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

    private static Dictionary<string, string[]> ValidateClientTokenRequest(ClientTokenRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Cpf))
        {
            errors["cpf"] = new[] { "CPF deve ser informado." };
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors["password"] = new[] { "Senha deve ser informada." };
        }

        if (string.IsNullOrWhiteSpace(request.BirthDate))
        {
            errors["birthDate"] = new[] { "Data de nascimento deve ser informada." };
        }

        return errors;
    }

    private sealed record ClientTokenRequest(string Cpf, string Password, string BirthDate);
}
