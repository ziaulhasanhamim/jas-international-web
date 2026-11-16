# Project audit — customer confidence release

The existing application already provides carts, checkout, bKash/COD, inventory, order tracking, reviews, wishlists, coupons, SEO/catalog feeds, and marketing reports. This release retains the existing file structure and static SSR storefront architecture.

## Confirmed issues addressed

- Admin upload routes lacked authorization and CSRF enforcement and trusted extensions without decoding image contents.
- Numeric order endpoints and pages exposed private order references and transaction details anonymously.
- Webhook authentication could be bypassed by omitting the secret. Matching by phone or token suffix could select the wrong order.
- Payment verification could overwrite fulfillment progress and accept conflicting or reused transaction IDs.
- Coupon usage validation was separate from locking; concurrent checkouts could exceed the limit.
- Cached detail availability and cached homepage output could survive inventory changes.
- Empty account carts could create temporary checkout items and fail while deleting them.
- Guest reviews were immediately published, and success text claimed verification without purchase evidence. Catalog summaries supplied invented fallback ratings/review counts.
- Forwarded headers trusted arbitrary sources, detailed errors were enabled in production, and marketing scripts/cookies ignored the consent choice.
- Settings used direct memory caching, performed synchronous reads in SEO rendering, and silently changed a free-delivery threshold while reading.
- Development fixtures had stale inventory/address/payment fields that prevented fresh-database startup.
- Mobile review submission could be obscured by the cookie banner; review dialogs now sit above it and scroll within the viewport.

## Added capability

Confirmed, variant-specific back-in-stock emails with unsubscribe, persistent retry/lease state, and admin delivery visibility. Mail configuration remains with the existing provider.

## Future feature opportunities

Customer return requests, product comparison, account-synced wishlists, and a durable marketing-conversion outbox would add useful capabilities. They are deferred from this release to keep the new workflow focused and avoid additional external services.

## Evidence

See `docs/validation.md` for test results, rendering checks, local migration verification, screenshots, and performance measurements. GitHub Actions repeats required checks before publishing a tagged release.
