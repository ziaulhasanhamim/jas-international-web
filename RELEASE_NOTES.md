# v1.1.0 — Customer confidence and storefront reliability

This release protects order and payment details, hardens admin uploads and webhook verification, and adds confirmed back-in-stock email alerts. Landing, catalog, and product-detail pages retain static SSR without WASM startup or active Blazor circuits.

## Changes

- Admin uploads require administrator authorization and CSRF protection, validate image contents and dimensions, and reject active SVG content.
- Guest receipts, payment updates, and live status use private order tokens. Numeric order URLs require the account owner or an administrator; anonymous numeric URLs return 404.
- Full normalized Bangladesh phone matching replaces partial-phone tracking lookups.
- Payment verification rejects missing webhook secrets, ambiguous matches, nonpositive amounts, reused transactions, and conflicting replays. Verification preserves fulfillment progress.
- Concurrent checkout enforces stock and coupon limits. Account checkout with an empty saved cart works correctly. Inventory writes invalidate product and homepage availability caches.
- Reviews await moderation. Verified-buyer badges require an authenticated delivered purchase; rating summaries use approved reviews.
- New variant-specific stock alerts require email confirmation, support unsubscribe and admin retry, and persist delivery leases and retries across restarts.
- Shared product availability, payment summaries, and fulfillment components improve mobile readability. Enhanced-navigation lifecycle cleanup prevents duplicate timers and handlers.
- Application caching uses HybridCache, including settings, verification codes, and redirects. Settings reads no longer silently alter free-delivery thresholds, and delivery/marketing settings forms bind to their posted models correctly.
- Trusted proxy configuration is honored, production detailed errors are disabled, and marketing tracking requires accepted consent.
- Development fixtures are updated to the current entity schema.

## Upgrade

1. Back up the database and apply the additive `CustomerConfidenceAndStockNotifications` migration through the normal staging process. It adds stock subscriptions and an order marketing-consent field; historical orders default to no marketing consent.
2. Configure a unique `SmsWebhookSecret` in delivery/payment settings. Empty secrets and the previous hardcoded demo secrets disable webhook verification. The sender must supply the exact secret, positive amount, transaction ID, and an exact order reference or submitted transaction ID.
3. Check `Hosting:KnownProxies` against the actual reverse proxy. Only trusted proxy headers are accepted; untrusted forwarded IP headers are ignored.
4. Configure the existing mail provider before offering stock alerts. `/admin/stock-notifications` exposes failed sends and retry actions. SMTP delivery is at least once: a crash after sending but before saving success can produce a duplicate email.
5. Old anonymous numeric receipt links no longer reveal order data. Customers should use their new private email links or full-phone tracking lookup. Never publish private tokens.
6. Check the configured free-delivery threshold; the application now honors it rather than clearing a value of 5000 on reads.

CI now builds CSS and runs .NET and browser checks. Releases are published only from validated version tags whose commits belong to main. This release does not deploy production or apply production migrations.
