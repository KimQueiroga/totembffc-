using Microsoft.Extensions.Options;
using TotemBff.Configuration;
using TotemBff.Endpoints;
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

        if (origins.Contains("*"))
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();

            return;
        }

        policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<ILaboratoryApiClient, LaboratoryApiClient>();
builder.Services
    .AddOptions<LaboratoryApiOptions>()
    .Bind(builder.Configuration.GetSection(LaboratoryApiOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var app = builder.Build();

app.UseCors("FlutterLocal");

app.MapTerminalEndpoints();

app.Run();
