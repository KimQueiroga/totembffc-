# Totem BFF C#

BFF em ASP.NET Core para o sistema de autoatendimento.

## Requisitos

- .NET SDK 8

## Configuracao

As URLs ficam em `appsettings.json`:

- `Dev`: `https://services-hml.grupopardini.com.br/csp`
- `Homol`: `https://services-hml.grupopardini.com.br/csp`
- `Prod`: `https://services-prd.grupopardini.com.br/csp`

Credenciais locais devem ficar em `appsettings.Local.json`, que e ignorado pelo Git. Use `appsettings.Local.example.json` como referencia.

Durante o desenvolvimento o CORS aceita qualquer origem (`Cors:AllowedOrigins: ["*"]`), porque o Flutter Web pode subir em portas diferentes quando o `--web-port` nao e fixado. Antes de publicar, troque `*` pelos hosts reais do terminal.

Exemplo:

```json
{
  "LaboratoryApi": {
    "ActiveEnvironment": "Dev",
    "Environments": {
      "Dev": {
        "Username": "%PASSELIVRE",
        "Password": "sua-senha"
      }
    }
  }
}
```

Em homologacao/producao, prefira variaveis de ambiente ou secret manager:

```powershell
$env:LaboratoryApi__ActiveEnvironment = "Prod"
$env:LaboratoryApi__Environments__Prod__Username = "usuario"
$env:LaboratoryApi__Environments__Prod__Password = "senha"
```

## Desenvolvimento

Executar:

```bash
dotnet run --urls http://127.0.0.1:8000
```

O projeto nao depende de pacotes NuGet externos. Se a maquina corporativa bloquear `https://api.nuget.org/v3/index.json`, ainda assim o build deve funcionar porque o BFF usa apenas o framework compartilhado do ASP.NET Core instalado com o SDK.

Health check:

```text
GET http://127.0.0.1:8000/api/health
```

Identidade visual:

```text
GET http://127.0.0.1:8000/api/terminal-visual?hostName=ihpmgaimtotem1
```

Fluxo interno:

1. `POST /digitalRest/autenticacao/token` com Basic Auth.
2. Cache do `access_token` usando `expires_in`, descontando `TokenCacheSafetySeconds`.
3. `GET /digitalRest/autoAtendimento/visual?hostName=...` com `Authorization: Bearer <token>`.
