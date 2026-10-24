-- Opens one note's record in a restored copy, or in production, so that the two can be compared (design.md §10.7,
-- A83): who and what the note is for, its status and current version, and when each version was saved, by whom, and
-- how many items and how much text it has. It only reads: the Note, Participant, NoteVersion, NoteVersionItem and
-- AspNetUsers tables, through the note's own organisation. It writes nothing. It never shows the text of a note: only
-- the length of the guided notes, so a restored copy and production can be compared without reading what a participant
-- received.
--
-- Variable: Note, a note ID copied from the output of ops/restore-check.sql, never retyped. A value that is not an ID,
-- and an ID that no note has, are refused.
--
-- Two result sets:
--   1  Participant, Note date, Status (Draft, Submitted or Discarded), Current version, Author.
--   2  Version, Saved (Melbourne) with its offset, Saved by, Items (the items the version holds), Guided notes length
--      (the characters in the guided notes): by Version. A person who has no account row shows "(no longer on record)".
--
-- Run it as a member of the Entra group "Grow2Notes SQL admins", the SQL Entra admin, from the root of a clone of this
-- repository, with the sign-in, the temporary firewall rule for your own IP address and the retry for error 40613 that
-- the header of infra/sql/check-database.sql describes. Run it on the restored copy and on production with the same
-- note, and do not keep the output (ops/restore-drill.md):
--
--   sqlcmd -S <server>.database.windows.net -d <database> \
--     --authentication-method ActiveDirectoryDefault -N strict -b -W -v Note=<note ID> -i ops/restore-note.sql

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @Note uniqueidentifier = TRY_CONVERT(uniqueidentifier, N'$(Note)');
IF @Note IS NULL
    THROW 50000, N'Refused: Note must be a note ID, copied from the output of ops/restore-check.sql.', 1;

-- Every other table is read through this, the note's own organisation, so that a row of another organisation is never
-- joined.
DECLARE @Organisation uniqueidentifier = (SELECT n.OrganisationId FROM dbo.Note AS n WHERE n.Id = @Note);
IF @Organisation IS NULL
    THROW 50000, N'Refused: no note has that ID.', 1;

SELECT
    p.GivenName + N' ' + p.FamilyName AS [Participant],
    n.NoteDate AS [Note date],
    CASE n.Status WHEN 0 THEN N'Draft' WHEN 1 THEN N'Submitted' WHEN 2 THEN N'Discarded' END AS [Status],
    n.CurrentVersionNumber AS [Current version],
    ISNULL(u.DisplayName, N'(no longer on record)') AS [Author]
FROM dbo.Note AS n
INNER JOIN dbo.Participant AS p ON p.OrganisationId = n.OrganisationId AND p.Id = n.ParticipantId
LEFT JOIN dbo.AspNetUsers AS u ON u.OrganisationId = n.OrganisationId AND u.Id = n.AuthorUserId
WHERE n.OrganisationId = @Organisation AND n.Id = @Note;

SELECT
    v.VersionNumber AS [Version],
    CAST(v.CreatedAtUtc AT TIME ZONE 'UTC' AT TIME ZONE N'AUS Eastern Standard Time' AS datetimeoffset(3))
        AS [Saved (Melbourne)],
    ISNULL(u.DisplayName, N'(no longer on record)') AS [Saved by],
    (SELECT COUNT_BIG(*)
     FROM dbo.NoteVersionItem AS i
     WHERE i.OrganisationId = v.OrganisationId AND i.NoteVersionId = v.Id) AS [Items],
    LEN(v.Narrative) AS [Guided notes length]
FROM dbo.NoteVersion AS v
LEFT JOIN dbo.AspNetUsers AS u ON u.OrganisationId = v.OrganisationId AND u.Id = v.CreatedByUserId
WHERE v.OrganisationId = @Organisation AND v.NoteId = @Note
ORDER BY v.VersionNumber;
