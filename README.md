# JAS International

Blazor Web App using .NET 10, PostgreSQL, EF Core and ASP.NET Core Identity. The storefront and admin forms use static SSR with enhanced navigation. Small Interactive Auto components handle cart actions; JavaScript interop is limited to browser local storage.

## Local setup

Install the .NET 10 SDK, PostgreSQL and Node.js. From the solution directory, configure your local database and administrator:

```bash
dotnet user-secrets set "DB_URL" "Host=localhost;Database=jas_ecommerce;Username=postgres;Password=YOUR_PASSWORD" --project JASIntWeb
dotnet user-secrets set "Admin:Email" "YOUR_ADMIN_EMAIL" --project JASIntWeb
dotnet user-secrets set "Admin:Password" "YOUR_ADMIN_PASSWORD" --project JASIntWeb
npm ci --ignore-scripts
npm run css
dotnet restore JASIntWeb.sln
```

Configuration comes from `appsettings.json`, user secrets in Development, and environment variables. No `.env` loader is required. `ConnectionStrings:DefaultConnection` can also supply the database connection.

## Generate the model migration

The earlier migrations are preserved. The snapshot is restored to `CommercePhase`, so you can generate the next migration against the current models:

```bash
dotnet tool install --global dotnet-ef --version 10.0.0
dotnet ef migrations add ProductAttributeBindings --project JASIntWeb --startup-project JASIntWeb --output-dir Persistence/Migrations
```

If `dotnet-ef` is already installed, ensure its major version matches EF Core 10.

Review the generated migration **before applying it**. For a database containing existing products and variants, include a data migration that creates product attribute/option bindings, converts legacy variant selections to those bindings, and computes each variant's canonical `OptionSignature`. Resolve duplicate combinations before creating the unique index. EF scaffolding alone does not preserve that data automatically. Test the upgrade on a database backup.

Then apply the reviewed migration and start the app:

```bash
dotnet ef database update --project JASIntWeb --startup-project JASIntWeb
dotnet build JASIntWeb.sln
dotnet dev-certs https --trust
dotnet run --project JASIntWeb --launch-profile https
```

Open `https://localhost:7176` and sign in to access `/admin`. Development startup seeds an empty catalog. Demo customers use `demo.customer1@example.test` or `demo.customer2@example.test` with password `DemoTest123!`. Set `Seed:Enabled=false` to disable these development fixtures.

## Forms and project organization

Admin pages are grouped under `Components/Pages/Admin` into Dashboard, Products, Variants, Inventory, Categories, Attributes, Carousel, Orders and Customers. Add and edit pages keep their existing URLs and share the admin layout.

Forms initialize in `OnInitialized` or `OnInitializedAsync`. Posted models are preserved with `??=` or a null check, and an `EditForm` renders only after its model exists. Each SSR form has a unique name, matching `SupplyParameterFromForm`, validation and a C# submit handler. Collection fields use explicit posted names rather than indexed binding expressions. Native checkbox fields include a false fallback after the checkbox so unchecked values post correctly.

Parameter lifecycle events remain on interactive child components that receive changing data, such as the carousel and reorder editor.

Products use reusable global attributes and values, with product-specific bindings controlling allowed options and their order. Variants reference those bindings. Customers can select any combination without another attribute changing automatically; unavailable combinations show N/A. Guest carts use local storage and signed-in carts use PostgreSQL.

## Email and hosting

Password reset emails require `Mail:Host`, `Mail:Port`, `Mail:EnableSsl`, `Mail:From`, `Mail:Username` and `Mail:Password`. Configure `PublicBaseUrl` and `AllowedHosts` for your host. Environment variable names use double underscores, for example `Mail__Host`. Signup uses email/password without mandatory email confirmation.

For production, persist data-protection keys with `Hosting:DataProtectionPath` and configure trusted reverse proxies with `Hosting:KnownProxies` if needed. Apply migrations deliberately and keep development seeding disabled.

This editing environment has no .NET SDK or PostgreSQL runtime. Source checks and CSS generation can run here; Razor compilation, migrations and browser form flows must be verified locally before deployment.
