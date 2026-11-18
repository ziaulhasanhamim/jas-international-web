> Historical validation record. Browser checks below describe earlier releases. As of v1.1.3, Playwright tests, tooling and CI steps are removed from the project; current CI runs CSS verification, the Release build, EF model verification and the three .NET suites.

# Validation evidence

Baseline commit: `d0a60db`. Validation uses disposable PostgreSQL containers and disabled mail delivery; no production database or customer email was used.

## Results

- Release build: zero warnings and zero errors.
- Unit tests: Passed: 52, Failed: 0, Skipped: 0.
- SEO tests: Passed: 35, Failed: 0, Skipped: 0.
- Integration tests: Passed: 91, Failed: 0, Skipped: 0.
- Desktop/mobile browser tests: Passed: 12, Failed: 0, Skipped: 0.

## Requirement coverage

| Requirement | Evidence |
| --- | --- |
| Private order access | `OrderAccess_AnonymousNumericReferencesAreDeniedAcrossAllSurfaces`, `OrderAccess_PrivateTokenOwnerAndAdminWorkButAnotherCustomerDoesNot` |
| Protected uploads and forms | `Uploads_AnonymousAndCustomersCannotReachAdminStorage`, `Uploads_AdminNeedsRealAntiforgeryTokenAndValidImageContents`, `Uploads_RealTokenAllowsValidImageAndRejectsOversizedFiles`, `CustomerForms_RealAntiforgeryRejectsMissingToken` |
| Webhook authentication and replay | `Webhook_MissingWrongAndUnconfiguredSecretsCannotVerifyPayment`, `Webhook_ValidPaymentIsReplaySafeAndUnconfiguredWebhookIsDisabled` |
| Payment state and transaction reuse | `PaymentVerification_ReplayDoesNotDuplicateTimelineAndPreservesFulfillment`, `PaymentVerification_RejectsReusedTransactionInvalidAmountAndCancelledOrder` |
| Concurrent coupon and stock limits | `Coupon_LastUseConcurrentCheckoutsAllowOnlyOneAndRollbackLosingReservation`, `Inventory_LastUnitConcurrentCheckoutAllowsOnlyOneAndRefreshesCachedDetails` |
| Accurate reviews | `Review_SubmittedByGuestIsPendingAndCannotClaimVerifiedPurchase`, `Review_DeliveredCustomerReceivesVerifiedBadgeButStillRequiresModeration` |
| Complete phone matching | `PhoneNormalization_RequiresCompleteValidBangladeshMobile`, `Tracking_CompletePhoneWorksButSuffixDoesNotRevealOrder` |
| Consent and proxy trust | `Attribution_WithoutAcceptedConsentCreatesNoTrackingCookiesOrData`, `ClientIp_DoesNotTrustRawForwardedHeader` |
| HybridCache invalidation | `DeliverySettingsService_InvalidateCache_ClearsCacheEntry`, `AdminSettings_SecretPersistsAndInvalidatesHybridCache` |
| Confirmed stock alerts | `Subscription_ConfirmsBeforeRestockEmailAndCompletesOnlyOnce`, `Subscription_ConcurrentDuplicatesUseOneNormalizedRequest`, `Subscription_UnsubscribePreventsDeliveryAndAllowsNewRequest` |
| Durable delivery/recovery | `Worker_ConcurrentClaimsDoNotSendSameRequestTwice`, `Worker_ExpiredLeaseRecoversAndFailedMailRemainsRetryable`, `Worker_MailUnavailableDoesNotClaimOrPretendToSend`, `Worker_UnsubscribeWhileSendingCannotReactivateRequest` |
| Admin configuration visibility | `StockAdmin_ProvidesMailConfigurationAndRequestVisibility` |
| Desktop/mobile SSR and interactions | Playwright: `hot routes render without Blazor circuits or WASM downloads`, `mobile and desktop product navigation preserves wishlist and adds one cart item`, `review submits through protected POST and awaits moderation`, `missing consent prevents Meta script and marketing cookies`, `payment updates redirect with private token and polling survives SSE failure` |

## Commands

The repository uses TUnit on Microsoft.Testing.Platform with .NET 11. Run the suites with `dotnet run --project tests/JASIntWeb.Tests.Unit --configuration Release`, and equivalent commands for `.Seo` and `.Integration`. Native `dotnet test` requires `--project` or `--solution` in this repository.

