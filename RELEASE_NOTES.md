# v1.2.0 — Product-owned variants and streamlined administration

- Product cards consistently center their details and reserve two lines for titles. Products with selectable attributes offer “Choose options” rather than purchasing a default combination silently.
- Product management now has Details, Variants & stock, and Content & SEO tabs. Existing option/generator/variant edit URLs lead into the workspace.
- Product values belong to each product. Global values are optional suggestions copied on selection; changing a product label preserves its stable selection code and does not affect other products.
- Every variant owns its price, compare-at price, cost, SKU and barcode. Product-information saves cannot change those fields or inventory. New products are hidden drafts with one inactive, unpriced variant until explicitly configured and published.
- The workspace previews and generates missing combinations, preserves existing variant identities, prevents stale/concurrent overwrites, and records reasoned stock adjustments without reducing stock below reservations. New combinations start inactive with zero stock.
- Brand and category Markdown is converted to sanitized HTML, including existing supported HTML. Live previews use the same renderer as public pages. Category introductions and buyer guides now appear above/below the product grid, and SEO fields explain their public destinations.
- Administration has consistent navigation groups, deep-link states, breadcrumbs, mobile controls and shared responsive surfaces. Auto catalog editors warn before discarding drafts; pricing, configuration and stock use explicit commands.

## Migration and compatibility

Apply `20261117132551_ProductOwnedVariantOptions` before starting the new application. It copies labels/codes into existing product-option records, makes their global suggestion reference optional, adds product revisions, and copies existing product compare-at prices to variants. IDs, option signatures, inventory, reservations, SKUs, barcodes and historical order snapshots are retained. Legacy product compare-at columns remain for upgrade compatibility but are no longer authoritative.

Old product-edit request fields for prices, SKU, barcode and stock are accepted but ignored. Use `/api/admin/products/{id}/workspace` and its protected configuration, variant, stock, preview and generation commands. Stale revisions return 409; refresh and reapply the retained draft. Reload already-open admin forms after upgrading.

Take a database backup before migration. Downgrade is deliberately blocked once product-owned values differ from suggestions or have no source; restore a backup rather than lose those values. No new external service or production configuration is required. Staging migration and trimmed-publish smoke checks use isolated data and disabled mail. Publishing this release does not trigger a separate production deployment command.
