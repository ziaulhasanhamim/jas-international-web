# JAS International

Blazor Web App using .NET 11, PostgreSQL, EF Core and ASP.NET Core Identity. Public shopping pages use static SSR with enhanced navigation; stateful admin editors use Interactive Auto. Landing, catalog and product-detail routes stay static SSR with small JavaScript modules. Checkout/payment retain their existing Interactive Server modes; do not introduce active Blazor circuits or WASM startup on the hot storefront routes.

## Development setup

Install the .NET 11 SDK, PostgreSQL and Node.js with npm. Start PostgreSQL and create a local `jas_ecommerce` database. Run all commands below from the solution directory.

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
dotnet tool install --global dotnet-ef --version 11.0.0
dotnet ef migrations add YourMigrationName --project JASIntWeb --startup-project JASIntWeb --output-dir Persistence/Migrations
```

If `dotnet-ef` is already installed, ensure its major version matches EF Core 11. Review generated migrations and any required data conversions before applying them. Test changes against a database backup when existing data matters. Development startup applies pending migrations; to apply them manually, run:

```bash
dotnet ef database update --project JASIntWeb --startup-project JASIntWeb
```

## Forms and project organization

Minimal APIs live in static classes under `JASIntWeb/Endpoints`. `CartEndpoints` defines named handlers such as `Add`, `Merge` and `Update`, while `HealthEndpoints` handles `/health`. Each class exposes a mapping extension on `IEndpointRouteBuilder`; `Program.cs` registers them together with `app.MapStoreEndpoints()`. Cart writes validate antiforgery tokens; clients obtain them from `/api/cart/security`. API failures return safe JSON errors. Service registration and middleware setup live in focused `Services` extensions.

Admin pages are grouped under `Components/Pages/Admin` into Dashboard, Products, Variants, Inventory, Categories, Attributes, Carousel, Orders and Customers. Add and edit pages keep their existing URLs and share the admin layout.

Forms initialize in `OnInitialized` or `OnInitializedAsync`. Posted models are preserved with `??=` or a null check, and an `EditForm` renders only after its model exists. Each SSR form has a unique name, matching `SupplyParameterFromForm`, validation and a C# submit handler. Collection fields use explicit posted names rather than indexed binding expressions. Native checkbox fields include a false fallback after the checkbox so unchecked values post correctly.

Parameter lifecycle events remain on interactive child components that receive changing data, such as the carousel and reorder editor.

Products use reusable attribute definitions and product-owned values. The global value library provides optional suggestions that are copied into a product; labels and ordering are then independent. Variants reference stable product-owned option IDs. Customers can select any combination without another attribute changing automatically; unavailable combinations show N/A. Guest carts use a cookie reference and PostgreSQL; signed-in carts use the account identity.

## Unified product editor (v1.4.0)

Open `/admin/products/new` or `/admin/products/{id}/edit`. Details, Options, Variants and Content & SEO share one draft and **Save product** action. A name and visible category are sufficient to save a Draft; publishing also requires a cover image, description and one active default variant with a positive price. Stock may be zero. Rich text and drag/drop images remain available.

Options belong to the product. Global definitions and suggested values are templates copied when selected. Optional value fields support Text, Number, Yes/No and `#RRGGBB` color; choose a hex field as the storefront swatch. Keys/types cannot change or disappear while populated. Clear affected data explicitly first. Renaming labels or changing metadata preserves option codes, combinations and SKUs. Technical specifications belong in the rich description.

Preview combinations without saving, filter by values, and select only combinations that exist. Each page shows at most 50 rows, including Existing, Missing and Archived states. Nothing is selected automatically. Only checked Missing rows are staged; archived matches require explicit restoration. Products support 100 non-archived variants, including inactive rows. Legacy over-limit products can be edited or reduced, but cannot add/restore variants until within the limit. Map existing rows explicitly when changing options; inventory is never copied across combinations.

Blank variant SKUs become `JAS-{TITLE}-{OPTION-VALUES}-{SHORT-ID}` (or `BASE` for standard variants), within 100 characters. Existing/manual SKUs stay stable. **Regenerate SKU** clears it and shows a preview; the next save explicitly replaces the identifier. Uniqueness is case-insensitive after trimming and includes archived variants. Stock for new variants can be entered once as opening stock. Existing inventory uses a separate adjustment with quantity, reason and movement preview; PostgreSQL `xmin` protects it against stale stock/reservation changes independently of catalog revisions.

