/*
    Repoints the boost artwork from .png to .webp.

    The files themselves changed in the same commit: each was a 2048x2048 PNG of 3-4 MB, rendered
    into boxes between 30 and 260 pixels wide, and is now a 640x640 WebP of 20-40 KB. Across the
    eight images that is 28.0 MB down to 229 KB. The homepage was serving a 3.4 MB image to fill a
    200 pixel slot, so this is the largest single load-time saving available on the site.

    Additive and safe to apply ahead of the code deploy in one direction only: applied early, the
    database points at .webp files the running site does not yet have, so boost images would 404
    until the deploy completes. The deploy workflows run migrations immediately before publishing,
    so that window is the length of one FTP upload. Nothing else reads these columns.

    Deliberately matched on the exact old value rather than a blanket REPLACE on the column, so a
    row somebody has since pointed at custom artwork is left alone rather than silently rewritten
    to a path that may not exist.

    Note: none-disabled.png was referenced by the client but has never existed as a file, and no
    replacement is invented here. Predictions.razor still points at none-disabled.webp, which is
    equally absent. That is a pre-existing gap, flagged rather than papered over.
*/

UPDATE
    [BoostDefinitions]
SET
    [ImageUrl] = REPLACE([ImageUrl], '.png', '.webp')
WHERE
    [ImageUrl] LIKE '/images/boosts/%.png';

UPDATE
    [BoostDefinitions]
SET
    [SelectedImageUrl] = REPLACE([SelectedImageUrl], '.png', '.webp')
WHERE
    [SelectedImageUrl] LIKE '/images/boosts/%.png';

UPDATE
    [BoostDefinitions]
SET
    [DisabledImageUrl] = REPLACE([DisabledImageUrl], '.png', '.webp')
WHERE
    [DisabledImageUrl] LIKE '/images/boosts/%.png';
