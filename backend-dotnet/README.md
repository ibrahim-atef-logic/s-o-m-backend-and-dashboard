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

## Auth (mobile activation)

```http
POST /api/v1/auth/login
{ "company": "logic-trial", "personnelNumber": "1006", "password": "123" }
```

`company` is the **admin registry / environment key** (unlocks D365 credentials), not the D365 DataArea.

The API calls D365 OData action `LogicRetailMobileUsersActivation_BI.AuthenticateUser`, then:

- rejects `IsSuccess=false` (`401 AUTH_FAILED`)
- rejects inactive accounts (`403 ACCOUNT_DISABLED` when `IsActive` or `UserInfoEnable` is false)
- sets operating company = `InventLocationDataAreaId` else `Company`
- returns JWT **plus the full activation payload** on `data.user` (channel, warehouse, currency, default customer, `needsWarehouseSelection`, …)

Mobile must cache the entire `data` object. Subsequent catalog calls use `user.activeCompany` as `?company=`.

```http
POST /api/v1/auth/change-password
Authorization: Bearer <accessToken>
{ "oldPassword": "123", "newPassword": "1234" }
```

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
