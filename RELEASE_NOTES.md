# v1.2.2 — Admin layout and brand management polish

- Fixed a conflicting admin-shell flex rule that stretched the page and clipped right-side controls. The fixed sidebar and content area now keep independent, bounded widths.
- Simplified admin headers, sidebar styling, back links and pagination. Removed technical implementation wording from the brand screen.
- Brands use a compact desktop table and mobile cards, with readable identities, clear filters, search, homepage toggles and accessible actions. Out-of-range pagination is clamped safely.
- The brand editor groups details, logo and visibility settings, with optional storefront content and SEO in expandable sections. Shared image controls now have a styled upload button, file guidance, preview and optional URL input.
- Brand list commands recheck the current administrator security stamp. Featured-toggle accessibility states are explicit and safe.
- Corrected the related-products “View all” category URL.

No database migration or configuration change is required. Existing URLs and publishing behavior are preserved. Public shopping pages retain static SSR. No browser-test dependency or CI step is added.
