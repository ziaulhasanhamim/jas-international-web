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
