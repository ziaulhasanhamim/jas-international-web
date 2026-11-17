# v1.1.2 — Show complete carousel banners on mobile

- Removed the carousel's forced aspect ratios and minimum/maximum heights. Banner images now render at full width with their natural height, preserving the complete artwork on mobile and desktop.
- Kept fading slides stacked in a CSS grid so navigation does not collapse the carousel. Existing touch controls and static SSR remain intact.
- Added browser regression coverage for image proportions, slide navigation, and Chromium selecting the AVIF `<picture>` source. Regenerated production CSS.

The live banner's Cloudinary AVIF source was verified in Chromium: HTTP 200 with `Content-Type: image/avif`, even though its URL ends in `.png`. The original filename extension does not identify the delivered format.

No database migration or new configuration is required. Redeploy the application with this release to update a hosted site; publishing the GitHub release alone does not replace an already-running deployment. The full-width banner is shorter on narrow phones, so embedded text becomes smaller while remaining uncropped.
