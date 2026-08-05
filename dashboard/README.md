# Logic Retail Super Admin Dashboard

Flutter **web** dashboard to register companies and their Dynamics (D365) credentials used by mobile login.

## Run

```bash
# API must be running on :3000
cd backend-dotnet/src/LogicRetail.Api
dotnet run

# Dashboard
cd dashboard
flutter pub get
flutter run -d chrome --dart-define=API_BASE_URL=http://localhost:3000
```

## APIs

| Method | Path |
|--------|------|
| GET | `/api/v1/admin/companies` |
| POST | `/api/v1/admin/companies` |
| PUT | `/api/v1/admin/companies/{code}` |
| DELETE | `/api/v1/admin/companies/{code}` |

Seeded: `logic-trial`, `usmf` (trial Azure app registration).