Earlier releases used disposable browser-test databases and archived screenshots/traces. These browser tests are no longer part of the project.

## Migration check

The additive `CustomerConfidenceAndStockNotifications` migration was applied to a fresh local disposable staging database. `dotnet ef migrations has-pending-model-changes` reported no model changes. Production migrations and deployment were not performed.

## Limits

Browser payment-update tests simulate SSE failure/status responses; PostgreSQL integration tests separately verify the payment and access rules. SMTP transport is faked in stock-worker tests. The existing integration factory bypasses CSRF for business tests; `UploadSecurityIntegrationTests` uses a separate factory retaining real antiforgery validation. Stock notification delivery is at least once across a crash after the provider accepts an email.

## Local rendering measurements

Same host and seeded disposable database, median of three fresh-browser-context samples per route. External HTTPS assets were blocked for repeatability. Application processes were warm; host load and local caching affect timing. These are smoke measurements, not production Core Web Vitals or an assertion of a performance improvement.

| Version | Route | Response start (ms) | Local requests | JavaScript transferred (bytes) | Circuit/WASM requests |
| --- | --- | ---: | ---: | ---: | ---: |
| baseline | `/` | 20 | 22 | 92916 | 0 |
| baseline | `/products` | 3 | 22 | 92916 | 0 |
| baseline | `/product/kospet-tank-t4c-smart-watch` | 10 | 22 | 92916 | 0 |
| overhaul | `/` | 46 | 23 | 92518 | 0 |
| overhaul | `/products` | 3 | 23 | 92518 | 0 |
| overhaul | `/product/kospet-tank-t4c-smart-watch` | 12 | 23 | 92518 | 0 |

The extra local request is the shared confidence stylesheet. Moving order-tracking JavaScript off the hot routes offsets the consent module: measured JavaScript transfer is slightly lower. No new SignalR circuit or WASM startup requests occur on these routes.

## Screenshots

[Desktop storefront](screenshots/home-desktop.png) · [Mobile storefront](screenshots/home-mobile.png) · [Desktop details](screenshots/details-desktop.png) · [Mobile details](screenshots/details-mobile.png)


## v1.1.1 production readiness follow-up

Validation uses isolated PostgreSQL databases and fake/test mail; production customer data and real customer messages are excluded.

| Requirement | Evidence |
| --- | --- |
| Clean composition root and endpoint organization | `Program.cs` is 17 lines; upload, webhook, account and legacy payment-status routes are registered from `Endpoints`; Release build has no warnings or errors |
| Exhaustive native unions without fallback cases | `FormReadResult`, `PaymentNotificationReadResult`, `RegistrationResult`, `VerificationResult`; compiler-validated switches in upload/webhook handlers and SSR signup |
| Real cart CSRF validation | `Cart_RealAntiforgeryRejectsMissingAndInvalidTokensAndAcceptsValidToken` |
| Upload authorization and CSRF retained | `Uploads_AnonymousAndCustomersCannotReachAdminStorage`, `Uploads_AdminNeedsRealAntiforgeryTokenAndValidImageContents`, `Uploads_RealTokenAllowsValidImageAndRejectsOversizedFiles` |
| Distributed verification and replay/password preservation | `Signup_CodeWorksAcrossScopesAndCannotBeReplayedOrResetConfirmedAccount` |
| Production mail configuration fails safely | `Signup_ProductionWithoutMailRejectsBeforeCreatingAccount` |
| Serialized concurrent verification | `Signup_ConcurrentVerificationHasExactlyOneWinner` |
| Verified provider email and safe failure redirects | `ExternalLogin_EmailMustBeExplicitlyVerified`, `ExternalLogin_RejectsUnconfiguredProviderAndSanitizesProviderErrors` |
| Safe API errors and health responses | `ApiFailure_ReturnsTraceableProblemWithoutExceptionDetails`, `Health_LiveAndReadyExposeStatusWithoutConnectionDetails` |
| PostgreSQL URL credentials, TLS, timeout and application name | `PostgresUrl_PreservesEscapedCredentialsAndExplicitTlsOptions`, `PostgresUrl_DefaultsToTlsAndStandardPort` |
| Bounded nonblocking analytics queue | `ConversionQueue_RejectsOverflowWithoutBlockingAndRetainsQueuedEvents` |
| SMS/form parsing and malformed JSON | `Webhook_ParsesFormSmsAmountsInvariantlyAndRejectsMalformedJson` |
| Browser signup and text-only cart feedback | `static signup confirms email before signing in`, `cart feedback treats server messages as text and rejects external action links` in desktop and mobile Playwright projects |
| Static SSR and enhanced navigation preserved | `hot routes render without Blazor circuits or WASM downloads`, `mobile and desktop product navigation preserves wishlist and adds one cart item`; no hot-route circuit/WASM requests |
| HybridCache only | Source search finds no direct `IMemoryCache` or `AddMemoryCache` in application code |

