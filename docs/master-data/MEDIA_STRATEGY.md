# Media Strategy

> **Phase covered:** 4. Stack context: .NET 10 API, Angular web, Flutter mobile, small Oracle VM, Neon
> Postgres, Caddy/TLS (per [[gymbro-live-deployment]]). Every non-obvious claim is cited in *Sources*.

Exercise media is the most bandwidth-, cost-, and rights-sensitive part of the library. The governing
principles: **never serve media from the app server**, **never ship animated GIFs**, **never scrape
competitor assets**, and **store license + attribution on every asset**.

---

## 1. Image formats & responsive delivery

**Recommendation:** AVIF → WebP → JPEG fallback for photographic/rendered images; **SVG** for flat line-art
(muscle diagrams, equipment glyphs, stick-figure illustrations); transparent WebP/AVIF when alpha is needed,
PNG only as the universal fallback.

- Modern formats (WebP, AVIF) compress better than PNG/JPEG, shrinking size and improving LCP. WebP is
  supported everywhere modern; AVIF is newer with narrower support — so serve **AVIF first, WebP next, JPEG
  fallback** via `<picture>`/`<source>` ordering; the browser picks the first it can render.
- **Web:** drive resolution with `srcset` + `sizes` (`w` descriptors paired with `sizes` so the browser knows
  the rendered box).
- **SVG vs raster:** vector for flat/geometric art (sharp at any density, tiny); raster (AVIF/WebP) for photos
  and shaded/3D renders.

---

## 2. Animation — no GIFs

**Recommendation:** **never ship animated GIFs.** Use **Lottie** for vector line-art exercise/UI animations;
use **muted looping MP4 + WebM** `<video>` for filmed/rendered motion clips.

- **GIF is the wrong tool.** web.dev's canonical example: the same animation is **3.7 MB GIF / 551 KB MP4 /
  341 KB WebM**. Reproduce GIF behavior with `<video autoplay loop muted playsinline>`; supply **WebM first
  (smaller) then MP4 fallback** (browsers use the first supported source). Lighthouse explicitly flags
  animated GIFs ("Use video formats for animated content").
  - ffmpeg: MP4 `ffmpeg -i in.gif -b:v 0 -crf 25 -f mp4 -vcodec libx264 -pix_fmt yuv420p out.mp4`;
    WebM `ffmpeg -i in.gif -c vp9 -b:v 0 -crf 41 out.webm`.
- **Lottie** stores per-frame vector instructions (JSON), not pixels — typically ~15–50 KB vs ~0.5–2 MB for an
  equivalent GIF (up to ~98% smaller in `.lottie` form), vector-sharp at any size, full alpha. **Use Lottie**
  for vector line-art/icon-style motion (animated demos, rep counters, loaders). **Don't** use it for filmed
  footage or photoreal/shaded renders — those belong in video.

---

## 3. Video — coaching / mistakes / slow-motion

**Recommendation:** H.264/AAC MP4 as the universal baseline; add HEVC and/or AV1 renditions selectively;
**HLS adaptive streaming** for anything beyond a few seconds; always supply a **poster frame**.

- **Codecs:** H.264 (AVC) = safest, the only codec guaranteed on every HLS device (Apple HLS mandates H.264;
  HEVC allowed via `hvc1`). HEVC = better compression for Apple-heavy audiences. AV1 = best efficiency
  (~60–70% better than H.264) but adoption depends on device/player/pipeline. → H.264 baseline + HEVC/AV1
  where it pays.
- **HLS:** package renditions in CMAF, list multiple codec variants per resolution tier (player picks best
  supported at runtime), a **4–6 rendition ladder (240p→1080p)**, **~2-second keyframe/GOP**.
- **Poster frames:** always set a poster so users see a frame before/while loading.

Short looping demo clips (≤ ~10 s) ship as plain muted `<video>` (§2); only longer coaching/mistakes videos
need HLS.

---

## 4. Rendition table (derivatives per asset)

Web uses `srcset` width descriptors; Flutter uses 1x/2x/3x density buckets (Flutter auto-selects from
`1x/2x/3x` asset folders / `@2x`/`@3x` suffixes; mdpi=1x, xhdpi=2x, xxhdpi=3x).

| Rendition | CSS px (1x) | Emit widths (raster) | Use |
|---|---|---|---|
| `thumb` | 96×96 | 96 / 192 / 288 | list rows, search results |
| `card` | 320×180 | 320 / 640 / 960 | grid/library cards |
| `detail` | 720×405 | 720 / 1080 / 1440 | exercise detail image/clip frame |
| `hero` | 1280×720 | 1280 / 1920 / 2560 | hero/banner |

