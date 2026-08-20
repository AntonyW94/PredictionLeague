-- An administrator had no way to see when a player signed up, because nothing recorded it.
--
-- [AspNetUsers] carried no creation timestamp: ASP.NET Identity does not supply one, and the two custom date columns are
-- about consent rather than registration. This adds the column. Every account created from here on is stamped by
-- ApplicationUser.RecordRegistration.
--
-- ADDITIVE. A new nullable column, safe to apply ahead of the code deploy: nothing reads it until the deploy lands, and
-- the write path tolerates NULL.
--
-- DATING THE ACCOUNTS THAT CAME BEFORE IT is a one-off data fix rather than a schema change, so it is not in this set and
-- is not applied by DbUp. It is run by hand, once, per environment. Until it has been, every pre-existing account reads
-- as NULL and the admin list shows "Join date unknown" - which is a true statement about an undated account, not a
-- broken screen. Nothing in the application depends on the backfill having happened.

ALTER TABLE [AspNetUsers]
    ADD [CreatedAtUtc] [datetime2](7) NULL;
