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
