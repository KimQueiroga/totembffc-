using Microsoft.Extensions.Options;
using TotemBff.Configuration;
using TotemBff.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddCors(options =>
{
    options.AddPolicy("FlutterLocal", policy =>
    {
        var origins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();

        policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<ILaboratoryApiClient, LaboratoryApiClient>();
builder.Services
    .AddOptions<LaboratoryApiOptions>()
    .Bind(builder.Configuration.GetSection(LaboratoryApiOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("FlutterLocal");

app.MapGet("/api/health", (IOptions<LaboratoryApiOptions> options) => Results.Ok(new
{
    status = "ok",
    service = "Totem BFF C#",
    environment = options.Value.ActiveEnvironment,
}));

app.MapGet("/api/terminal-visual", async (
    string hostName,
    ILaboratoryApiClient laboratoryApi,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(hostName) || !TerminalHostNameValidator.IsValid(hostName))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["hostName"] = new[] { "hostName deve conter apenas letras, numeros, ponto, hifen ou underline." },
        });
    }

    try
    {
        var response = await laboratoryApi.GetVisualIdentityAsync(hostName, cancellationToken);

        return Results.Content(response.RootElement.GetRawText(), "application/json");
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

app.Run();
