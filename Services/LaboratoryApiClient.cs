using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TotemBff.Configuration;

namespace TotemBff.Services;

public sealed class LaboratoryApiClient(
    HttpClient httpClient,
    IMemoryCache cache,
    IOptions<LaboratoryApiOptions> options,
    ILogger<LaboratoryApiClient> logger) : ILaboratoryApiClient
{
    private readonly LaboratoryApiOptions _options = options.Value;

    public async Task<JsonDocument> GetVisualIdentityAsync(string hostName, CancellationToken cancellationToken)
    {
        var environment = GetActiveEnvironment();
        var token = await GetBearerTokenAsync(environment, cancellationToken);
        var url = $"{environment.BaseUrl.TrimEnd('/')}/digitalRest/autoAtendimento/visual?hostName={Uri.EscapeDataString(hostName)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Visual identity request failed. StatusCode={StatusCode}, Body={Body}",
                (int)response.StatusCode,
                content);

            throw new LaboratoryApiException($"Falha ao consultar identidade visual. HTTP {(int)response.StatusCode}.");
        }

        return JsonDocument.Parse(content);
    }

    private async Task<string> GetBearerTokenAsync(LaboratoryApiEnvironmentOptions environment, CancellationToken cancellationToken)
    {
        var cacheKey = $"laboratory_api_token:{_options.ActiveEnvironment}";

        if (cache.TryGetValue(cacheKey, out string? cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
        {
            return cachedToken;
        }

        ValidateCredentials(environment);

        var url = $"{environment.BaseUrl.TrimEnd('/')}/digitalRest/autenticacao/token";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{environment.Username}:{environment.Password}")));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Token request failed. StatusCode={StatusCode}, Body={Body}",
                (int)response.StatusCode,
                content);

            throw new LaboratoryApiException($"Falha ao autenticar na API do laboratorio. HTTP {(int)response.StatusCode}.");
        }

        using var payload = JsonDocument.Parse(content);
        var token = ExtractToken(payload.RootElement);
        var ttlSeconds = ExtractTtlSeconds(payload.RootElement);

        cache.Set(cacheKey, token, TimeSpan.FromSeconds(ttlSeconds));

        return token;
    }

    private LaboratoryApiEnvironmentOptions GetActiveEnvironment()
    {
        if (!_options.Environments.TryGetValue(_options.ActiveEnvironment, out var environment))
        {
            throw new LaboratoryApiConfigurationException($"Ambiente da API do laboratorio invalido: {_options.ActiveEnvironment}.");
        }

        if (string.IsNullOrWhiteSpace(environment.BaseUrl))
        {
            throw new LaboratoryApiConfigurationException($"Base URL da API do laboratorio nao configurada para {_options.ActiveEnvironment}.");
        }

        return environment;
    }

    private void ValidateCredentials(LaboratoryApiEnvironmentOptions environment)
    {
        if (string.IsNullOrWhiteSpace(environment.Username) || string.IsNullOrWhiteSpace(environment.Password))
        {
            throw new LaboratoryApiConfigurationException($"Usuario e senha da API do laboratorio devem ser configurados para {_options.ActiveEnvironment}.");
        }
    }

    private static string ExtractToken(JsonElement payload)
    {
        var token = GetString(payload, "access_token")
            ?? GetString(payload, "bearerToken")
            ?? GetString(payload, "token")
            ?? GetString(payload, "jwt")
            ?? GetString(payload, "data", "access_token")
            ?? GetString(payload, "data", "bearerToken")
            ?? GetString(payload, "data", "token");

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new LaboratoryApiException("Resposta de autenticacao sem token.");
        }

        return token;
    }

    private int ExtractTtlSeconds(JsonElement payload)
    {
        var expiresIn = GetInt(payload, "expires_in")
            ?? GetInt(payload, "expiresIn")
            ?? GetInt(payload, "expires")
            ?? GetInt(payload, "data", "expires_in")
            ?? GetInt(payload, "data", "expiresIn")
            ?? _options.TokenFallbackTtlSeconds;

        return Math.Max(60, expiresIn - _options.TokenCacheSafetySeconds);
    }

    private static string? GetString(JsonElement payload, string propertyName)
    {
        return payload.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string? GetString(JsonElement payload, string parentName, string propertyName)
    {
        return payload.TryGetProperty(parentName, out var parent) && parent.ValueKind == JsonValueKind.Object
            ? GetString(parent, propertyName)
            : null;
    }

    private static int? GetInt(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)
            ? number
            : null;
    }

    private static int? GetInt(JsonElement payload, string parentName, string propertyName)
    {
        return payload.TryGetProperty(parentName, out var parent) && parent.ValueKind == JsonValueKind.Object
            ? GetInt(parent, propertyName)
            : null;
    }
}
