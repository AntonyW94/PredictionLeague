-- An account cannot exist without an email address: it is the login, it is what a confirmation link is sent to, and every
-- creation path already requires one (ApplicationUser.Create guards it, and the Google path takes it from the provider's
-- claim, where Identity would reject a blank UserName before this constraint was reached).
--
-- The column was nullable only because ASP.NET Identity's schema is generic. Nine reads declared the address
-- always-present, which was true of the data and not of the schema - so the schema now says what the product means, rather
-- than nine result types each carrying a "skip anybody with no address" branch for a state that cannot happen.
--
-- Checked before writing this: no row on dev or prod is null or blank.
--
-- Tightening rather than additive: apply it before the code that relies on it, which is the usual order anyway because the
-- deploy runs migrations first.
ALTER TABLE [AspNetUsers]
ALTER COLUMN [Email] nvarchar(256) NOT NULL;
