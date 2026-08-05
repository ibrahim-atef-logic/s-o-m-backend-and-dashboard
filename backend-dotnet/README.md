# LogicRetail .NET API

Flutter-compatible Sales Orders backend (ASP.NET Core) replacing the Node.js API.

## Run

```bash
cd backend-dotnet/src/LogicRetail.Api
dotnet run
```

Listens on **http://0.0.0.0:3000** (same as the Flutter emulator `10.0.2.2:3000`).

## Configuration

`appsettings.json` / `appsettings.Development.json` or environment variables:

| Env | Setting |
|-----|---------|
| `DYNAMICS_MODE` | `Mock` or `Live` |
| `FINOPS_BASE_URL` | D365 F&O base URL |
| `AZURE_TENANT_ID` / `AZURE_CLIENT_ID` / `AZURE_CLIENT_SECRET` | App registration |
| `JWT_SECRET` | HMAC secret (min ~32 chars) |
| `STORE_PATH` | JSON store path (default `data/store.json`) |

Development defaults to **Live** credentials from `appsettings.Development.json` (trial sandbox). Use `Dynamics:Mode=Mock` for offline tests.

## Auth (ERM-style)

```http
POST /api/v1/auth/login
{ "company": "usmf", "personnelNumber": "1006", "password": "123" }
```

Validates against Dynamics entity `LogicRetailUserSetup_BI` with:

`PersonnelNumber` + `Password` + `IsActivated` + `GroupCompany`.

JWT is scoped to that company. No separate company-select screen.

## Contract tests

Covers every HTTP endpoint (health, auth, catalog, line jobs) via `WebApplicationFactory` in Mock mode:

```bash
cd backend-dotnet
dotnet test tests/LogicRetail.Api.Tests
```

## Unit tests

Service-level coverage (`AuthService`, `LineJobsService`, `JsonFileStore`) against Mock Dynamics:

```bash
dotnet test tests/LogicRetail.Unit.Tests
```

Or run all:

```bash
dotnet test
```

## Reference

D365 MSAL / OData patterns inspired by `reference/erm-api` (`D365Authenticator`, `D365ODataClient`).
