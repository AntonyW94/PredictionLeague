# Image Storage: Self-Hosted Team Logos And User Profile Pictures

## Status

**Not Started** | In Progress | Complete

## Summary

Move team logos off other people's servers and onto ours, and add user profile pictures. These are
one piece of work because they share a mechanism, but they have **different storage answers**: team
logos are small and fixed and can live in the database; profile pictures cannot, because the database
has a hard 1 GB ceiling.

## Priority

**Medium.** Nothing is broken today. The drivers are that the two logo hosts can withdraw at any time,
that the Content-Security-Policy has to keep permitting arbitrary external images until logos are
local, and that profile pictures are a wanted feature that needs a storage decision first.

## The measured facts this plan rests on

Checked against the dev database and the live site on 2026-08-10.

| Fact | Value |
|---|---|
| Database data file cap | **1,000 MB**, currently 24 MB used |
| Database log file cap | 100 MB |
| Teams with a logo | 71, all external, all SVG |
| Logo host, 48 teams | `cdn.jsdelivr.net/npm/circle-flags@2.8.2/flags/{cc}.svg` - **MIT licensed** |
| Logo host, 23 teams | `resources.premierleague.com/premierleague25/badges/{id}.svg` - Premier League IP |
| `Teams.LogoUrl` column | `nvarchar(255)`, nullable |
| Current CSP `img-src` | `'self' data: https:` - **already permits both hosts**, verified by loading one of each in the live page |

## Why profile pictures cannot go in the database

The 1 GB cap is the whole argument.

| Users | Avatars at ~30 KB each | Share of the 1 GB cap |
|---|---|---|
| 1,000 | 30 MB | 3% |
| 5,000 | 150 MB | 15% |
| 30,000 | 900 MB | **90%** |

And that competes with the transactional data, which grows faster: one prediction per user per match
is roughly 1.9 million rows per season at 5,000 users. The database should be spent on predictions,
not on pixels.

Two further reasons, independent of size:

- **The nightly `backup-prod-db.yml` copies the whole database.** Avatars in the database means every
  backup carries them.
- **Profile pictures are personal data.** The dev refresh copies tables down and anonymises personal
  fields; real users' photographs appearing in dev is exactly what that exists to prevent. They would
  have to be added to the skip list.

Team logos have none of these problems: 71 rows of a few KB, no personal data, admin-controlled.

## Recommended shape

| Asset | Storage | Served as |
|---|---|---|
| Team logos | Database table `TeamLogos` | `/api/teams/{id}/logo` |
| Profile pictures | **Azure Blob Storage** | `/api/users/{id}/avatar`, streamed from blob |

Both are served from **our own origin**, which is what eventually allows `img-src 'self' data:`. Azure
Blob is configuration rather than new infrastructure: there is already a Key Vault and a service
principal per environment, so a storage account and a connection secret follow the existing pattern.

Streaming avatars through the app rather than handing out blob URLs keeps the CSP tight at the cost of
the request passing through the web tier. At current scale that is the right trade. If it becomes a
load problem, switch to direct blob URLs and add that single origin to `img-src`.

## Requirements

### Team logos

- [ ] `TeamLogos` table: `TeamId`, `Content varbinary(max)`, `ContentType`, `Width`, `Height`, `ContentHash`, `UpdatedAtUtc`
- [ ] `GET /api/teams/{id}/logo` with long `Cache-Control` and an `ETag` from `ContentHash`
- [ ] Ingest the 48 MIT-licensed flags from the pinned `circle-flags@2.8.2`, recording the licence
- [ ] Ingest the 23 Premier League badges (see the licensing note below)
- [ ] Migration repointing `Teams.LogoUrl` to `/api/teams/{id}/logo` - fits `nvarchar(255)`, so **no schema change to `Teams`**
- [ ] Add `TeamLogos` to `TableCopyOrder` in `DatabaseRefresher` (no anonymisation: not personal data)
- [ ] Update `docs/guides/database-schema.md`

### Profile pictures

