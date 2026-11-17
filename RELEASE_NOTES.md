# v1.1.3 — Consistent carousel sizing and simpler CI

- Every hero slide now uses one 8:3 frame at full container width, keeping a consistent height when images have different dimensions. Images use `object-contain`, preserving the full artwork; non-matching proportions leave space around the image.
- Removed the committed Playwright storefront suite, its configuration, npm dependency and browser-test command. Browser automation is no longer required to work on this project.
- Simplified CI to CSS generation/verification, dependency audit, the Release build, EF model verification, and the three .NET suites. PostgreSQL integration tests manage their own disposable Testcontainers database. Removed browser installation, browser tests, the extra PostgreSQL service and the production-container smoke step.

No database migration or new configuration is required. Matching 8:3 source artwork fills the frame edge to edge. Existing branding, carousel controls and static SSR behavior are retained.