Commands run from the repository root:

```bash
npm run css
npm audit --audit-level=high
dotnet build JASIntWeb.sln --configuration Release --no-restore
dotnet run --project tests/JASIntWeb.Tests.Unit --configuration Release --no-build
dotnet run --project tests/JASIntWeb.Tests.Seo --configuration Release --no-build
dotnet run --project tests/JASIntWeb.Tests.Integration --configuration Release --no-build
dotnet ef migrations has-pending-model-changes --project JASIntWeb --configuration Release --no-build
dotnet package list --project JASIntWeb.sln --include-transitive --vulnerable --no-restore --format json
docker build -t jas-production-review .
```

Results: **193 .NET tests passed** (60 Unit, 35 SEO, 98 PostgreSQL Integration), **16 browser tests passed**, zero skipped. npm audit reports zero vulnerabilities; the transitive NuGet vulnerability report lists no vulnerable packages. EF Core 11 reports no pending model changes. No schema migration is added in this patch.

Production container validation checks the unprivileged UID, writable upload directory, production environment, database readiness, and disabled-by-default migrations on an isolated staging database. For v1.1.1, CI repeated container build/readiness checks and EF model validation, and archived browser screenshots and network evidence. These browser/container steps have since been removed from CI. See [production configuration and release procedure](production-readiness.md) for operator checks required in the actual hosting environment; no production deployment is part of this release.


## v1.1.3 carousel sizing and CI simplification

- The carousel uses one 8:3 frame with `object-contain`; mixed image dimensions keep the same slide height without cropping.
- Private visual checks exercised landscape (2400×900), portrait (900×1200) and wide (1800×600) sources at 302px, 390px and 1440px viewport widths. Frame dimensions stayed constant when navigating slides. These checks are internal working tools, not project files or CI dependencies.
- Removed `tests/browser/storefront.spec.js`, `playwright.config.js`, the Playwright npm dependency and the browser-test npm script.
- CI now runs npm/CSS validation, the Release build, the EF model check and the three .NET suites. Integration tests create their own PostgreSQL Testcontainers database; the separate PostgreSQL service and production-container smoke step are removed.
- Release build: zero warnings/errors. .NET tests: 60 Unit, 35 SEO and 98 Integration passed (193 total, zero failures/skips). npm audit: zero vulnerabilities. The EF consistency check also succeeds with an unreachable test connection string, confirming that it does not require the removed PostgreSQL service.


## v1.1.4 admin editors

Validated against a disposable PostgreSQL database with a fake administrator and no customer email or production data. The Release build and trimmed publish both succeed with zero warnings/errors. **220 tests pass**: 80 Unit, 35 SEO and 105 PostgreSQL Integration, with zero failures/skips. EF reports no pending model changes; this release adds no migration. npm audit reports zero vulnerabilities.

| Requirement | Evidence |
| --- | --- |
| Bounded category images, emoji and unsafe URL handling | `CategoryVisual_RecognizesImageLocationsWithoutTreatingEmojiOrScriptsAsImages`; `CategoryList_RendersImageAsThumbnailAndSortsGloballyBeforePaging` |
| Live SEO rules and storage limits | `SeoPreview_UsesSameTitleAndDescriptionRulesAsStorefront`; `EditorValidation_GuidelinesDoNotReplaceStorageLimits`; private published-build typing checks on category, brand and product editors |
| Current administrator identity and real CSRF protection | `CatalogApi_RequiresAdministratorAndCurrentSecurityStamp`; `CatalogApi_RechecksDatabaseRoleForAnAlreadyAuthenticatedAdministrator`; `CatalogApi_RealAntiforgeryRejectsMissingAndWrongTokenAndAcceptsCorrectToken` |
| Persisted fields and public SEO output | `CatalogApi_CategoryAndBrandPersistEditorFieldsAndRejectMismatchedIds`; `CatalogApi_ProductPersistsMediaRichTextFlagsAndSimpleInventory` |
| Invalid input and missing record handling | `CatalogApi_ValidatesInputsAndMissingRecordsWithoutWriting` |
| Global sorting, stable ties and safe columns | `AdminSorting_WhitelistsColumnsAndSortsBeforePaginationWithStableTies`; `CategoryList_RendersImageAsThumbnailAndSortsGloballyBeforePaging` |
| Interactive Auto works in both runtimes | Private checks against the trimmed publish exercise cold Server and warmed WebAssembly category editing, live counters, uploads, saves and validation-error correction/retry; brand preview/save and product rich-text persistence also pass |
| Navigation and static routes | Private checks confirm existing slug preservation, changing editor IDs, sorting with filters, toast dismissal, and fresh home/catalog/detail visits without circuit or WebAssembly startup requests |

