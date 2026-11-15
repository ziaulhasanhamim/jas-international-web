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

Run automated unit and integration tests:

```bash
dotnet run --project tests/JASIntWeb.Tests.Unit
dotnet run --project tests/JASIntWeb.Tests.Integration
```

The tests cover domain rules, cart behavior, order fulfillment, coupon calculations, and reporting boundaries.

## Email and hosting

Password reset emails require `Mail:Host`, `Mail:Port`, `Mail:EnableSsl`, `Mail:From`, `Mail:Username` and `Mail:Password`. Configure `PublicBaseUrl` and `AllowedHosts` for your host. Environment variable names use double underscores, for example `Mail__Host`. Signup uses email/password without mandatory email confirmation.

For production, persist data-protection keys with `Hosting:DataProtectionPath` and configure trusted reverse proxies with `Hosting:KnownProxies` if needed. Apply migrations deliberately and keep development seeding disabled.

---

## SEO & Meta Ads (Facebook/Instagram) Production System

JAS International is engineered for maximum search engine domination on Google Bangladesh and high-converting Meta Ads with end-to-end attribution.

### 1. Canonical Domain & Technical SEO
- **Canonical Host:** `https://www.jasinternational.com.bd` (strict 301 redirection of non-www, HTTP, and legacy hosts).
- **XML Sitemaps:**
  - Index: `https://www.jasinternational.com.bd/sitemap.xml`
  - Products: `https://www.jasinternational.com.bd/sitemap-products.xml`
  - Categories: `https://www.jasinternational.com.bd/sitemap-categories.xml`
  - Brands: `https://www.jasinternational.com.bd/sitemap-brands.xml`
  - Buying Guides: `https://www.jasinternational.com.bd/sitemap-guides.xml`
  - Core Pages: `https://www.jasinternational.com.bd/sitemap-pages.xml`
- **Robots.txt:** Optimized disallow directives for private routes (`/checkout`, `/cart`, `/order/*`, `/account/*`, `/admin/*`) and filter queries (`?q=`, `?sort=`, `?page=`).
- **Structured Data (JSON-LD):** Generated strictly via `System.Text.Json` (no string interpolation):
  - `Product`: with variant SKUs, GTIN barcodes, price valid until, BDT currency, delivery fees (৳80 Khulna / ৳130 outside), 7-day return policy, and genuine aggregate ratings.
  - `BreadcrumbList`, `Store` (LocalBusiness with Khulna showroom coordinates), `Organization`, `WebSite` with SearchAction, and `FAQPage`.
- **Core Web Vitals & Next-Gen Image Optimization:**
  - **AVIF & WebP Pipeline:** All static assets and runtime uploads are automatically compressed into next-gen **AVIF** and **WebP** formats alongside standard fallbacks.
  - **Storefront `<picture>` Tags:** Reusable `<PictureImage>` Blazor component delivers `<picture><source type="image/avif" /><source type="image/webp" /><img /></picture>` across all product cards, product details, carousel hero banners, category tiles, brand showcases, and brand logos.
  - **Automated Upload Compression:** `IImageOptimizerService` automatically strips camera metadata, auto-orients smartphone uploads, normalizes dimensions, and outputs optimized primary, WebP, and AVIF bytes.
  - **Static Asset Reduction:** Brand logo shrunk from 506 KB down to 53 KB PNG, with companion 11 KB WebP and 10 KB AVIF (a ~98% weight reduction for instant mobile page loads).
  - **TinyMCE Isolation:** TinyMCE and `admin-editor.js` load exclusively on Admin routes, eliminating heavy scripts from public visitor and Googlebot payloads.

### 2. Meta Pixel & Conversions API (CAPI) Integration
- **Zero-Latency Checkout Architecture:** Server conversions use an in-memory non-blocking channel (`MetaCapiQueue`) processed by a background worker (`MetaCapiBackgroundWorker`) with exponential backoff retries. Tracking never delays customer checkout.
- **Bangladesh Phone Normalization:** Strict E.164 without `+` (`8801XXXXXXXX`) with SHA-256 lowercase hashing, matching >85% of BD Facebook/Instagram users.
- **Deduplication:** Server CAPI events and client Pixel events share identical `event_id` (`order.PublicToken.ToString()`).
- **Full Attribution:** Captures `fbclid`, `_fbp`, `_fbc`, `utm_*`, Meta dynamic URL tags (`campaign_id`, `adset_id`, `ad_id`), and records `FirstTouchSource` vs `LastTouchSource` on every Order.
- **Post-Purchase Conversion Events:** Dispatches `OrderDelivered` when orders reach Delivered status and `OrderCancelled` on returns.
- **Cookie Consent:** Non-blocking banner (`CookieConsentBanner.razor`) respecting customer privacy choices.

