# JAS International

Blazor Web App using .NET 10, PostgreSQL, EF Core and ASP.NET Core Identity. The storefront and admin forms use static SSR with enhanced navigation. Small Interactive Auto components handle cart actions; JavaScript interop is limited to browser local storage.

## Development setup

Install the .NET 10 SDK, PostgreSQL and Node.js with npm. Start PostgreSQL and create a local `jas_ecommerce` database. Run all commands below from the solution directory.

Configure your local database and administrator with user secrets:

```bash
dotnet user-secrets set "DB_URL" "Host=localhost;Database=jas_ecommerce;Username=postgres;Password=YOUR_PASSWORD" --project JASIntWeb
dotnet user-secrets set "Admin:Email" "YOUR_ADMIN_EMAIL" --project JASIntWeb
dotnet user-secrets set "Admin:Password" "YOUR_ADMIN_PASSWORD" --project JASIntWeb
```

Configuration comes from `appsettings.json`, user secrets in Development, and environment variables. No `.env` loader is required. `ConnectionStrings:DefaultConnection` can also supply the database connection.

Install dependencies and trust the local HTTPS certificate:

```bash
npm ci --ignore-scripts
npm run css
dotnet restore JASIntWeb.sln
dotnet dev-certs https --trust
```

## Run during development

Keep two terminals running from the solution directory. In the first, watch Tailwind styles and rebuild the generated CSS when source files change:

```bash
npm run css:watch
```

In the second, start the app with .NET watch for hot reload and automatic restarts:

```bash
dotnet watch --project JASIntWeb --launch-profile https
```

Open `https://localhost:7176` and sign in with your configured administrator to access `/admin`. Development startup applies the checked-in migrations and seeds an empty catalog. Demo customers use `demo.customer1@example.test` or `demo.customer2@example.test` with password `DemoTest123!`. Set `Seed:Enabled=false` to disable these development fixtures.

For a single build or a run without watchers:

```bash
npm run css
dotnet build JASIntWeb.sln
dotnet run --project JASIntWeb --launch-profile https
```

## Database changes

The current schema is checked in under `JASIntWeb/Persistence/Migrations`. Generate a new migration only when you change the EF models:

```bash
dotnet tool install --global dotnet-ef --version 10.0.0
dotnet ef migrations add YourMigrationName --project JASIntWeb --startup-project JASIntWeb --output-dir Persistence/Migrations
```

If `dotnet-ef` is already installed, ensure its major version matches EF Core 10. Review generated migrations and any required data conversions before applying them. Test changes against a database backup when existing data matters. Development startup applies pending migrations; to apply them manually, run:

```bash
dotnet ef database update --project JASIntWeb --startup-project JASIntWeb
```

## Forms and project organization

Minimal APIs live in static classes under `JASIntWeb/Endpoints`. `CartEndpoints` defines named handlers such as `Add`, `Merge` and `Update`, while `HealthEndpoints` handles `/health`. Each class exposes a mapping extension on `IEndpointRouteBuilder`; `Program.cs` registers them together with `app.MapStoreEndpoints()`. Cart routes retain authentication and CSRF protection, and return JSON errors through `CartRequestMiddleware`.

Admin pages are grouped under `Components/Pages/Admin` into Dashboard, Products, Variants, Inventory, Categories, Attributes, Carousel, Orders and Customers. Add and edit pages keep their existing URLs and share the admin layout.

Forms initialize in `OnInitialized` or `OnInitializedAsync`. Posted models are preserved with `??=` or a null check, and an `EditForm` renders only after its model exists. Each SSR form has a unique name, matching `SupplyParameterFromForm`, validation and a C# submit handler. Collection fields use explicit posted names rather than indexed binding expressions. Native checkbox fields include a false fallback after the checkbox so unchecked values post correctly.

Parameter lifecycle events remain on interactive child components that receive changing data, such as the carousel and reorder editor.

Products use reusable global attributes and values, with product-specific bindings controlling allowed options and their order. Variants reference those bindings. Customers can select any combination without another attribute changing automatically; unavailable combinations show N/A. Guest carts use local storage and signed-in carts use PostgreSQL.

## Admin sales metrics

Open `/admin/analytics` using **Sales & profit** in the admin menu. The dashboard also compares today, the last 7 days, last week, this month, last month, the current year, the last 365 days and all time. Last week means the previous Monday–Sunday; rolling ranges include today. All boundaries use Bangladesh time (UTC+6).

Only delivered orders count as sales, on their delivery date. The report includes product sales, gross profit, margin, order counts, units sold, average product sale per order, delivery fees, trends, a searchable sold-item breakdown and historical best sales/profit days, months and years. Item lists are paginated at 50 rows; report totals always cover the entire selected period.

Gross profit is product sales minus the purchase cost of sold units. Delivery fees are reported separately; delivery costs, taxes and operating expenses are excluded. New orders save each variant's purchase cost at checkout so later price changes cannot rewrite their profit. The `SalesMetrics` migration estimates older order costs from variant purchase costs at migration time, marks those costs as estimates and restores delivery dates from the order's last update (or creation date). Affected profit totals and records are labelled in the report. These estimates remain fixed after migration.

Run the metrics regression checks without a database:

```bash
dotnet run --project JASIntWeb.MetricsChecks
```

The checks cover reporting boundaries, gross profit, historical records and PostgreSQL query translation. Verify migration execution and checkout/delivery flows against a local PostgreSQL database as well.

## Email and hosting

Password reset emails require `Mail:Host`, `Mail:Port`, `Mail:EnableSsl`, `Mail:From`, `Mail:Username` and `Mail:Password`. Configure `PublicBaseUrl` and `AllowedHosts` for your host. Environment variable names use double underscores, for example `Mail__Host`. Signup uses email/password without mandatory email confirmation.

For production, persist data-protection keys with `Hosting:DataProtectionPath` and configure trusted reverse proxies with `Hosting:KnownProxies` if needed. Apply migrations deliberately and keep development seeding disabled.
