# Content photography

Photographs used in page content, as opposed to brand assets or game artwork. Both files here were
previously hotlinked from the photographer's host, which meant every visitor's IP address was handed
to a third party, the homepage depended on someone else's CDN staying up, and the
Content-Security-Policy had to keep permitting arbitrary external images.

Licences recorded so provenance is not lost. Neither requires attribution; both are credited anyway.

| File | Source | Licence |
|------|--------|---------|
| `hero-stadium.webp` | Unsplash photo `photo-1579952363873-27f3bade9f55` | [Unsplash License](https://unsplash.com/license) - free for commercial use, no attribution required |
| `friends-watching-football.webp` | Pexels photo `23495488` | [Pexels License](https://www.pexels.com/license/) - free for commercial use, no attribution required |

## Sizing

`hero-stadium.webp` is 1920px wide at quality 45. The quality looks low for a hero image and is
deliberate: `.hero-section` lays an 80% opaque purple gradient over it
(`--purple-900-alpha-80`), so only about a fifth of the image's luminance reaches the viewer and
compression artefacts are invisible. At quality 80 the same image is 693KB rather than 312KB for no
perceptible gain. If it ever stops being overlaid, re-fetch at a higher quality.

`friends-watching-football.webp` is 1200px wide, rendered inside a Bootstrap `img-fluid` column.

## Replacing one of these

Fetch at the width the page actually needs, in WebP, and record the source and licence above. Do not
reintroduce a hotlink: the CSP permits external images (`img-src 'self' data: https:`) so it would not
break anything visibly, which is exactly why it would go unnoticed.
