-- The yearly list for the retention task (design.md §10.9, A29, A84): every participant of one organisation, Active and
-- Archived, with the dates that decide whether the periods for keeping their records may have ended, so that the
-- managers can decide. It only reads: the Organisation, Participant and Note tables, for the organisation given. It
-- writes nothing. It lists the participants and decides nothing: the managers' written decision is what permits a
-- destruction (ops/retention-and-destruction.md), and the NDIS claim period is not in the database.
--
-- One result set, one row per participant in the order the app lists them (A5), with the columns:
--   Family name, Given name, Date of birth, Status (Active or Archived);
--   Last submitted note: the latest note date of a Submitted note, blank when there is none;
--   7 years after last service: that date and 7 years, blank when there is none;
--   Note before 18: Yes when a note in any status is dated before the participant's 18th birthday, else No;
--   Turns 25: the participant's 25th birthday, always shown;
--   May have ended: No submitted note, or Yes when the 7 years have passed and, when Note before 18 is Yes, so has the
--     25th birthday, else No. A date has passed when it is before today in Melbourne;
--   Participant ID: the ID that retention-destroy.sql asks for.
--
-- Variable: Organisation, an organisation ID copied from the output of ops/find-ids.sql. An organisation that does not
-- exist is refused.
--
-- Run it as a member of the Entra group "Grow2Notes SQL admins", the SQL Entra admin, from the root of a clone of this
-- repository, with the sign-in, the temporary firewall rule for your own IP address and the retry for error 40613 that
-- the header of infra/sql/check-database.sql describes. The list names people: write it straight to a file with -o,
-- save it in the provider's records storage and delete the local copy (ops/retention-and-destruction.md).
--
--   sqlcmd -S <server>.database.windows.net -d <database> \
--     --authentication-method ActiveDirectoryDefault -N strict -b -W -v Organisation=<organisation ID> \
--     -i ops/retention-list.sql -o <file>

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @Organisation uniqueidentifier = TRY_CONVERT(uniqueidentifier, N'$(Organisation)');
IF @Organisation IS NULL
    THROW 50000,
          N'Refused: Organisation must be an organisation ID, copied from the output of ops/find-ids.sql.',
          1;
IF NOT EXISTS (SELECT 1 FROM dbo.Organisation WHERE Id = @Organisation)
    THROW 50000, N'Refused: no organisation has that ID.', 1;

DECLARE @Today date = CAST(SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE N'AUS Eastern Standard Time' AS date);

WITH Dates AS (
    SELECT
        p.Id,
        p.FamilyName,
        p.GivenName,
        p.DateOfBirth,
        p.Status,
        (SELECT MAX(n.NoteDate)
         FROM dbo.Note AS n
         WHERE n.OrganisationId = p.OrganisationId AND n.ParticipantId = p.Id AND n.Status = 1) AS LastService,
        CASE WHEN EXISTS (SELECT 1
                          FROM dbo.Note AS n
                          WHERE n.OrganisationId = p.OrganisationId AND n.ParticipantId = p.Id
                            AND n.NoteDate < DATEADD(YEAR, 18, p.DateOfBirth))
             THEN 1 ELSE 0 END AS NoteBefore18
    FROM dbo.Participant AS p
    WHERE p.OrganisationId = @Organisation
)
SELECT
    d.FamilyName AS [Family name],
    d.GivenName AS [Given name],
    d.DateOfBirth AS [Date of birth],
    CASE d.Status WHEN 1 THEN N'Active' WHEN 2 THEN N'Archived' END AS [Status],
    d.LastService AS [Last submitted note],
    DATEADD(YEAR, 7, d.LastService) AS [7 years after last service],
    CASE d.NoteBefore18 WHEN 1 THEN N'Yes' ELSE N'No' END AS [Note before 18],
    DATEADD(YEAR, 25, d.DateOfBirth) AS [Turns 25],
    CASE
        WHEN d.LastService IS NULL THEN N'No submitted note'
        WHEN DATEADD(YEAR, 7, d.LastService) < @Today
             AND (d.NoteBefore18 = 0 OR DATEADD(YEAR, 25, d.DateOfBirth) < @Today) THEN N'Yes'
        ELSE N'No'
    END AS [May have ended],
    d.Id AS [Participant ID]
FROM Dates AS d
ORDER BY d.FamilyName, d.GivenName, d.Id;