- **Formats per rendition:** AVIF + WebP + JPEG fallback (transparent → WebP/PNG; line-art → one SVG, no size
  variants).
- **Video renditions:** poster (JPEG/WebP) per size above; clip ladder 360p/540p/720p/1080p, H.264 baseline +
  optional HEVC/AV1, HLS for > ~10 s.
- **Web markup:** `srcset` + `sizes`. **Flutter:** store `1x/2x/3x` (skip 4x unless xxxhdpi target).

---

## 5. Storage & CDN — cheap-but-scalable for a small VM

**Recommendation:** **do NOT serve media from the Oracle VM app server.** Put derivatives in S3-compatible
object storage behind a CDN; lock the origin so only the CDN can read it; serve **immutable, content-hashed**
URLs with year-long cache; use **short-lived signed URLs only for premium content**.

- **Architecture:** object storage (Cloudflare R2 / S3 / Backblaze B2 / MinIO) as origin, fronted by a CDN
  (Cloudflare / CloudFront / Fastly). Lock origin so only the CDN reaches the storage endpoint (prevents CDN
  bypass). **Cloudflare R2 (zero egress) or B2+Cloudflare is the cheapest scalable path** for a budget VM —
  egress is the usual cost killer.
- **Cache headers / immutable URLs:** for content-hashed filenames, set
  `Cache-Control: public, max-age=31536000, immutable`; new content = new filename (cache-busting). The Neon
  Postgres row stores only the **asset key/hash**, never bytes.
- **Signed URLs for premium content:** S3 signed URLs (expiry + signature). Caveat: volatile query params
  bust CDN/browser caches — whitelist only the signature param (e.g. `X-Amz-Signature`) or key on ETag.
  **Split:** public exercise images = unsigned immutable URLs; paywalled coaching videos = signed URLs/cookies.

---

## 6. Copyright & licensing of media

**Recommendation:** do not scrape competitor GIFs/videos (almost always proprietary → infringement risk).
Source from public-domain/permissive datasets, CC content with proper attribution, commissioned work, or
in-house shoots — and **store license + attribution on every asset** (data and media are separate license
regimes — see [DATA_SOURCE_COMPARISON.md](DATA_SOURCE_COMPARISON.md) §1).

- **free-exercise-db images:** the *data* is Unlicense, but the *images* trace to Everkinetic **CC-BY-SA 3.0**
  → attribution + share-alike. Prefer to **replace** them for the brand-facing core library.
- **Wikimedia Commons:** per-file CC0/PD/BY/BY-SA. Use **CC0/PD and CC-BY only**; **avoid CC-BY-SA**.
  Attribute with **TASL** (Title-Author-Source-License), note changes.
- **Commission / in-house shoot:** cleanest rights story and best brand consistency — recommended for the core
  catalog.
- **Per-asset license metadata** on `ExerciseMedia`: `license_code`, `attribution_text`, `source_url`,
  `author`, `license_url`, `requires_attribution` (bool), `share_alike` (bool). Drives the credits page and
  lets us audit/replace risky assets.

---

## 7. Pipeline — upload → derivatives → CDN

**Flow:** upload original to a private **originals** bucket → background worker generates derivatives
(resize, format-encode, posters) → write to a **derivatives** bucket with content-hashed keys → CDN fronts
the derivatives bucket → DB row records keys + placeholder hash + license. (Ties into the asset stage of
[IMPORT_PIPELINE.md](IMPORT_PIPELINE.md).)

- **Image derivatives in .NET:** **ImageSharp** (Six Labors) is the mature managed option (reads/writes JPEG,
  PNG, WebP, GIF, TIFF; `Clone()` to fan out sizes/formats). **⚠️ License:** Six Labors **Split License**
  (since July 2022) — Apache-2.0 for OSS/non-profits and for-profits **under $1M USD annual revenue**; a paid
  commercial license is required above that for closed-source for-profit use. If that's a concern, use
  **NetVips (libvips binding)** — permissive — instead. **Decide based on GymBro's revenue/OSS status before
  committing.**
- **Video/animation:** **ffmpeg** for GIF→MP4/WebM and HLS packaging (§2 commands).
- **Placeholders (LQIP):** generate **BlurHash** (~20–30 B; no alpha → renders transparency as black) or
  **ThumbHash** (~28 B; better quality, supports alpha) and store on the asset row for instant
  blur-up while the real image loads; **SQIP** (SVG silhouette) is an alternative.

