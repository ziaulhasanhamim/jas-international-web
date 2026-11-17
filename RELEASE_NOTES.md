# v1.1.4 — Interactive admin editors and category rendering fixes

- Category image URLs render as bounded thumbnails instead of overflowing table rows as text. Emoji icons remain supported, with safe placeholders for invalid or broken images.
- Category, brand and product add/edit forms now use Interactive Auto, preserving existing URLs and server-authorized layouts. Shared SEO previews and character counters react while typing.
- Uploads, gallery ordering, rich-text descriptions, checkboxes and conditional product fields use the same state as saving. New slugs follow the name until manually edited; existing slugs remain stable when names change.
- Public category and product pages now honor saved SEO titles, descriptions and sharing images, using the same fallback rules as the live preview.
- Added protected admin editor APIs with model validation, current security-stamp/role checks, real antiforgery protection and no-store responses. Errors retain entered form values.
- Fixed inert admin table sorting and notification dismiss controls. Sort links preserve filters and sort before pagination. Removed obsolete JavaScript bindings from the converted editors.

No schema migration or new configuration is required. Reload any already-open admin edit forms after upgrading. Landing, catalog and product-detail routes remain static SSR. Existing simplified .NET CI is retained; no browser testing tools or pipeline steps are added.
