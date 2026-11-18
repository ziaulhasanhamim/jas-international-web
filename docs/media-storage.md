# Neon / S3 media storage

Runtime uploads use `AWSSDK.S3` and the configured S3 endpoint. There is no Cloudinary SDK, URL transformation service, original-file storage, or local-disk fallback. Existing bundled icons/logos are unchanged.

## Configuration

| Variable | Purpose |
|---|---|
| `Storage__Bucket` | Bucket name (`jas-media`) |
| `Storage__PublicBaseUrl` | Public URL including the bucket, without trailing slash |
| `AWS_ENDPOINT_URL_S3` | Neon branch S3 endpoint; omit for native AWS S3 |
| `AWS_REGION` | Signing region (`ap-southeast-1` for Singapore) |
| `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` | Server-only credentials; AWS IAM credentials also work through the SDK's default chain |
| `Storage__ForcePathStyle` | `true` for Neon; configure for the destination provider when moving to AWS |

Create a `public_read` bucket through Neon, not an S3 ACL call. The application never grants per-object ACLs. Neon does not provide the Cloudinary transformation API; encoding runs in the application before upload. Missing configuration disables uploads with a safe error; it never writes raw files to the container.

Static images produce `images/static/<id>.webp` and `.avif`. Animated images produce only `images/animated/<id>.webp`, preserving timing/loops at quality 90. Static quality is WebP 82 and AVIF 75. Images are auto-oriented, metadata stripped and resized within 2048 × 2048. Existing upload byte limits apply; decoded images are limited to 40 million pixels per frame, 100 frames and 100 million aggregate canvas pixels. Two encoders can run concurrently. Sanitized SVGs are rasterized; remote resources and unsafe SVG content are rejected.

Keys are immutable and objects carry the matching MIME type and `public,max-age=31536000,immutable`. Both static formats must finish before an upload succeeds. Failed pairs are cleaned up. Deleting a managed image removes its companion; foreign/unmanaged URLs are never deleted. Browser markup uses AVIF plus a WebP image for static uploads, and WebP directly for animation. It does not invent companion URLs for unrelated external images.

## Migration commands

The operational commands run without starting the web server, hosted workers, schema migrations, seeders or mail. Run the Release app with configuration supplied through secure environment variables:

```sh
dotnet JASIntWeb.dll --MediaMigration:Action=prepare --MediaMigration:Manifest=/private/media-manifest.json
dotnet JASIntWeb.dll --MediaMigration:Action=copy --MediaMigration:Manifest=/private/media-manifest.json
dotnet JASIntWeb.dll --MediaMigration:Action=verify --MediaMigration:Manifest=/private/media-manifest.json
```

Supply the explicit production `DATABASE_URL`, destination storage configuration and source inventory credentials through `MediaMigration__Source__CloudName`, `MediaMigration__Source__ApiKey`, and `MediaMigration__Source__ApiSecret`. Never commit credentials or the manifest. The manifest contains catalog field backups and checksums and is written with owner-only permissions on Unix.

`prepare` inventories all `jas-storefront` originals and referenced images outside that folder. It scans an explicit catalog/content field allowlist, including rich content and payment QR images; it never scans customers or orders. Format/quality negotiation is removed from source URLs while visual crop/resize transformations are preserved. `copy` checkpoints each verified asset and uses deterministic identities, so interrupted runs resume without duplicated objects. `verify` anonymously downloads every object, validates its type and bytes, and compares saved checksums.

For cutover, set `MediaMigration__ConfirmTarget` to the manifest's database fingerprint and `MediaMigration__LiveBaseUrl` to the production origin. Then run `apply`. The command refuses a different database/destination, unresolved assets, stale source references or a live build without the configured media-pipeline health header. Its exact-value updates run transactionally, lock affected products and increment catalog revisions. Replaying a completed apply is a no-op.

After applying, authenticate as the current administrator, obtain the normal upload antiforgery token, and POST a form to `/api/admin/uploads/cache/invalidate` with `X-CSRF-TOKEN`. This clears catalog and delivery-settings HybridCache entries and catalog output-cache entries, including product feeds, in the running instance. Verify storefront pages, metadata and feeds. On multiple replicas, invoke invalidation for each instance; the current deployment has one instance.

To roll back, use `rollback` with the same manifest and target confirmation, then invalidate caches. Rollback only restores fields still matching migrated values; a conflicting edit aborts the transaction rather than overwriting a newer draft. Keep the original files until rollback and remaining development references are retired.

## Production migration scope

Project: **jas-media-store**, AWS Singapore. Bucket: **jas-media**. Only the production web database is updated. Development and POS databases remain unchanged. The initial inventory contains 79 images, producing 158 verified objects, and 15 production reference fields. No schema migration is required.

Existing Cloudinary images remain readable for development and rollback; they are not used by the migrated production catalog. Legacy source credentials found in historical tests must be rotated in the source account after migration; removing the current test cannot remove secrets from Git history. Migration tooling contains legacy URL/API knowledge only for the one-time import and rollback workflow; the runtime storage service has no Cloudinary dependency.

## Moving to AWS S3 later

Copy both managed namespaces without renaming keys. Configure the new bucket, region, public base URL and credentials, and remove the Neon endpoint override. Configure public image delivery through the AWS bucket/CDN using infrastructure permissions. Repoint stored managed URLs using the same exact-value backup/verification approach. The S3 service and image conversion code stay the same.

No CDN or additional paid service is introduced by this change. A custom public base URL/CDN can be configured later without changing the S3 adapter.
