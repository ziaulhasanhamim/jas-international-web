# v1.2.1 — Direct Buy Now and related-product sidebar

- Product cards always offer Buy Now for the active default variant, including products with selectable options. A successful add opens the cart; unavailable defaults remain disabled.
- Product details show up to four other visible products from the same category beside the specifications on desktop, and below them on smaller screens. The current product is excluded before applying the result limit.
- CSS generation uses the same Tailwind compiler with Node’s built-in file watcher, removing the CLI watcher dependency affected by an unpatched picomatch advisory. Both existing npm commands are preserved.
- Removed the duplicated bottom recommendations section. Product pages remain static SSR with the existing protected cart behavior.

No migration or configuration change is required. Related products appear when other published products exist in the category. Existing browser-free CI is unchanged.