### 3. Catalog Feeds & Dynamic Ads
Live feeds update in real-time with BDT pricing, inventory stock, and computed custom labels:
- **Meta Advantage+ Catalog (XML):** `https://www.jasinternational.com.bd/feeds/meta.xml`
- **Meta Catalog (CSV):** `https://www.jasinternational.com.bd/feeds/meta.csv`
- **Google Merchant Center (XML):** `https://www.jasinternational.com.bd/feeds/google.xml` (includes Bangladesh shipping rules: ৳80 Khulna, ৳130 Nationwide).
- **Custom Labels Computed:**
  - `custom_label_0`: Margin Tier (`High Margin`, `Standard Margin`, `Low Margin`)
  - `custom_label_1`: Price Tier (`Under 1000 BDT`, `1000-5000 BDT`, `5000-15000 BDT`, `Above 15000 BDT`)
  - `custom_label_2`: Best Seller status (`BestSeller`, `Standard`)
  - `custom_label_3`: New Arrival (`NewArrival`, `Regular`)
  - `custom_label_4`: Stock urgency (`InStock`, `LowStock`, `OutOfStock`)

### 4. Admin Marketing Hub
- **Meta Ads & ROAS Dashboard (`/admin/marketing`):** Tracks sales and true ROAS strictly calculated from **Delivered Orders** (reflecting Bangladesh COD realities).
- **Abandoned Cart Recovery (`/admin/marketing/abandoned-carts`):** Captures shopper phone numbers early at checkout with 1-click WhatsApp and phone recovery links.
- **Custom Audiences Export (`/admin/marketing/audiences`):** 1-click Meta-formatted CSV export of Purchasers, VIPs (৳5,000+ LTV), Repeat Buyers, and Abandoned Leads for Lookalike targeting.
- **Feed Health Dashboard (`/admin/feeds`):** Diagnostic audit of active variants, in-stock percentages, and feed access tokens.

### 5. Automated Regression Verification Suite
Run the standalone regression test suite without starting a web server:

```bash
dotnet run --project tests/JASIntWeb.SeoChecks
```

---

## Store Owner Action Checklist

To begin running Meta Ads and ranking on Google Bangladesh:

1. **Google Search Console:**
   - Log in to [Google Search Console](https://search.google.com/search-console).
   - Add property `https://www.jasinternational.com.bd`.
   - Copy the verification token (HTML tag content) and enter it in `/admin/seo` or `appsettings.json` under `Seo:GoogleSiteVerification`.
   - Submit the sitemap index: `https://www.jasinternational.com.bd/sitemap.xml`.

2. **Facebook Domain Verification:**
   - In Meta Business Settings > Brand Safety > Domains, add `jasinternational.com.bd`.
   - Copy the verification meta-tag token and save it in `/admin/settings/delivery` or `appsettings.json` under `Seo:FacebookDomainVerification`.
   - Click **Verify Domain** in Meta Business Manager.

3. **Meta Pixel & Conversions API Setup:**
   - Open Meta Events Manager and select your Pixel.
   - Go to Settings > Conversions API > **Generate access token**.
   - Navigate to `/admin/settings/meta-ads` in your JAS Command Center.
   - Enter your **Pixel ID** and **Access Token**.
   - Optional: paste a **Test Event Code** (e.g. `TEST12345`) to verify live events in Meta Events Manager's Test Events tab.

4. **Connect Meta Advantage+ Product Catalog:**
   - In Meta Commerce Manager, create a Catalog and select **Add Products > Data Feed**.
   - Select **Scheduled Feed** and enter `https://www.jasinternational.com.bd/feeds/meta.xml`.
   - Set refresh interval to Hourly or Daily.

5. **Connect Google Merchant Center:**
   - In Google Merchant Center, create a primary feed for Bangladesh (`BDT`).
   - Enter feed URL `https://www.jasinternational.com.bd/feeds/google.xml`.

