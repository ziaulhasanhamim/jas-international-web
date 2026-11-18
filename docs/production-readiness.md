# Production readiness follow-up — v1.1.1

## Code organization and behavior

`Program.cs` is the composition root: service registration, database initialization, middleware, endpoint mapping, and Razor render modes. API handlers live under `Endpoints`. Hosting, accounts, persistence, caching and commerce registrations are grouped in `Services/ServiceCollectionExtensions.cs`; middleware ordering is explicit in `ApplicationPipeline.cs`.

Native C# 15 unions represent form parsing, payment notifications and account registration/verification results. Consumers handle each case directly in exhaustive switches without fallback arms. Collection expressions are used where they simplify construction. Runtime caching uses `HybridCache`; no application service uses `IMemoryCache`.

Cart writes now require real antiforgery tokens, including JSON and SSR form routes. The browser/WASM clients fetch a token when performing an action. Toast messages use text nodes. Google linking requires a verified provider email, honors lockout and two-factor requirements, and does not silently confirm an existing unverified account. Those shoppers must complete email signup first. Confirmed email is required for password sign-in.

Signup and confirmation use a PostgreSQL transaction and per-account advisory lock. Identity email codes are derived from the account security stamp and work across instances; confirmation rotates the stamp and consumes the code. HybridCache holds development-only code hints, never authentication evidence. Production signup fails safely when verification mail cannot be sent. Codes expire according to Identity's email token provider; request another code when necessary.

API exceptions return sanitized Problem Details with a trace identifier. Public health probes expose status only. Analytics enqueueing is bounded at 10,000 events and rejects overflow without holding up checkout; this optional queue remains in memory and is not durable. Stock notification delivery retains its database-backed leases and retries. Meta requests have timeouts and omit HTTP-client request logging that could reveal query-string tokens. The default Graph version is v25.0, configurable with `Meta:ApiVersion` ([Meta version lifecycle](https://developers.facebook.com/docs/graph-api/changelog/versions/)).

## Hosting configuration

Use the existing deployment platform and mail provider. Set these through the platform's secret/configuration store, not committed files:

| Setting | Production value |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `DATABASE_URL`, `DB_URL`, or `ConnectionStrings__DefaultConnection` | Restricted PostgreSQL credentials; prefer certificate-validated TLS |
| `PublicBaseUrl` | Public HTTPS origin |
| `AllowedHosts` | Explicit public hostnames separated by semicolons; include health-probe Host if different |
| `Hosting__KnownProxies__0` (and further indices) | Actual reverse-proxy IP addresses; never trust arbitrary forwarded headers |
| `Database__ApplyMigrations` | `false` in normal application replicas (the production default) |
| `Seed__Enabled` | `false`; demo seeding is also restricted to Development |
| `Mail__...` | Existing SMTP or Brevo provider settings; verify sender identity and delivery in staging |
| `Admin__Email`, `Admin__Password` | Unique bootstrap credentials, then remove the password after the administrator exists |

Data-protection keys persist in PostgreSQL by default. `Hosting__DataProtectionPath` optionally selects a persistent writable shared directory. Keep the keys when restarting or replacing containers. Protect their storage and database backups. All replicas must share keys and the database. The Docker image runs as the built-in unprivileged .NET user. Configure Neon/S3 media storage and its server-only credentials as described in [media storage](media-storage.md); uploads have no container-disk fallback. Configure the TLS-terminating proxy and its health probes before routing customers: secure identity and antiforgery cookies require HTTPS outside Development.

PostgreSQL URLs preserve escaped credentials and honor `sslmode`, `channel_binding`, `connect_timeout`, and `application_name`. Unknown URL options fail early. URL connections default to `sslmode=require`; use `verify-full` and trusted CA certificates in production. Standard Npgsql connection strings remain supported.

`/health/live` checks the process; `/health/ready`, `/health`, and `/healthz` check database connectivity. Readiness does not validate every table or provider. Migration failures abort startup when migrations are explicitly enabled. A fresh database must be migrated before normal production boot because administrator-role initialization needs the schema.

## Migration and release procedure

This patch adds no EF schema migration. Apply existing migrations to an isolated staging database before upgrading a deployment that predates v1.1.0. Build CSS and the Release solution, then generate a reviewed idempotent script or migration bundle using EF Core 11:

```bash
npm ci --ignore-scripts
npm run css
dotnet build JASIntWeb.sln --configuration Release
dotnet ef migrations has-pending-model-changes --project JASIntWeb --configuration Release
dotnet ef migrations script --idempotent --project JASIntWeb --configuration Release --output migration.sql
```

Apply the reviewed script as a separate deployment step using migration credentials. Back up and rehearse restoration first. A one-off staging boot with `Database__ApplyMigrations=true` can validate empty-database migration; do not enable competing migrations on normal replicas. Run live/ready checks, secure signup, password login, uploads, checkout, private order links and provider test notifications in staging. Use fake/test recipients and an isolated database for automation. Production data and customer mail are excluded from tests.

GitHub releases are tag-driven and require CI. Publishing a release does not deploy production. Configuration, real-provider delivery, proxy topology, backups and monitoring require operator verification in the target environment.
