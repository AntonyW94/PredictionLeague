-- An administrator had no way to see when a player signed up, because nothing recorded it.
--
-- [AspNetUsers] carried no creation timestamp: ASP.NET Identity does not supply one, and the two custom date columns are
-- about consent rather than registration. This adds the column; 0011 backfills the accounts that predate it.
--
-- Two scripts rather than one because SQL Server parses a batch before it runs any of it, so a statement referring to
-- [CreatedAtUtc] cannot sit in the same batch as the ALTER that creates it. A GO separator would also do it, but no
-- migration in this set uses one, and a script per step is separately journalled and separately readable.
--
-- ADDITIVE. A new nullable column, safe to apply ahead of the code deploy: nothing reads it until the deploy lands, and
-- the write path tolerates NULL.

ALTER TABLE [AspNetUsers]
    ADD [CreatedAtUtc] [datetime2](7) NULL;