---

## 8. `ExerciseMedia` model implications

Today's `ExerciseMedia` is `{ Type: "Image"|"Video" (string), Url (≤500) }` — too thin. Target:

| Field | Notes |
|---|---|
| `MediaType` | enum: Image / Animation(Lottie) / Animation(Video) / Video / Model3D(future) |
| `Role` | enum: Thumbnail / Card / Detail / Hero / DemoLoop / CoachingVideo / MistakesVideo / Illustration |
| `AssetKey` | content-hashed object key (CDN resolves; never an app-server URL) |
| `RenditionSet` | available sizes/formats (or derived by convention) |
| `PlaceholderHash` | BlurHash/ThumbHash for blur-up |
| `DurationMs` | for video/animation |
| `Poster` | poster asset key for video |
| `LanguageCode?` | for localized coaching video; null = language-neutral demo |
| license block | `LicenseCode`, `AttributionText`, `SourceUrl`, `Author`, `RequiresAttribution`, `ShareAlike` |

Publish gate (§11 of architecture): a published exercise needs ≥ 1 image/animation asset with a clean
`LicenseCode`.

---

## Sources

- web.dev image performance & `<picture>`: https://web.dev/learn/performance/image-performance ,
  https://web.dev/learn/design/picture-element , https://web.dev/learn/design/responsive-images
- MDN / CSS-Tricks responsive images: https://developer.mozilla.org/en-US/docs/Web/HTML/Guides/Responsive_images ,
  https://css-tricks.com/a-guide-to-the-responsive-images-syntax-in-html/
- GIF→video (sizes, markup, ffmpeg): https://web.dev/articles/replace-gifs-with-videos ;
  Lighthouse audit: https://developer.chrome.com/docs/lighthouse/performance/efficient-animated-content
- Lottie size/format: https://www.svgator.com/blog/what-is-lottie-format-guide/ , https://www.lottielab.com/lottie ,
  https://bplugins.com/blog/16510/lottie-animations-vs-gifs-in-wordpress/
- Codecs / HLS: https://antmedia.io/video-codecs-streaming-guide/ ,
  https://www.dacast.com/blog/encoder-settings-hls-live-streaming/ , https://www.dacast.com/blog/hls-streaming-protocol/ ,
  https://bitmovin.com/blog/higher-quality-lower-bandwidth-multi-codec-streaming/ ,
  https://wasabi.com/learn/how-to-add-av1-to-your-video-pipeline
- Flutter density / srcset density: Flutter resolution-aware assets docs ;
  https://cloudfour.com/thinks/responsive-images-101-part-3-srcset-display-density/
- Storage/CDN/caching: https://www.dchost.com/blog/en/using-object-storage-as-a-website-origin-with-s3-minio-and-a-cdn/ ,
  https://www.codestudy.net/blog/caching-images-with-different-query-strings-s3-signed-urls/ ,
  https://medium.com/@depascalematteo/optimizing-content-delivery-the-complete-guide-through-s3-caching-and-cloudfront-df64d1b7536a
- Media licensing: https://github.com/yuhonas/free-exercise-db (Unlicense data; CC-BY-SA images) ,
  https://commons.wikimedia.org/wiki/Commons:Licensing , https://commons.wikimedia.org/wiki/Commons:Reusing_content_outside_Wikimedia
- ImageSharp + license + alternative: https://sixlabors.com/products/imagesharp/ ,
  https://docs.sixlabors.com/articles/imagesharp/processing.html , https://sixlabors.com/posts/license-changes/ ,
  https://github.com/SixLabors/ImageSharp/blob/main/LICENSE , https://kleisauke.github.io/net-vips/ , https://www.libvips.org/
- LQIP/BlurHash/ThumbHash/SQIP: https://cloudinary.com/blog/low_quality_image_placeholders_lqip_explained ,
  https://www.fastly.com/documentation/solutions/tutorials/low-quality-image-placeholders/

**Caveats:** confirm GymBro's revenue/OSS status against the Six Labors Split License before relying on
ImageSharp commercially (NetVips/libvips is the permissive fallback). CC-BY-SA media is incompatible with a
clean proprietary library — prefer Unlicense/CC-BY/PD or commissioned assets for the core set. Keep public
images on unsigned immutable URLs; reserve signed URLs for premium video.