The .NET test/build commands above remain valid. Published-build verification also ran `dotnet publish JASIntWeb/JASIntWeb.csproj --configuration Release --no-restore`. Private browser tools and their dependencies are excluded from the repository and CI.

[Category list after the fix](screenshots/admin-categories-fixed.png) · [Live editor SEO preview](screenshots/admin-live-seo-preview.png)


## v1.2.0 product-owned variants and admin workflows

Validation uses disposable PostgreSQL databases, seeded fixtures and a private administrator with mail disabled. Production customer data and customer emails are excluded. **236 tests passed**: 83 Unit, 35 SEO and 118 Integration; zero failures/skips. Release build and trimmed publish succeed; EF 11 reports no pending model changes and npm audit reports no vulnerabilities.

| Requirement | Evidence |
| --- | --- |
| Migration preserves IDs, selections, inventory reservations and order history | `ProductOwnedMigration_PreservesIdentitiesSelectionsReservationsAndOrderSnapshots` migrates seeded records through the prior schema and forward again; the migration also applies to the isolated staging database |
| Local values copy suggestions independently, with stable codes | `Workspace_ProductValuesAreIndependentSuggestionsAndPreserveVariantIdentity` |
| Duplicate/foreign values and stale saves fail without partial writes | `Workspace_RejectsDuplicatesForeignOptionsAndStaleConfigurationWithoutPartialWrites` |
| Required mappings and referenced values are protected | `Workspace_RequiresMappingsAndPreservesReferencedValuesOnFailure` |
| Combination preview has no writes; generation is replay-safe and bounded | `Workspace_GenerationPreviewWritesNothingAndReplayCreatesNoDuplicates`; `Workspace_GenerationAcceptsOneHundredAndRejectsOneHundredOne` |
| Concurrent saves have one winner; details cannot rewrite prices or stock | `Workspace_ConcurrentVariantEditsHaveOneWinnerAndDetailsCannotChangePriceOrStock`; `SaveProductAsync_CreatesHiddenUnpricedVariantAndIgnoresLegacyStockFields` |
| Variant prices, SKU uniqueness and defaults are validated | `Workspace_RejectsInvalidPricesSkuReuseAndDefaultArchival`; `Workspace_VariantPriceFlowsToCatalogDetailsCartAndFeeds` |
| Stock edits are explicit, reservation-safe and ledger-backed | `Workspace_StockAdjustmentsAreExplicitReservedSafeAndLedgerBacked` |
| Administrator authorization and real antiforgery remain enforced | `Workspace_RequiresAdministratorAndRealAntiforgeryForMutationsAndPreview` |
| Markdown and existing HTML are formatted safely; preview equals public output | `CatalogContent_RendersMarkdownAndExistingHtmlWithSafeFormatting`; `CatalogContent_RemovesScriptsHandlersUnsafeLinksAndEmbeds`; `CatalogContent_PreviewMatchesPublicBrandAndCategoryOutput` |
| Stable attribute codes and Unicode value matching | `Workspace_UsedAttributeCodesCannotChangeSelectionUrls`; `ProductValues_NormalizeUnicodeWhitespaceAndCase` |
| Cards, full admin navigation and both Auto runtimes | Private trimmed-publish checks: centered card titles, 28 admin navigation destinations, mobile menu, live Markdown, cold Server and warm WebAssembly custom values/generation/variant pricing, preserved identities and zero initial stock |
| Drafts and static storefront | Private checks cover discard confirmation and retained drafts, successful saves, repeated navigation, and fresh home/catalog/details without circuit/WASM startup requests |