All editor API calls under `/api/admin/product-editor` require a current administrator session; writes require real antiforgery tokens and responses use `no-store`. Product and stock commands include operation IDs. Receipts commit with data, making replay safe. After an uncertain response, check operation status before retrying the same request. A confirmed save whose refresh fails requires a reload, rather than resubmitting new rows. Validation, conflict and session failures retain the draft; download it or review the latest revision before discarding it.

Legacy product edit/attribute/generator/variant URLs lead into the editor. Old product POST/PUT and workspace mutation contracts return 410 **editor updated**; reload older open tabs after upgrading. Brand/category APIs remain compatible.

### Product editor migration and rollout

`20261118122633_ProductEditorV2` adds JSONB schemas/value data, retirement flags, transactional operation receipts and a stored normalized-SKU unique index. Inventory uses PostgreSQL's existing `xmin` system column; no inventory quantities or historic IDs are rewritten. Existing schemas backfill to `[]`, metadata to `{}` and retired flags to false. Back up the database and apply the migration on staging first. Check collisions before rollout:

```sql
SELECT upper(btrim("SKU")) AS normalized_sku, array_agg("Id") AS variant_ids
FROM "ProductVariants"
GROUP BY upper(btrim("SKU")) HAVING count(*) > 1;
```

Resolve collisions explicitly, including archived rows. The migration stops with an actionable error instead of renaming identifiers. Receipt payloads are hashed; receipts contain operation/result identifiers, not the full product draft. Browser checks are private tooling and are not required in the repository or CI. Publishing the GitHub release uses the existing tag-driven validation workflow; Render retains its configured deployment behavior.

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

Password reset emails require `Mail:Host`, `Mail:Port`, `Mail:EnableSsl`, `Mail:From`, `Mail:Username` and `Mail:Password`. Configure `PublicBaseUrl` and `AllowedHosts` for your host. Environment variable names use double underscores, for example `Mail__Host`. Signup uses email/password with email-code verification. Local verification codes are shown only in Development when mail is not configured.

Data-protection keys persist in PostgreSQL by default; `Hosting:DataProtectionPath` optionally selects a persistent shared directory. Configure trusted reverse proxies with `Hosting:KnownProxies`. Production startup does not apply migrations by default. See the [production runbook](docs/production-readiness.md) for migration, cookie, mail, proxy and release requirements.

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
  - **AVIF & WebP Pipeline:** Static runtime uploads are stored only as **AVIF** and **WebP**; animations retain timing/loops in WebP only. Bundled static assets remain unchanged.
  - **Storefront `<picture>` Tags:** Reusable `<PictureImage>` Blazor component delivers `<picture><source type="image/avif" /><img src="…webp" /></picture>` across all product cards, product details, carousel hero banners, category tiles, brand showcases, and brand logos.
  - **Automated Upload Compression:** `IImageOptimizerService` automatically strips camera metadata, auto-orients smartphone uploads, normalizes dimensions, and outputs WebP/AVIF pairs for static images or animated WebP. No original/third fallback is stored.
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

### 5. Automated TUnit Verification Suites
Run the specialized test suites using TUnit:

