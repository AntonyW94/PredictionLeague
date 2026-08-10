# Content photography

Photographs used in page content, as opposed to brand assets or game artwork. Both files here were
previously hotlinked from the photographer's host, which meant every visitor's IP address was handed
to a third party, the homepage depended on someone else's CDN staying up, and the
Content-Security-Policy had to keep permitting arbitrary external images.

Licences recorded so provenance is not lost. Neither requires attribution; both are credited anyway.

| File | Source | Licence |
|------|--------|---------|
| `stadium-floodlit.webp` | Unsplash photo `photo-1579952363873-27f3bade9f55` | [Unsplash License](https://unsplash.com/license) - free for commercial use, no attribution required |
| `friends-watching-football.webp` | Pexels photo `23495488` | [Pexels License](https://www.pexels.com/license/) - free for commercial use, no attribution required |

## Sizing

`stadium-floodlit.webp` is 1920x1000 at quality 45.

The **landscape crop matters**: it backs the full-width `.hero-section` with `background-size: cover`,
and the source photograph is portrait. Fetched at its natural shape it came out 1920x2560, so the
browser scaled it to fill the width and threw away most of the height, paying for pixels nobody sees.
Cropping to a wide band took it from 312KB to 142KB with no visible difference.

The **low quality is also deliberate**: `.hero-section` lays an 80% opaque purple gradient over it
(`--purple-900-alpha-80`), so only about a fifth of the image's luminance reaches the viewer and
compression artefacts are invisible. If it ever stops being overlaid, re-fetch at a higher quality.

`friends-watching-football.webp` is 1200px wide, rendered inside a Bootstrap `img-fluid` column.

## Replacing one of these

Fetch at the width **and shape** the page actually needs, in WebP, and record the source and licence
above. Do not reintroduce a hotlink: the CSP permits external images (`img-src 'self' data: https:`)
so it would not break anything visibly, which is exactly why it would go unnoticed.
