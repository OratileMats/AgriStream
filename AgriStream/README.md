# AgriStream IoT – Real-Time Processing System

ASP.NET Core 8 MVC web app + four Azure Functions (isolated worker) on Azure App Service,
Azure SQL, and Azure Storage (Blob, Table, Queue).

```
Browser ──► MVC App Service ──► Azure SQL        (Users, Roles, Devices registry)
              │   │
              │   ├─► Blob Storage  "drone-images"  ──(Blob trigger)──► Fn1 BlobUploadAudit ──► Table "ProcessingAudit"
              │   ├─► HTTP GET  Fn2 GetLatestTelemetry ──► Table "Telemetry"  (dashboard polls every 5 s)
              │   └─► HTTP POST Fn3 TriggerIrrigation  ──► Queue "irrigation-commands"
              │                                               │ (Queue trigger)
Sensors ─► POST /api/telemetry ► Table "Telemetry"            ▼
 (X-Api-Key)   └─ moisture < 25% → auto-irrigation ► Fn4 ProcessIrrigationCommand ──► Azure SQL dbo.Devices.Status
```

## Task → code map

| Task | Where |
|---|---|
| a) Web hosting | `scripts/azure-setup.sh` (App Service, S1) |
| b) Azure SQL user management | `Models/Entities.cs`, `Data/AppDbContext.cs`, `AccountController`, `UsersController`, `sql/schema.sql` |
| c) Blob + Table storage | `Services/BlobService.cs`, `Services/TelemetryService.cs`, `MediaController` |
| d) 4 Azure Functions | `AgriStream.Functions/Functions/*.cs` – Blob, HTTP GET, HTTP POST, Queue |
| e) Load testing | `loadtest/loadtest.py` (or Azure Load Testing / JMeter) |
| f) Monitoring | Application Insights wired into both projects |
| g) Auto-scaling | autoscale rules at the end of `scripts/azure-setup.sh` |

## Run locally

1. Put your **own** connection strings in user-secrets (never commit them):
   ```bash
   cd AgriStream
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:AzureSql" "<azure sql connection string>"
   dotnet user-secrets set "ConnectionStrings:AzureBlobStorage" "<storage account connection string>"
   dotnet user-secrets set "Ingest:ApiKey" "<any long random string>"
   ```
2. Edit `AgriStream.Functions/local.settings.json`: set `AzureWebJobsStorage` (same storage account) and `SqlConnectionString`.
3. Start the functions (needs Azure Functions Core Tools v4): `cd AgriStream.Functions && func start`
4. Start the MVC app: `cd AgriStream && dotnet run` (or F5 in Visual Studio – set both projects as startup projects).
5. On first run the app creates the `Users` and `Devices` tables and seeds demo data.

Seeded logins: `admin / Admin@12345`, `agro1 / Agro@12345`, `farmer1 / Farmer@12345` (change them!). New registrations are Farmers; an Admin can change roles on **Users**.

Try it: **Devices → Simulate readings** (Admin/Agronomist) → watch the dashboard fill → **Trigger Irrigation** → refresh Devices to see `Irrigating`.
Upload an image on **Drone Media** → the audit table fills within ~a minute (blob triggers poll).

## Deploy to Azure

```bash
export SQLPASS='<strong password>' INGEST_KEY='<long random string>'
bash scripts/azure-setup.sh            # edit SUFFIX at the top first

dotnet publish AgriStream/AgriStream.csproj -c Release -o publish/web
cd publish/web && zip -r ../web.zip . && cd ../..
az webapp deploy -g rg-agristream -n <webapp> --src-path publish/web.zip --type zip

cd AgriStream.Functions && func azure functionapp publish <funcapp>
```
(Or use Visual Studio → Publish for both projects.) After publishing the functions, re-run the `FUNC_KEY=...` line
or copy the function's *default* host key into the web app setting `Functions__Key` — the key can only be read once the function app is deployed.

## Load test, monitoring & auto-scale (tasks e–g)

```bash
python loadtest/loadtest.py --url https://<webapp>.azurewebsites.net --api-key <Ingest key> --users 150 --duration 600 --ramp 60
```
While it runs, capture screenshots of: **App Service plan → Metrics** (CPU %, memory %), **Application Insights → Live Metrics / Performance / Failures**,
and **Scale out (App Service plan) → Run history** showing the instance count rising. Lower `--users` if you hit table throttling; raise it if CPU stays < 60 %.

## Evidence checklist for submission

- App Service overview + running site URL
- Azure SQL: server/database in portal, `SELECT * FROM Users` / `Devices` results, and the MVC login working
- Storage account: `drone-images` container with blobs, `Telemetry` + `ProcessingAudit` tables, `irrigation-commands` queue
- Function App: all 4 functions listed, plus an invocation trace for each
- Load test output + Azure Monitor / App Insights graphs
- Autoscale rules and the run history showing a scale-out event

## Security notes

- Passwords are stored as ASP.NET Core `PasswordHasher` hashes, not plain text.
- The browser never sees the function key: the MVC app proxies calls to the functions.
- Sensors authenticate to `/api/telemetry` with the `X-Api-Key` header.
- **Do not commit secrets.** Rotate any key or password that has been shared in a zip, chat or repository.
