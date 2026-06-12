# Totem BFF C#

BFF em ASP.NET Core para o sistema de autoatendimento.

## Requisitos

- .NET SDK 8

## Configuracao

As URLs ficam em `appsettings.json`:

- `Dev`: `https://services-hml.grupopardini.com.br/csp`
- `Homol`: `https://services-hml.grupopardini.com.br/csp`
- `Prod`: `https://servicesmob-prd.grupopardini.com.br/csp`

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

Contexto e servicos disponiveis:

```text
GET http://127.0.0.1:8000/api/terminal-context?hostName=ihpmgaimtotem1
```

Autenticar cliente por CPF, senha e data de nascimento:

```text
POST http://127.0.0.1:8000/api/client-token
```

Consultar cliente:

```text
GET http://127.0.0.1:8000/api/client?cpf=30488918030
GET http://127.0.0.1:8000/api/client?carteirinha=123456
GET http://127.0.0.1:8000/api/client?cpf=30488918030&carteirinha=123456
```

Cadastrar cliente:

```text
POST http://127.0.0.1:8000/api/client
```

Editar cliente:

```text
PUT http://127.0.0.1:8000/api/client?id=8066747022
```

Fluxo interno:

1. Autenticacao de cliente por CPF/senha/data usa `POST /mobileRest/Cliente/Token/`.
2. Consulta, cadastro e edicao de cliente usam `/digitalRest/autoAtendimento/cliente`.
3. Chamadas de AutoAtendimento usam token de `GET /digitalRest/agendamentoExterno/oauth/token/`.
4. O token e mantido em cache usando `expires_in`, descontando `TokenCacheSafetySeconds`.