```bash
# Core business logic and calculations
dotnet test --project tests/JASIntWeb.Tests.Unit/JASIntWeb.Tests.Unit.csproj

# Dedicated SEO, JSON-LD, OpenGraph & Meta CAPI/Pixel test suite
dotnet test --project tests/JASIntWeb.Tests.Seo/JASIntWeb.Tests.Seo.csproj

# End-to-end integration tests (PostgreSQL Testcontainers)
dotnet test --project tests/JASIntWeb.Tests.Integration/JASIntWeb.Tests.Integration.csproj
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



## Customer confidence and stock alerts

See [release notes](RELEASE_NOTES.md) for current compatibility changes. All application cache consumers use `Microsoft.Extensions.Caching.Hybrid.HybridCache`. Settings and redirects are read asynchronously; admin changes await invalidation.

Sold-out variants offer an SSR email-alert form. Requests stay pending until the recipient confirms via the emailed form. A database-backed worker checks availability every minute and processes up to 25 notifications per cycle. Failed sends retry with backoff, then appear as Failed in `/admin/stock-notifications` after five attempts. Administrators can retry after correcting mail configuration. Alerts never reserve stock.

Run the .NET suites after building the Release solution:

```bash
npm ci --ignore-scripts
npm run css
dotnet build JASIntWeb.sln --configuration Release
dotnet run --project tests/JASIntWeb.Tests.Unit --configuration Release --no-build
dotnet run --project tests/JASIntWeb.Tests.Seo --configuration Release --no-build
dotnet run --project tests/JASIntWeb.Tests.Integration --configuration Release --no-build
```

Integration tests create their own disposable PostgreSQL database with Testcontainers; Docker must be running. An explicitly supplied `TEST_DB_URL` is reset destructively and must point only to a disposable test database.

Carousel slides share an **8:3 aspect ratio** at full container width. Images use `object-contain` so different source proportions fit inside the same stable frame without cropping; surrounding space is expected for images that are not 8:3. Use matching 8:3 artwork for an edge-to-edge banner.

CI validates PRs and main without publishing releases. Push a version tag after merging to run the release workflow; it repeats validation and publishes `RELEASE_NOTES.md` only after success. Production deployment and production migrations remain separate operations.


## Interactive admin editors

Category, brand and product add/edit pages use Interactive Auto within their existing server-authorized admin layouts. A first visit can use server interactivity, with WebAssembly on later visits after the client bundle is cached. Storefront landing, catalog and detail pages remain static SSR.

The three editors share live SEO previews and character guidance, stateful image uploads and protected backend contracts. Admin APIs under `/api/admin/categories`, `/api/admin/brands` and `/api/admin/products` require the Admin role, current database-backed session validation and antiforgery tokens for mutations. Meta title/description guidance remains 60/155 characters; stored-field limits remain 100/300.

Static admin grids use sort links that preserve filters and sort before pagination. Notifications use a shared dismiss behavior. Browser checks remain private development tools; no browser-test dependencies or steps are added to the project or CI.


### Product workspace and content (v1.2)

Create product details as a hidden draft, then open **Variants & stock**. Add attributes, type custom values or copy suggestions, map the existing variant, and save the configuration. Preview before generating missing combinations (maximum 100). New combinations are inactive and have no price or stock. Enter variant prices, identifiers and active/default state explicitly; use **Adjust stock** with a reason to record inventory changes. Finally publish from Details. Product-detail saves never change variant prices or inventory.

The workspace uses product revisions to reject stale edits and existing inventory reservations to protect stock adjustments. IDs remain stable through option changes. Referenced values must be unmapped before removal; archived combinations retain their configuration and order history.

Product descriptions and brand/category introductions and buyer guides use a rich-text toolbar for headings, emphasis, lists, links, images and tables. The required product description is available immediately when creating a product and on both existing product tabs. Existing Markdown is displayed as formatted content without changing stored source until edited. Public content is sanitized. Introductions appear above the product grid; buyer guides appear below it. Meta title/description affect browser/search and sharing metadata, while the OG image affects sharing previews. Google may choose a different search snippet.

A brand’s **Brief description** is a short plain-text summary. It appears below the brand heading when the custom introduction is blank, and supplies the default SEO description when no custom meta description is set. A custom introduction changes the visible heading text; a custom meta description changes only search/sharing metadata.

Drop photos anywhere inside the dashed upload area or browse. Product galleries support dragging thumbnails to reorder; the first image is the cover. Arrow buttons provide the same ordering controls for keyboard and touch users. Upload limits remain ten files per batch and 10 MB per product image; brand logos are limited to 5 MB.

See [v1.2 migration and validation evidence](docs/validation.md#v120-product-owned-variants-and-admin-workflows) and [release notes](RELEASE_NOTES.md) before upgrading. Browser checks remain private working tools, with no repository dependency or CI step.


## Neon / S3 uploads

Configure the S3 endpoint, bucket, public base URL and server-only AWS credentials described in [media storage and migration](docs/media-storage.md). Uploads use `AWSSDK.S3`; changing to AWS S3 later requires configuration and an object/URL migration, with no adapter rewrite. Public shopping remains static SSR.