Existing .NET test commands above apply. Migration/model commands use EF tools **11.0.0**. No browser test files, packages or CI steps are added.

### Migration operations

`20261117132551_ProductOwnedVariantOptions` backfills local option values/codes and per-variant compare-at prices without replacing identities. Product revisions start at zero. Foreign-key ownership constraints remain in force. The unique product-value and code indexes reject duplicates. Upgrading fails atomically if existing values cannot satisfy those constraints; resolve conflicting catalog data on staging before deployment. Do not bypass the indexes.

Take a backup first. Downgrading after independent values are created/edited is guarded against data loss. Restore the backup if rollback is required. Old editor clients must reload: legacy product-edit price/stock fields are ignored, and variant writes use revision-checked workspace commands. No real mail is needed for these checks.

[Desktop workspace](screenshots/variant-workspace-desktop.png) · [Mobile workspace](screenshots/variant-workspace-mobile.png) · [Markdown preview](screenshots/variant-markdown-preview.png) · [Mobile cards](screenshots/variant-cards-mobile.png)

## v1.2.1 direct purchase and related products

| Requirement | Evidence |
| --- | --- |
| Configured cards buy the default variant and open the cart | `ConfiguredProductCard_BuysDefaultVariantAndRedirectsToCart` asserts the rendered action, redirect, cart variant and quantity |
| Related products exclude current/hidden products before limiting and appear beside specifications | `RelatedProducts_ExcludeCurrentBeforeLimitAndRenderBesideSpecifications` verifies four matching results and a single recommendations section before reviews |

Release build has zero warnings/errors. PostgreSQL integration suite: 120 passed, zero failed/skipped. Existing unit/SEO suites are repeated by CI. No schema migration or browser-test dependency is added.

CSS generation and native watch mode are checked from a clean checkout; npm audit reports zero vulnerabilities after removing the affected CLI watcher dependency.

## v1.2.2 admin and brand UI

| Requirement | Evidence |
| --- | --- |
| Readable list content, working filters and bounded pagination | `BrandList_UsesReadableContentAndPreservesFiltersAndPagination` |
| Feature/visibility commands preserve current identity validation | `BrandList_ActionsRecheckSecurityStampBeforeChangingVisibility` |
| Related-product category links resolve correctly | `RelatedProducts_ExcludeCurrentBeforeLimitAndRenderBesideSpecifications` |
| Responsive shell, brand cards and editor | Private published-build checks at 320, 390, 768, 1024, 1280 and 1440 CSS pixels verify no document overflow and bounded header actions; desktop/mobile screenshots reviewed |
| Editor controls | Private checks exercise brand creation, logo upload, live SEO, search and feature toggling against isolated PostgreSQL with a fake administrator and mail disabled |

Release build and trimmed publish succeed with zero warnings/errors. The PostgreSQL integration suite passes 122 tests with zero failures/skips. CI repeats the 83 Unit and 35 SEO tests as well. npm audit reports no vulnerabilities. No schema migration is added.

[Desktop brands](screenshots/brand-list-desktop.png) · [Mobile brands](screenshots/brand-list-mobile.png) · [Desktop editor](screenshots/brand-editor-desktop.png) · [Mobile editor](screenshots/brand-editor-mobile.png)

## v1.2.3 — Rich editors and photo workflows

Validation uses isolated PostgreSQL and fixture content only, with mail, tracking and external image uploads disabled. No schema migration is needed.

| Requirement | Evidence |
|---|---|
| Required product description available during creation | `ProductCreation_RequiredRichEditorAndDropUploadsArePresentOnMainForm` checks the rendered control, validation failure and persisted rich HTML. |
| Brief brand summary placement and metadata precedence | `BrandSummary_FillsBlankIntroductionAndSeoUntilExplicitOverrides` checks blank-intro fallback and explicit public/meta overrides. |
| Existing Markdown remains intact until edited | `RichEditor_PreparesLegacyMarkdownWithoutRewritingStoredSource`; private browser checks also open, blur and save the editor without changing its stored source. |
| Formatted content and safe images survive | `BrandAndCategory_RichHtmlPersistsAndRendersSafeImages`, `RichContent_PreservesSafeImagesAndRemovesDangerousImageSources`; scripts, event handlers and unsafe image URLs remain stripped. |
| Drag uploads, cover ordering and rich editing | Private published-app checks drop three actual PNG files, reorder the third to cover, save/reload product content, create a rich brand and save/reopen category content. |
| Responsive behavior and lifecycle | Private 390/320px layout checks, repeated navigation cleanup, Server editing and repeated full-page navigation. Browser scripts remain outside the repository and CI. |

