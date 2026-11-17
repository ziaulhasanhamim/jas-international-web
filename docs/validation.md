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

Run browser tests with `TEST_DB_URL` pointing to a disposable database and `npm run test:browser`. The browser test server applies migrations and development fixtures. Screenshots and traces are retained in the GitHub Actions `test-evidence` artifact.

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
# TEST_DB_URL points only to an isolated database
npm run test:browser
dotnet ef migrations has-pending-model-changes --project JASIntWeb --configuration Release --no-build
dotnet package list --project JASIntWeb.sln --include-transitive --vulnerable --no-restore --format json
docker build -t jas-production-review .
```

Results: **193 .NET tests passed** (60 Unit, 35 SEO, 98 PostgreSQL Integration), **16 browser tests passed**, zero skipped. npm audit reports zero vulnerabilities; the transitive NuGet vulnerability report lists no vulnerable packages. EF Core 11 reports no pending model changes. No schema migration is added in this patch.

Production container validation checks the unprivileged UID, writable upload directory, production environment, database readiness, and disabled-by-default migrations on an isolated staging database. CI repeats container build/readiness checks and EF model validation before a tag can publish a release. Browser screenshots and network evidence remain available in the CI test-evidence artifact. See [production configuration and release procedure](production-readiness.md) for operator checks required in the actual hosting environment; no production deployment is part of this release.
