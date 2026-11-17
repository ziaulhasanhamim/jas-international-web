# v1.1.1 — Production readiness and readable endpoints

- Reduced `Program.cs` to the composition root; moved upload, payment webhook, account and legacy payment-status handlers into the existing endpoint folder.
- Added native C# 15 result unions with exhaustive switches and modern collection expressions; grouped service registration and middleware setup.
- Protected cart writes with antiforgery validation and updated browser/WASM clients. Cart toast messages render safely as text.
- Serialized signup and confirmation in PostgreSQL, made verification codes work across app instances, blocked replay, required confirmed email, and hardened Google linking, lockout and return URLs.
- Added sanitized API errors, separate live/ready health probes, bounded analytics enqueueing and provider request timeouts.
- Production migration execution now requires explicit configuration. Failed migrations stop startup. Docker runs without root, excludes local secrets, and includes generated CSS.
- Updated the vulnerable source-map build dependency. CI audits npm dependencies and smoke-tests the production container.

No new schema migration in this patch. Apply existing migrations separately before normal production boot; keep `Database__ApplyMigrations=false` on application replicas. Data-protection keys persist in PostgreSQL unless a shared directory is configured. Set HTTPS, trusted proxies, allowed hosts, real mail and storage credentials using the production runbook.

Compatibility carried forward from v1.1.0: guest order links use the private GUID token; numeric IDs require the order owner or an administrator. Payment webhooks require a configured non-demo secret, positive amount, transaction ID and exact unambiguous order reference. Missing configuration disables webhook verification. Fulfillment progresses independently of payment verification.

Validation evidence and operator requirements: [production runbook](docs/production-readiness.md) and [validation record](docs/validation.md). Publishing this release does not deploy production.
