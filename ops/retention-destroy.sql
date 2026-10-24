-- Destroys one archived participant's records, once the managers have decided in writing that the periods for keeping
-- them have ended (design.md §12 rows HPP 4.2 and 4.3, §10.9, A29, A89; ops/retention-and-destruction.md). It is the
-- one ops script that writes, and the one reviewed exception to "no ad-hoc SQL against production" (design.md §10.6).
--
-- It deletes the participant's rows from eight tables, in this order, in one transaction, scoped by the organisation:
--   NoteVersionItem, NoteVersionGroup, NoteVersion, NoteReview, NoteDraft, Note (every status), Goal, Participant.
-- It never touches AuditEvent, AspNetUsers, Organisation, CommonItemGroup or CommonItem, and it writes no audit row:
-- the log that design.md §12 asks for (HPP 4.3) is the output of this script, kept in the provider's records storage.
--
-- Variables, each copied from the output of ops/find-ids.sql or ops/retention-list.sql, never retyped:
--   Organisation  the organisation's ID.
--   Participant   the participant's ID.
--   DateOfBirth   the participant's date of birth as yyyy-mm-dd, which must match: it proves that the pasted ID is
--                 the person the managers named.
--   Mode          preview or destroy. Preview runs the same deletes in the same transaction and then rolls it back, so
--                 it prints the log values and the exact row counts and changes nothing. Run it first and save it.
--
-- It refuses, changing nothing, when: a value is not well formed; the organisation does not exist; the participant is
-- not in that organisation (one message covers an unknown ID and another organisation's ID); the date of birth does
-- not match; or the participant is not Archived. A refusal is an error with exit code 1 under sqlcmd -b.
--
-- Two result sets:
--   1  Outcome (Preview: nothing was deleted, or Destroyed), Name, Period from, Period to, Date of deletion: the values
--      of the HPP 4.3 log row. The period is the earliest and the latest note date of the participant in any status,
--      blank when there are no notes. The date is today in Melbourne.
--   2  Table, Rows: the rows deleted from each table, in the order above.
--
-- Run it outside working hours: it waits up to 30 seconds for a lock and then rolls everything back. It must run as
-- a member of the Entra group "Grow2Notes SQL admins", the SQL Entra admin, from the root of a clone of this
-- repository, with the sign-in, the temporary firewall rule for your own IP address and the retry for error 40613 that
-- the header of infra/sql/check-database.sql describes. The app's own identity cannot delete a participant.
--
--   sqlcmd -S <server>.database.windows.net -d <database> \
--     --authentication-method ActiveDirectoryDefault -N strict -b -W -v Organisation=<organisation ID> \
--     -v Participant=<participant ID> -v DateOfBirth=<yyyy-mm-dd> -v Mode=preview -i ops/retention-destroy.sql \
--     -o <file>

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @DateOfBirthText nvarchar(100) = N'$(DateOfBirth)';
DECLARE @Organisation uniqueidentifier = TRY_CONVERT(uniqueidentifier, N'$(Organisation)');
DECLARE @Participant uniqueidentifier = TRY_CONVERT(uniqueidentifier, N'$(Participant)');
DECLARE @DateOfBirth date = TRY_CONVERT(date, @DateOfBirthText, 23);
DECLARE @Mode nvarchar(100) = N'$(Mode)';

IF @Organisation IS NULL OR @Participant IS NULL OR @DateOfBirth IS NULL
   OR @DateOfBirthText NOT LIKE N'[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'
   OR @Mode NOT IN (N'preview', N'destroy')
    THROW 50000,
          N'Refused: Organisation and Participant must be IDs, DateOfBirth yyyy-mm-dd, Mode preview or destroy.',
          1;

IF NOT EXISTS (SELECT 1 FROM dbo.Organisation WHERE Id = @Organisation)
    THROW 50000, N'Refused: no organisation has that ID.', 1;

DECLARE @Name nvarchar(400), @Born date, @Status tinyint;
SELECT @Name = CONCAT(p.GivenName, N' ', p.FamilyName), @Born = p.DateOfBirth, @Status = p.Status
FROM dbo.Participant AS p
WHERE p.OrganisationId = @Organisation AND p.Id = @Participant;
IF @Name IS NULL
    THROW 50000, N'Refused: that organisation has no participant with that ID.', 1;
IF @Born <> @DateOfBirth
    THROW 50000, N'Refused: the date of birth is not that participant''s.', 1;
IF @Status <> 2
    THROW 50000, N'Refused: that participant is not Archived. A manager archives them in the app first.', 1;

-- The log values are read before the deletes, as the notes are gone afterwards.
DECLARE @PeriodFrom date, @PeriodTo date;
SELECT @PeriodFrom = MIN(n.NoteDate), @PeriodTo = MAX(n.NoteDate)
FROM dbo.Note AS n
WHERE n.OrganisationId = @Organisation AND n.ParticipantId = @Participant;
DECLARE @Today date = CAST(SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE N'AUS Eastern Standard Time' AS date);

-- A table variable keeps its rows through a rollback, so a preview prints the exact counts.
DECLARE @Deleted TABLE (Ordinal int IDENTITY(1, 1) PRIMARY KEY, [Table] sysname NOT NULL, [Rows] int NOT NULL);

SET LOCK_TIMEOUT 30000;
BEGIN TRANSACTION;

DELETE i
FROM dbo.NoteVersionItem AS i
INNER JOIN dbo.NoteVersion AS v ON v.OrganisationId = i.OrganisationId AND v.Id = i.NoteVersionId
INNER JOIN dbo.Note AS n ON n.OrganisationId = v.OrganisationId AND n.Id = v.NoteId
WHERE i.OrganisationId = @Organisation AND n.ParticipantId = @Participant;
INSERT @Deleted ([Table], [Rows]) VALUES (N'NoteVersionItem', @@ROWCOUNT);

DELETE g
FROM dbo.NoteVersionGroup AS g
INNER JOIN dbo.NoteVersion AS v ON v.OrganisationId = g.OrganisationId AND v.Id = g.NoteVersionId
INNER JOIN dbo.Note AS n ON n.OrganisationId = v.OrganisationId AND n.Id = v.NoteId
WHERE g.OrganisationId = @Organisation AND n.ParticipantId = @Participant;
INSERT @Deleted ([Table], [Rows]) VALUES (N'NoteVersionGroup', @@ROWCOUNT);

DELETE v
FROM dbo.NoteVersion AS v
INNER JOIN dbo.Note AS n ON n.OrganisationId = v.OrganisationId AND n.Id = v.NoteId
WHERE v.OrganisationId = @Organisation AND n.ParticipantId = @Participant;
INSERT @Deleted ([Table], [Rows]) VALUES (N'NoteVersion', @@ROWCOUNT);

DELETE r
FROM dbo.NoteReview AS r
INNER JOIN dbo.Note AS n ON n.OrganisationId = r.OrganisationId AND n.Id = r.NoteId
WHERE r.OrganisationId = @Organisation AND n.ParticipantId = @Participant;
INSERT @Deleted ([Table], [Rows]) VALUES (N'NoteReview', @@ROWCOUNT);

DELETE d
FROM dbo.NoteDraft AS d
INNER JOIN dbo.Note AS n ON n.OrganisationId = d.OrganisationId AND n.Id = d.NoteId
WHERE d.OrganisationId = @Organisation AND n.ParticipantId = @Participant;
INSERT @Deleted ([Table], [Rows]) VALUES (N'NoteDraft', @@ROWCOUNT);

DELETE FROM dbo.Note WHERE OrganisationId = @Organisation AND ParticipantId = @Participant;
INSERT @Deleted ([Table], [Rows]) VALUES (N'Note', @@ROWCOUNT);

DELETE FROM dbo.Goal WHERE OrganisationId = @Organisation AND ParticipantId = @Participant;
INSERT @Deleted ([Table], [Rows]) VALUES (N'Goal', @@ROWCOUNT);

DELETE FROM dbo.Participant WHERE OrganisationId = @Organisation AND Id = @Participant;
INSERT @Deleted ([Table], [Rows]) VALUES (N'Participant', @@ROWCOUNT);

IF @Mode = N'preview'
    ROLLBACK TRANSACTION;
ELSE
    COMMIT TRANSACTION;

SELECT
    CASE WHEN @Mode = N'preview' THEN N'Preview: nothing was deleted' ELSE N'Destroyed' END AS [Outcome],
    @Name AS [Name],
    @PeriodFrom AS [Period from],
    @PeriodTo AS [Period to],
    @Today AS [Date of deletion];

SELECT [Table], [Rows] FROM @Deleted ORDER BY Ordinal;
