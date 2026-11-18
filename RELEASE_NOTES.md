# v1.4.0 — Unified product editor and selective variants

- One parent-owned draft and Save product action across Details, Options, Variants and Content & SEO; reliable validation/conflict/session/recovery states preserve edits and release busy controls.
- Name/category-only drafts, no automatic placeholder variants, atomic publishing and explicit opening stock. Existing stock adjustments remain ledger-backed and independently versioned.
- Filtered, paginated selective combinations; no automatic selections or Cartesian generation. Up to 100 non-archived actual variants, explicit archived restoration and existing-row mapping.
- Product-local typed option metadata with global templates/suggestions, optional accessible SSR color swatches, stable readable automatic SKUs, explicit regeneration and normalized global uniqueness.
- Missing storefront combinations retain choices and show Out of stock, N/A price and N/A SKU. Real zero-stock variants retain pricing and stock alerts. Public shopping stays static SSR.
- Legacy product/workspace mutations now return 410 editor updated. Reload existing admin tabs. Existing public URLs and entity/inventory/order identities remain compatible.

Migration: `20261118122633_ProductEditorV2` adds JSONB metadata/schema columns, retirement flags, operation receipts and normalized SKU uniqueness. Existing rows receive empty metadata/schemas. Inventory concurrency maps to PostgreSQL xmin. Preflight `upper(btrim("SKU"))` collisions, including archived records; resolve them explicitly. The migration refuses collisions without modifying historic identifiers. See README for the preflight SQL and admin contracts.

Validation evidence and isolated staging screenshots are in `docs/product-editor-v2/`. No production customer data or real emails are used. Existing tag-based CI remains browser-free. Render keeps its configured deployment model; no separate manual production deploy was added.

# v1.3.1 — Complete media cache invalidation

- Media cache invalidation now evicts catalog, sitemap and static-page output-cache entries as well as HybridCache entries, keeping previously cached product feeds and image sitemaps current after URL migrations.
- Added PostgreSQL/HTTP regressions that warm a feed and product image sitemap, change their image URL, confirm the old cached responses, then verify protected invalidation refreshes them.

The production migration is complete: 79 images copied as 158 verified objects, 15 production web fields updated, and live storefront checks show AVIF loading with no Cloudinary requests. Development/POS databases and legacy source files remain unchanged. No schema/configuration migration is required beyond v1.3.0.

# v1.3.0 — Neon / S3 media storage

- Replaced the Cloudinary service/package with AWSSDK.S3 and portable endpoint, region, bucket and public URL configuration.
- Static uploads store AVIF + WebP only; animations preserve timing/loops in WebP. Removed original/PNG/JPEG and local-disk fallbacks. Safe SVG input is rasterized.
- Added bounded conversion, immutable object caching, partial-upload cleanup, paired deletion and managed-image-only URL resolution.
- Added resumable, checksum-verified catalog migration with exact-value updates, concurrency protection, rollback backups and protected cache invalidation.
- Removed legacy credentials from current test code. Rotate the old source API key because it remains present in Git history.

Production media is provisioned in jas-media-store (AWS Singapore). All 79 source images are copied as 158 verified modern-format objects; cutover affects the production web database only. Development/POS data and original source files remain unchanged. No EF schema migration is needed. Configure the documented storage secrets before the storage-aware build becomes live. See docs/media-storage.md for cutover, rollback and AWS migration instructions.

# v1.2.3 — Rich content editing and drag-and-drop media

- Restored the required rich-text description on the main product form, including new products. Formatting includes headings, lists, tables, links, images and a product specifications template.
- Brand and category introductions and buyer guides now use the same visual editor. Existing Markdown is prepared for display without rewriting stored source until edited; public rendering continues to sanitize content.
- Restored full photo drop targets and draggable product thumbnails, with keyboard ordering buttons and first-image cover selection. Existing protected uploads and size limits are retained.
- Explained the brand brief description beside the field and fixed whitespace-only introductions hiding its public fallback. It appears below the brand heading unless a custom introduction is provided, and supplies default SEO copy unless a custom meta description is provided.
- Added lifecycle cleanup for editors and drag handlers during navigation.

No migration or new configuration is required. Reload any admin tabs opened before upgrading to load the updated editor scripts. The existing TinyMCE CDN remains in use; if unavailable, preserved HTML remains editable in the fallback field. Public shopping pages remain static SSR. Browser tooling remains outside the repository and CI.

# v1.2.2 — Admin layout and brand management polish

- Fixed a conflicting admin-shell flex rule that stretched the page and clipped right-side controls. The fixed sidebar and content area now keep independent, bounded widths.
- Simplified admin headers, sidebar styling, back links and pagination. Removed technical implementation wording from the brand screen.
- Brands use a compact desktop table and mobile cards, with readable identities, clear filters, search, homepage toggles and accessible actions. Out-of-range pagination is clamped safely.
- The brand editor groups details, logo and visibility settings, with optional storefront content and SEO in expandable sections. Shared image controls now have a styled upload button, file guidance, preview and optional URL input.
- Brand list commands recheck the current administrator security stamp. Featured-toggle accessibility states are explicit and safe.
- Corrected the related-products “View all” category URL.

No database migration or configuration change is required. Existing URLs and publishing behavior are preserved. Public shopping pages retain static SSR. No browser-test dependency or CI step is added.