- [ ] Azure Storage account per environment, connection string in Key Vault, referenced by `${...}` substitution like the existing secrets
- [ ] `IImageStore` abstraction with a blob implementation, so the backend is swappable and testable
- [ ] `GET /api/users/{id}/avatar` streaming from blob, with `ETag` and cache headers
- [ ] A default avatar for users who have not set one, served from `wwwroot` (no storage round-trip)
- [ ] Deletion on account deletion - `DeleteUserCommandHandler` already exists and must clean up the blob
- [ ] Add the avatar reference column to the dev refresh **skip or blank** list: personal data

### Upload and fetch-from-URL, shared by both

- [ ] Blazor `<InputFile>` for direct upload on the Team pages and the user's profile page
- [ ] Keep the existing `LogoUrl` text box on the Team pages, but change its meaning from "store this
      string" to "fetch this, convert it, and store the result"
- [ ] One ingest path for both, so validation and conversion happen in a single place
- [ ] Explicit request size limit - this is the application's first file upload

## Security work this cannot ship without

### SVG is executable

Both current logo sources are SVG, so SVG will be uploaded. An SVG served from our own origin can
contain `<script>`, which then runs as our site - stored XSS, and access tokens live in localStorage.

**Rasterise on ingest.** Convert whatever arrives to PNG or WebP at a fixed size (256x256 is generous;
badges render at 25-40 px). This removes the attack rather than mitigating it, and normalises sizes.
`SkiaSharp` and `Svg.Skia` are **already referenced by Infrastructure**, so the capability is present.

Always re-encode, even for PNG input, so metadata and any polyglot payload is stripped. Validate by
magic bytes, never by file extension. Cap byte size and pixel dimensions before decoding.

### Fetching a user-supplied URL is SSRF

"Enter a URL and the site fetches it" means the server requests an address someone else chose. Pointed
at `http://169.254.169.254/` that reads cloud metadata; at `http://localhost` it reaches things not
exposed publicly. Admin-only narrows the blast radius; it does not close it.

Required: HTTPS only, resolve the hostname and reject loopback, private and link-local ranges, refuse
redirects into those ranges, a hard timeout, and a response size cap enforced while reading.

## Licensing note on the Premier League badges

The 23 club badges are Premier League intellectual property and the club crests are registered
trademarks. Hotlinking them uses someone else's image from their server; copying them onto ours is
redistribution, which is a materially different act, and this site charges for a Season Pass so the use
is commercial.

**The site owner has considered this and accepted the risk** (2026-08-10), on the basis that the badges
will be removed if a request is ever received. Recorded here so the decision is deliberate and
attributable rather than an accident of a performance tidy-up. The 48 flags carry no such question:
`circle-flags` is MIT licensed.

## What deliberately is not being done yet

**The CSP `img-src` is being left as `'self' data: https:`.** It is tempting to narrow it to the two
known hosts now, but that would be wrong while `Teams.LogoUrl` is an admin-entered free-text field: the
next logo an admin adds from a third host would silently fail to render, with only a console message to
explain it. Narrowing is correct **after** logos are served from our own origin, at which point the
target is:

```
img-src 'self' data:;
```

Blanket `https:` on `img-src` is a modest exposure - it permits image loads, not script - so carrying it
until then is a reasonable trade rather than a hole.

## Order of work

1. `TeamLogos` table, the serving endpoint, and the ingest path with rasterising and the SSRF guards
2. Ingest the 71 existing logos, migrate `Teams.LogoUrl`
3. Narrow the CSP `img-src` to `'self' data:` and verify every team badge still renders
4. Azure Storage account and `IImageStore`
5. Profile pictures on top of it

Steps 1 to 3 are self-contained and deliver the CSP tightening. Steps 4 and 5 are the new feature.

## Notes

- The 1 GB figure was measured on **dev**. Confirm the production database has the same cap before
  relying on the headroom numbers; both are on the same Fasthosts server so they are likely identical,
  but it has not been checked.
- If logos live in storage rather than `wwwroot`, the "which folder do flags versus badges go in"
  question disappears: every team logo is `/api/teams/{id}/logo` regardless of origin. If they ever do
  go in `wwwroot` instead, use **one** folder named by team (`teams/arsenal.svg`, `teams/argentina.svg`)
  rather than splitting by provenance - a flag *is* an international side's logo. See the naming rules
  in `src/ThePredictions.Web.Client/CLAUDE.md`.
