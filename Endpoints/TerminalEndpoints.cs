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
                using var response = string.IsNullOrWhiteSpace(request.ClientCode)
                    ? await laboratoryApi.AuthenticateClientAsync(
                        request.Cpf?.Trim() ?? string.Empty,
                        request.Password,
                        request.BirthDate?.Trim() ?? string.Empty,
                        cancellationToken)
                    : await laboratoryApi.AuthenticateClientByCodeAsync(
                        request.ClientCode.Trim(),
                        request.Password,
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

        api.MapGet("/client", async (
            string? cpf,
            string? carteirinha,
            ILaboratoryApiClient laboratoryApi,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(cpf) && string.IsNullOrWhiteSpace(carteirinha))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["client"] = new[] { "CPF ou carteirinha deve ser informado." },
                });
            }

            try
            {
                using var response = await laboratoryApi.GetClientAsync(
                    cpf?.Trim(),
                    carteirinha?.Trim(),
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

        api.MapPost("/client", async (
            JsonElement payload,
            ILaboratoryApiClient laboratoryApi,
            CancellationToken cancellationToken) =>
        {
            try
            {
                using var response = await laboratoryApi.CreateClientAsync(
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
            string? clientToken,
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
                using var response = await laboratoryApi.GetPreAttendanceAsync(
                    clientId.Trim(),
                    clientToken,
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

        api.MapGet("/exams", async (
            string keyword,
            string? clientToken,
            string? healthPlan,
            string? unit,
            ILaboratoryApiClient laboratoryApi,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["keyword"] = new[] { "Keyword deve ser informado." },
                });
            }

            try
            {
                using var response = await laboratoryApi.SearchExamsAsync(
                    keyword.Trim(),
                    clientToken,
                    healthPlan,
                    unit,
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
                var statusCode = exception.StatusCode == StatusCodes.Status400BadRequest
                    ? StatusCodes.Status400BadRequest
                    : StatusCodes.Status502BadGateway;

                return Results.Problem(exception.Message, statusCode: statusCode);
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
        var hasCpf = !string.IsNullOrWhiteSpace(request.Cpf);
        var hasClientCode = !string.IsNullOrWhiteSpace(request.ClientCode);

        if (!hasCpf && !hasClientCode)
        {
            errors["client"] = new[] { "CPF ou codigo do cliente deve ser informado." };
        }

        if (hasCpf && hasClientCode)
        {
            errors["client"] = new[] { "Informe CPF ou codigo do cliente, nao ambos." };
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors["password"] = new[] { "Senha deve ser informada." };
        }

        if (hasCpf && string.IsNullOrWhiteSpace(request.BirthDate))
        {
            errors["birthDate"] = new[] { "Data de nascimento deve ser informada." };
        }

        return errors;
    }

    private sealed record ClientTokenRequest(string? Cpf, string Password, string? BirthDate, string? ClientCode);
}