Release build: zero warnings/errors. Unit: 84 passed; SEO: 35 passed; PostgreSQL integration: 126 passed. Total: **Passed: 245, Failed: 0, Skipped: 0**. Release publish with the existing client trimming settings, JavaScript syntax checks and npm dependency audit also pass. An additional whole-server trimming experiment completed with the framework’s existing reflection/Blazor warnings; full server trimming is not enabled for deployment. Existing strict authorization, antiforgery and upload validation tests run in the integration suite.

Screenshots: [product description and dropped gallery](screenshots/v1.2.3-product-editor.png), [brand rich content](screenshots/v1.2.3-brand-editor.png), [mobile category editor](screenshots/v1.2.3-category-mobile.png).

## v1.3.0 — Neon / S3 media migration

| Requirement | Evidence |
|---|---|
| Static formats, alpha and dimensions | `StaticConversion_ProducesOnlyModernFormatsAndPreservesTransparency`, `Conversion_DownsizesAndRejectsExcessiveFrames` |
| Animated WebP and safe vectors | `AnimatedConversion_PreservesFramesTimingAndLoopsWithoutAvif`, `SvgConversion_RasterizesSafeVectorsAndRejectsExternalPaintUrls` |
| S3 object lifecycle, provider portability and safe failures | `S3Upload_WritesPairWithContentTypesAndImmutableCaching`, `S3Upload_PartialFailureCleansPairAndMissingConfigDoesNotWrite`, `S3Animation_WritesOnlyWebpAndDeletesOnlyOwnedObjects`, `S3Configuration_SupportsNeonEndpointAndNativeAwsRegion` |
| Browser image contracts | `ManagedImages_StaticPairAndAnimatedWebpOnly`, `PictureImageComponent_RendersStaticAvifWithWebpAndAnimationWithoutPhantomAvif` renders actual component HTML |
| Migration semantics and preservation | `MigrationUrls_RemoveNegotiationButPreserveVisualTransformsAndHtml`, `Migration_PreservesInventoryReservationsAndOrderSnapshotsAcrossReplayAndRollback`, `MigrationCheckpoint_CancellationPreservesBackupAndRetryRetainsChecksums` |
| Database boundaries and stale edits | `Migration_ConcurrentEditAbortsWholeTransactionAndWrongDatabaseIsRejected`, `Migration_UnverifiedAssetsAndNonMediaFieldsAreRejected` |
| Private cache invalidation | `MediaCacheInvalidation_RequiresAdminAndRealAntiforgery` |

Local suites: **87 unit + 35 SEO + 130 PostgreSQL integration = 252 passed, 0 failed, 0 skipped**. Release build has zero warnings/errors; standard Release publish with existing client trimming succeeds. NuGet and npm report no known vulnerabilities.

Actual Neon SDK uploads and anonymous byte/type checks passed on an isolated storage branch. Private browser checks exercised real protected static and animated uploads, AVIF selection, WebP-only animation, cache invalidation, a 390px product view and public static SSR without a Blazor circuit. Browser tooling remains outside the repository and CI.

Production import inventory: **79 source images, 158 copied/verified objects, 15 catalog/content reference fields**. The manifest holds exact-value backups, source/destination checksums and the production database fingerprint outside tracked files. Only catalog/content fields are read; testing uses an isolated database with fake mail. Production URL application is gated on the deployed pipeline header and followed by cache invalidation and public smoke checks. No EF schema migration is needed.

[Desktop fixture](screenshots/v1.3.0-neon-desktop.png) · [Mobile fixture](screenshots/v1.3.0-neon-mobile.png). The newly-created media project's unused PostgreSQL compute was removed; object storage remains accessible independently.


## v1.3.1 — Media output-cache completion

`MediaCacheInvalidation_RefreshesPreviouslyCachedCatalogFeed` warms an actual public feed, changes a fixture image in PostgreSQL, confirms the response remains cached, and verifies protected invalidation exposes the new URL. This complements the real-antiforgery test. The complete local suites total 253 passing tests (87 unit, 35 SEO, 131 integration). Production v1.3.0 cutover updated all 15 media fields; live home/catalog/product/category checks selected Neon AVIF with zero Cloudinary requests and no public Blazor circuit/WASM startup.
