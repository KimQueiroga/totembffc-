using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TotemBff.Configuration;

namespace TotemBff.Services;

public sealed class LaboratoryApiClient : ILaboratoryApiClient
{
    private static readonly Regex UnquotedScalarValueRegex = new(
        @"(""[^""\\]*(?:\\.[^""\\]*)*""\s*:\s*)(?![""{\[]|null\b|true\b|false\b)([^,}\]]*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TrailingStatusStringRegex = new(
        @"""status""\s*:\s*""(?:[^""\\]|\\.)*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly JsonDocumentOptions TolerantJsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private readonly IMemoryCache _cache;
    private readonly HttpClient _httpClient;
    private readonly ILogger<LaboratoryApiClient> _logger;
    private readonly LaboratoryApiOptions _options;

    public LaboratoryApiClient(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<LaboratoryApiOptions> options,
        ILogger<LaboratoryApiClient> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<JsonDocument> GetVisualIdentityAsync(string hostName, CancellationToken cancellationToken)
    {
        return await SendAuthorizedGetAsync(
            hostName,
            "visual",
            "identidade visual",
            cancellationToken);
    }

    public async Task<JsonDocument> GetTerminalContextAsync(string hostName, CancellationToken cancellationToken)
    {
        return await SendAuthorizedGetAsync(
            hostName,
            "contexto",
            "contexto do terminal",
            cancellationToken);
    }

    public async Task<JsonDocument> AuthenticateClientAsync(
        string cpf,
        string password,
        string birthDate,
        CancellationToken cancellationToken)
    {
        var environment = GetActiveEnvironment();
        var token = await GetLegacyBearerTokenAsync(environment, cancellationToken);
        var url = $"{environment.BaseUrl.TrimEnd('/')}/mobileRest/Cliente/Token/";
        var payload = JsonSerializer.Serialize(new
        {
            authKey = cpf,
            authPass = password,
            authKeyType = "1",
            birthDate,
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Client token request failed. StatusCode={StatusCode}, Body={Body}",
                (int)response.StatusCode,
                content);

            throw new LaboratoryApiException($"Falha ao autenticar cliente. HTTP {(int)response.StatusCode}.");
        }

        return JsonDocument.Parse(content);
    }

    public async Task<JsonDocument> GetClientAsync(
        string? cpf,
        string? cardNumber,
        CancellationToken cancellationToken)
    {
        var environment = GetActiveEnvironment();
        var token = await GetBearerTokenAsync(environment, cancellationToken);
        var queryParameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(cpf))
        {
            queryParameters.Add($"cpf={Uri.EscapeDataString(cpf)}");
        }

        if (!string.IsNullOrWhiteSpace(cardNumber))
        {
            queryParameters.Add($"carteirinha={Uri.EscapeDataString(cardNumber)}");
        }

        var url = $"{environment.BaseUrl.TrimEnd('/')}/digitalRest/autoAtendimento/cliente?{string.Join("&", queryParameters)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Client query request failed. StatusCode={StatusCode}, Body={Body}",
                (int)response.StatusCode,
                content);

            throw new LaboratoryApiException($"Falha ao consultar cliente. HTTP {(int)response.StatusCode}.");
        }

        return JsonDocument.Parse(content);
    }

    public async Task<JsonDocument> CreateClientAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        var environment = GetActiveEnvironment();
        var token = await GetBearerTokenAsync(environment, cancellationToken);
        var url = $"{environment.BaseUrl.TrimEnd('/')}/digitalRest/autoAtendimento/cliente";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(payload.GetRawText(), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Client create request failed. StatusCode={StatusCode}, Body={Body}",
                (int)response.StatusCode,
                content);

            throw new LaboratoryApiException($"Falha ao cadastrar cliente. HTTP {(int)response.StatusCode}.");
        }

        return string.IsNullOrWhiteSpace(content)
            ? JsonDocument.Parse("{}")
            : JsonDocument.Parse(content);
    }

    public async Task<JsonDocument> UpdateClientAsync(
        string clientId,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        var environment = GetActiveEnvironment();
        var token = await GetBearerTokenAsync(environment, cancellationToken);
        var url = $"{environment.BaseUrl.TrimEnd('/')}/digitalRest/autoAtendimento/cliente?id={Uri.EscapeDataString(clientId)}";

        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(payload.GetRawText(), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Client update request failed. ClientId={ClientId}, StatusCode={StatusCode}, Body={Body}",
                clientId,
                (int)response.StatusCode,
                content);

            throw new LaboratoryApiException($"Falha ao atualizar cliente. HTTP {(int)response.StatusCode}.");
        }

        return string.IsNullOrWhiteSpace(content)
            ? JsonDocument.Parse("{}")
            : JsonDocument.Parse(content);
    }

    public async Task<JsonDocument> GetPreAttendanceAsync(
        string clientId,
        string? clientToken,
        CancellationToken cancellationToken)
    {
        var environment = GetActiveEnvironment();
        ValidateCredentials(environment);

        var apiToken = string.IsNullOrWhiteSpace(clientToken)
            ? await GetServiceTokenAsync(environment, cancellationToken)
            : clientToken;
        var url = $"{environment.BaseUrl.TrimEnd('/')}/pscRest/preAtendimento/consultar/";
        var clientCode = long.TryParse(clientId, out var numericClientId)
            ? (object)numericClientId
            : clientId;
        var payload = JsonSerializer.Serialize(new
        {
            codigoCliente = clientCode,
            validade = "S",
            atendidos = "N",
            tipoAtendimento = "P",
            origem = environment.Username,
            token = apiToken,
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Pre-attendance request failed. ClientId={ClientId}, StatusCode={StatusCode}, Body={Body}",
                clientId,
                (int)response.StatusCode,
                content);

            throw new LaboratoryApiException($"Falha ao consultar pre atendimento. HTTP {(int)response.StatusCode}.");
        }

        return ParsePreAttendanceContent(content, clientId);
    }

    public async Task<JsonDocument> SearchExamsAsync(
        string keyword,
        string? clientToken,
        string? healthPlan,
        string? unit,
        CancellationToken cancellationToken)
    {
        var environment = GetActiveEnvironment();
        ValidateCredentials(environment);

        var apiToken = string.IsNullOrWhiteSpace(clientToken)
            ? await GetServiceTokenAsync(environment, cancellationToken)
            : clientToken;

        var queryHealthPlan = string.IsNullOrWhiteSpace(healthPlan)
            ? "UNREG"
            : healthPlan.Trim();
        var queryUnit = string.IsNullOrWhiteSpace(unit) ? "1" : unit.Trim();

        var url = $"{environment.BaseUrl.TrimEnd('/')}/attendanceRest/v1/basic/exams?keyword={Uri.EscapeDataString(keyword)}&healthPlan={Uri.EscapeDataString(queryHealthPlan)}&unit={Uri.EscapeDataString(queryUnit)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("token", apiToken);
        request.Headers.Add("origem", environment.Username);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            _logger.LogWarning(
                "Exam search request returned 400. Keyword={Keyword}, HealthPlan={HealthPlan}, Unit={Unit}, Body={Body}",
                keyword,
                queryHealthPlan,
                queryUnit,
                content);

            throw new LaboratoryApiException(
                $"Falha ao consultar exames. HTTP 400. {ExtractExamSearchErrorMessage(content)}",
                StatusCodes.Status400BadRequest);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Exam search request failed. Keyword={Keyword}, HealthPlan={HealthPlan}, Unit={Unit}, StatusCode={StatusCode}, Body={Body}",
                keyword,
                queryHealthPlan,
                queryUnit,
                (int)response.StatusCode,
                content);

            throw new LaboratoryApiException($"Falha ao consultar exames. HTTP {(int)response.StatusCode}.");
        }

        return JsonDocument.Parse(content);
    }

    private static string ExtractExamSearchErrorMessage(string content)
    {
        try
        {
            var jsonStart = content.IndexOf('{');
            if (jsonStart >= 0)
            {
                var jsonBody = content.Substring(jsonStart);
                using var document = JsonDocument.Parse(jsonBody);
                if (document.RootElement.TryGetProperty("error", out var errorElement) &&
                    errorElement.ValueKind == JsonValueKind.Object &&
                    errorElement.TryGetProperty("message", out var messageElement) &&
                    messageElement.ValueKind == JsonValueKind.String)
                {
                    return messageElement.GetString() ?? string.Empty;
                }
            }
        }
        catch
        {
            // ignore parse failures
        }

        return content;
    }

    private JsonDocument ParsePreAttendanceContent(string content, string clientId)
    {
        try
        {
            return JsonDocument.Parse(content, TolerantJsonOptions);
        }
        catch (JsonException exception)
        {
            var repairedContent = RepairTrailingStatusString(
                RepairPreAttendanceJson(content));
            var parseExceptions = new List<(string Content, JsonException Exception)>();

            if (repairedContent != content)
            {
                try
                {
                    return JsonDocument.Parse(repairedContent, TolerantJsonOptions);
                }
                catch (JsonException repairedException)
                {
                    parseExceptions.Add((repairedContent, repairedException));
                }
            }

            var trimmedContent = TrimToLastBalancedJson(content);
            if (trimmedContent != content)
            {
                try
                {
                    return JsonDocument.Parse(trimmedContent, TolerantJsonOptions);
                }
                catch (JsonException trimmedException)
                {
                    parseExceptions.Add((trimmedContent, trimmedException));
                }
            }

            if (TryAppendMissingContainerClosings(content, out var appendedContent) && appendedContent != content)
            {
                try
                {
                    return JsonDocument.Parse(appendedContent, TolerantJsonOptions);
                }
                catch (JsonException appendedException)
                {
                    parseExceptions.Add((appendedContent, appendedException));
                }
            }

            if (TryExtractFirstBalancedJson(content, out var extractedContent) && extractedContent != trimmedContent)
            {
                try
                {
                    return JsonDocument.Parse(extractedContent, TolerantJsonOptions);
                }
                catch (JsonException extractedException)
                {
                    parseExceptions.Add((extractedContent, extractedException));
                }
            }

            var details = DescribeJsonException(exception);
            _logger.LogWarning(
                exception,
                "Pre-attendance response is invalid JSON. ClientId={ClientId}, BodyLength={BodyLength}, Error={Error}, Snippet={Snippet}, Tail={Tail}",
                clientId,
                content.Length,
                details,
                GetJsonErrorSnippet(content, exception),
                GetJsonEndSnippet(content));

            if (parseExceptions.Count == 1)
            {
                var repairedException = parseExceptions[0].Exception;
                var repairedDetails = DescribeJsonException(repairedException);

                _logger.LogWarning(
                    repairedException,
                    "Pre-attendance response is invalid JSON after repair/trim. ClientId={ClientId}, BodyLength={BodyLength}, Error={Error}, Snippet={Snippet}",
                    clientId,
                    content.Length,
                    repairedDetails,
                    GetJsonErrorSnippet(parseExceptions[0].Content, repairedException));
            }
            else if (parseExceptions.Count > 1)
            {
                var first = parseExceptions[0];
                var second = parseExceptions[1];
                _logger.LogWarning(
                    second.Exception,
                    "Pre-attendance response is invalid JSON after repair and trim. ClientId={ClientId}, BodyLength={BodyLength}, FirstError={FirstError}, SecondError={SecondError}",
                    clientId,
                    content.Length,
                    DescribeJsonException(first.Exception),
                    DescribeJsonException(second.Exception));
            }

            throw new LaboratoryApiException($"Resposta de pre atendimento da API do laboratorio nao e um JSON valido. {details}");
        }
    }

    private static string DescribeJsonException(JsonException exception)
    {
        var line = exception.LineNumber is long lineNumber
            ? lineNumber + 1
            : 0;
        var column = exception.BytePositionInLine is long bytePosition
            ? bytePosition + 1
            : 0;

        return $"Linha {line}, coluna {column}: {exception.Message}";
    }

    private static string GetJsonErrorSnippet(string content, JsonException exception)
    {
        var index = GetJsonErrorIndex(content, exception) ?? Math.Max(0, content.Length - 240);
        var start = Math.Max(0, index - 120);
        var length = Math.Min(content.Length - start, 240);

        return content.Substring(start, length)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static int? GetJsonErrorIndex(string content, JsonException exception)
    {
        if (exception.LineNumber is not long lineNumber ||
            exception.BytePositionInLine is not long bytePosition)
        {
            return null;
        }

        var currentLine = 0L;
        var lineStart = 0;

        for (var index = 0; index < content.Length && currentLine < lineNumber; index++)
        {
            if (content[index] != '\n')
            {
                continue;
            }

            currentLine++;
            lineStart = index + 1;
        }

        if (currentLine != lineNumber)
        {
            return null;
        }

        return Math.Min(content.Length, lineStart + (int)Math.Min(bytePosition, int.MaxValue));
    }

    private static string RepairPreAttendanceJson(string content)
    {
        var repaired = UnquotedScalarValueRegex.Replace(content, match =>
        {
            var prefix = match.Groups[1].Value;
            var value = match.Groups[2].Value.Trim();

            if (string.IsNullOrEmpty(value))
            {
                return $"{prefix}null";
            }

            return $"{prefix}{(IsJsonScalar(value) ? value : JsonSerializer.Serialize(value))}";
        });

        return TrimToLastBalancedJson(repaired);
    }

    private static string RepairTrailingStatusString(string content)
    {
        if (!TrailingStatusStringRegex.IsMatch(content))
        {
            return content;
        }

        var missingContainerClosings = GetMissingContainerClosings(content);

        return $"{content}\"{missingContainerClosings}";
    }

    private static string TrimToLastBalancedJson(string content)
    {
        var inString = false;
        var escaped = false;
        var stack = new Stack<char>();
        var lastBalancedIndex = -1;

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (character == '"')
            {
                inString = true;
                continue;
            }

            if (character == '{')
            {
                stack.Push('}');
                continue;
            }

            if (character == '[')
            {
                stack.Push(']');
                continue;
            }

            if (character == '}' || character == ']')
            {
                if (stack.Count > 0 && stack.Peek() == character)
                {
                    stack.Pop();
                    if (stack.Count == 0)
                    {
                        lastBalancedIndex = index;
                    }
                }
            }
        }

        if (lastBalancedIndex >= 0 && lastBalancedIndex < content.Length - 1)
        {
            return content.Substring(0, lastBalancedIndex + 1);
        }

        return content;
    }

    private static bool TryExtractFirstBalancedJson(string content, out string json)
    {
        var inString = false;
        var escaped = false;
        var stack = new Stack<char>();
        var startIndex = -1;
        var lastBalancedIndex = -1;

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (character == '"')
            {
                inString = true;
                continue;
            }

            if (startIndex < 0)
            {
                if (character == '{' || character == '[')
                {
                    startIndex = index;
                    stack.Push(character == '{' ? '}' : ']');
                }

                continue;
            }

            if (character == '{')
            {
                stack.Push('}');
                continue;
            }

            if (character == '[')
            {
                stack.Push(']');
                continue;
            }

            if (character == '}' || character == ']')
            {
                if (stack.Count > 0 && stack.Peek() == character)
                {
                    stack.Pop();
                    if (stack.Count == 0)
                    {
                        lastBalancedIndex = index;
                        break;
                    }
                }
            }
        }

        if (startIndex >= 0 && lastBalancedIndex >= startIndex)
        {
            json = content.Substring(startIndex, lastBalancedIndex - startIndex + 1);
            return true;
        }

        json = string.Empty;
        return false;
    }

    private static string GetJsonEndSnippet(string content)
    {
        const int MaxLength = 120;

        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        var length = Math.Min(MaxLength, content.Length);
        return content.Substring(content.Length - length).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static bool TryAppendMissingContainerClosings(string content, out string json)
    {
        var missingClosings = GetMissingContainerClosings(content);

        if (string.IsNullOrEmpty(missingClosings))
        {
            json = string.Empty;
            return false;
        }

        json = content + missingClosings;
        return true;
    }

    private static string GetMissingContainerClosings(string content)
    {
        var missingClosings = new Stack<char>();
        var inString = false;
        var escaped = false;

        foreach (var character in content)
        {
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    missingClosings.Push('}');
                    break;
                case '[':
                    missingClosings.Push(']');
                    break;
                case '}':
                case ']':
                    if (missingClosings.Count == 0 ||
                        missingClosings.Peek() != character)
                    {
                        return string.Empty;
                    }

                    missingClosings.Pop();
                    break;
            }
        }

        return new string(missingClosings.ToArray());
    }

    private static bool IsJsonScalar(string value)
    {
        try
        {
            using var _ = JsonDocument.Parse($"[{value}]", TolerantJsonOptions);

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<JsonDocument> SendAuthorizedGetAsync(
        string hostName,
        string resource,
        string errorDescription,
        CancellationToken cancellationToken)
    {
        var environment = GetActiveEnvironment();
        var token = await GetBearerTokenAsync(environment, cancellationToken);
        var url = $"{environment.BaseUrl.TrimEnd('/')}/digitalRest/autoAtendimento/{resource}?hostName={Uri.EscapeDataString(hostName)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Laboratory API request failed. Resource={Resource}, StatusCode={StatusCode}, Body={Body}",
                resource,
                (int)response.StatusCode,
                content);

            throw new LaboratoryApiException($"Falha ao consultar {errorDescription}. HTTP {(int)response.StatusCode}.");
        }

        return JsonDocument.Parse(content);
    }

    private async Task<string> GetBearerTokenAsync(LaboratoryApiEnvironmentOptions environment, CancellationToken cancellationToken)
    {
        var cacheKey = $"laboratory_api_token:{_options.ActiveEnvironment}";

        if (_cache.TryGetValue(cacheKey, out string? cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
        {
            return cachedToken;
        }

        ValidateCredentials(environment);

        var url = $"{environment.BaseUrl.TrimEnd('/')}/digitalRest/agendamentoExterno/oauth/token/";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{environment.Username}:{environment.Password}")));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Token request failed. StatusCode={StatusCode}, Body={Body}",
                (int)response.StatusCode,
                content);

            throw new LaboratoryApiException($"Falha ao autenticar na API do laboratorio. HTTP {(int)response.StatusCode}.");
        }

        using var payload = JsonDocument.Parse(content);
        var token = ExtractToken(payload.RootElement);
        var ttlSeconds = ExtractTtlSeconds(payload.RootElement);

        _cache.Set(cacheKey, token, TimeSpan.FromSeconds(ttlSeconds));

        return token;
    }

    private async Task<string> GetLegacyBearerTokenAsync(LaboratoryApiEnvironmentOptions environment, CancellationToken cancellationToken)
    {
        var cacheKey = $"laboratory_api_legacy_token:{_options.ActiveEnvironment}";

        if (_cache.TryGetValue(cacheKey, out string? cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
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

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Legacy token request failed. StatusCode={StatusCode}, Body={Body}",
                (int)response.StatusCode,
                content);

            throw new LaboratoryApiException($"Falha ao autenticar na API legada do laboratorio. HTTP {(int)response.StatusCode}.");
        }

        using var payload = JsonDocument.Parse(content);
        var token = ExtractToken(payload.RootElement);
        var ttlSeconds = ExtractTtlSeconds(payload.RootElement);

        _cache.Set(cacheKey, token, TimeSpan.FromSeconds(ttlSeconds));

        return token;
    }

    private async Task<string> GetServiceTokenAsync(
        LaboratoryApiEnvironmentOptions environment,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"laboratory_api_service_token:{_options.ActiveEnvironment}";

        if (_cache.TryGetValue(cacheKey, out string? cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
        {
            return cachedToken;
        }

        ValidateCredentials(environment);

        var url = $"{environment.BaseUrl.TrimEnd('/')}/pscRest/geraTokenServico/";
        var payload = JsonSerializer.Serialize(new
        {
            authKey = environment.Username,
            authPass = environment.Password,
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Service token request failed. StatusCode={StatusCode}, Body={Body}",
                (int)response.StatusCode,
                content);

            throw new LaboratoryApiException($"Falha ao gerar token de servico. HTTP {(int)response.StatusCode}.");
        }

        using var responsePayload = JsonDocument.Parse(content);
        var tokenResult = responsePayload.RootElement.TryGetProperty("TokenResult", out var value)
            && value.ValueKind == JsonValueKind.Object
            ? value
            : responsePayload.RootElement;
        var token = GetString(tokenResult, "token");

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new LaboratoryApiException("Resposta de token de servico sem token.");
        }

        var ttlMinutes = GetInt(tokenResult, "tempoExpiracaoMin") ?? 5;
        var ttlSeconds = Math.Max(30, (ttlMinutes * 60) - _options.TokenCacheSafetySeconds);

        _cache.Set(cacheKey, token, TimeSpan.FromSeconds(ttlSeconds));

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
