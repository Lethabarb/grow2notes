-- Extracts one organisation's audit log for a manager's question (design.md §5.3, A28, A92). It only reads: the
-- AuditEvent table, and the AspNetUsers and Participant tables to put names beside IDs. It writes nothing. Run it only
-- for a request that has its row in the request record, with the IDs copied from the output of ops/find-ids.sql and
-- never retyped.
--
-- Variables, each a plain value (a letter, a digit or a hyphen, as the IDs and dates are):
--   Organisation  the organisation's ID.
--   From, To      the first and last day to cover, as yyyy-mm-dd Melbourne dates, both included.
--   Participant   a participant's ID, or any.
--   User          a user's ID, or any.
-- The three questions the log's indexes answer (design.md §5.3):
--   what was done to this participant   Participant=<ID>  User=any
--   what this user did                  Participant=any   User=<ID>
--   everything in the period            Participant=any   User=any
-- An ID with no row is accepted, because audit rows outlive a destruction (design.md §12): the rows still come back,
-- with the name shown as "(no longer on record)". The script refuses an organisation that is not an ID or does not
-- exist, a date that is not yyyy-mm-dd, From after To, and a Participant or User that is neither an ID nor any.
--
-- One result set, oldest first (then by Row): Time (Melbourne) with its offset, Event, By, By ID, Participant,
-- Participant ID, Entity, Entity ID, Entity name, Details, and Row. By shows "Operator command" for an event with no
-- actor. A name that cannot be found shows "(no longer on record)", and the ID beside it stays. A cell with nothing to
-- show is empty. The IP address in the table is not in the extract, and Details never hold note text. It removes no
-- duplicate and counts nothing: rows that look alike may be one event (ops/audit-extraction.md).
--
-- Run it as a member of the Entra group "Grow2Notes SQL admins", the SQL Entra admin, from the root of a clone of this
-- repository, with the sign-in, the temporary firewall rule for your own IP address and the retry for error 40613 that
-- the header of infra/sql/check-database.sql describes. To save the result for the manager, as ops/audit-extraction.md
-- says, add -s, -y 0 and -o, and then remove the line of dashes under the header:
--
--   sqlcmd -S <server>.database.windows.net -d <database> \
--     --authentication-method ActiveDirectoryDefault -N strict -b -W \
--     -v Organisation=<ID> -v From=2026-10-01 -v To=2026-10-31 -v Participant=any -v User=any \
--     -s $'\t' -y 0 -o <file>.tsv -i ops/audit-extract.sql
--   sed -i 2d <file>.tsv

SET NOCOUNT ON;
-- The joins need these session options, and sqlcmd can start with QUOTED_IDENTIFIER OFF.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @Org uniqueidentifier = TRY_CONVERT(uniqueidentifier, N'$(Organisation)');
IF @Org IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.Organisation WHERE Id = @Org)
    THROW 50000, N'Organisation is not the ID of an organisation. Copy it from the output of ops/find-ids.sql.', 1;

DECLARE @FromText nvarchar(100) = N'$(From)';
DECLARE @ToText nvarchar(100) = N'$(To)';
IF @FromText NOT LIKE N'[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]' OR TRY_CONVERT(date, @FromText, 23) IS NULL
    THROW 50000, N'From is not a date written yyyy-mm-dd.', 1;
IF @ToText NOT LIKE N'[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]' OR TRY_CONVERT(date, @ToText, 23) IS NULL
    THROW 50000, N'To is not a date written yyyy-mm-dd.', 1;
DECLARE @From date = TRY_CONVERT(date, @FromText, 23);
DECLARE @To date = TRY_CONVERT(date, @ToText, 23);
IF @From > @To
    THROW 50000, N'From is after To.', 1;

DECLARE @ParticipantText nvarchar(100) = N'$(Participant)';
DECLARE @P uniqueidentifier = TRY_CONVERT(uniqueidentifier, @ParticipantText);
IF @ParticipantText <> N'any' AND @P IS NULL
    THROW 50000, N'Participant is neither the ID of a participant nor any.', 1;
DECLARE @UserText nvarchar(100) = N'$(User)';
DECLARE @U uniqueidentifier = TRY_CONVERT(uniqueidentifier, @UserText);
IF @UserText <> N'any' AND @U IS NULL
    THROW 50000, N'User is neither the ID of a user nor any.', 1;

-- Melbourne midnight at the start of From up to Melbourne midnight after To, as UTC. Melbourne midnight always exists,
-- because the clocks change at 2 or 3 am.
DECLARE @FromUtc datetime2(3) =
    CAST(CAST(@From AS datetime2(0)) AT TIME ZONE N'AUS Eastern Standard Time' AT TIME ZONE 'UTC' AS datetime2(3));
DECLARE @ToUtc datetime2(3) =
    CAST(CAST(DATEADD(day, 1, @To) AS datetime2(0)) AT TIME ZONE N'AUS Eastern Standard Time' AT TIME ZONE 'UTC'
        AS datetime2(3));

-- A tab or a line break in a name would split a row of the saved file, so each becomes a space.
SELECT
    CAST(a.OccurredAtUtc AT TIME ZONE 'UTC' AT TIME ZONE N'AUS Eastern Standard Time' AS datetimeoffset(3))
        AS [Time (Melbourne)],
    a.EventType AS [Event],
    CASE
        WHEN a.ActorUserId IS NULL THEN N'Operator command'
        WHEN actor.Id IS NULL THEN N'(no longer on record)'
        ELSE REPLACE(REPLACE(REPLACE(actor.DisplayName, CHAR(9), N' '), CHAR(10), N' '), CHAR(13), N' ')
    END AS [By],
    ISNULL(CONVERT(nvarchar(36), a.ActorUserId), N'') AS [By ID],
    CASE
        WHEN a.ParticipantId IS NULL THEN N''
        WHEN p.Id IS NULL THEN N'(no longer on record)'
        ELSE REPLACE(REPLACE(REPLACE(p.GivenName + N' ' + p.FamilyName, CHAR(9), N' '), CHAR(10), N' '), CHAR(13), N' ')
    END AS [Participant],
    ISNULL(CONVERT(nvarchar(36), a.ParticipantId), N'') AS [Participant ID],
    ISNULL(a.EntityType, '') AS [Entity],
    ISNULL(CONVERT(nvarchar(36), a.EntityId), N'') AS [Entity ID],
    CASE
        WHEN a.EntityType IS NULL OR a.EntityType <> N'User' THEN N''
        WHEN entity.Id IS NULL THEN N'(no longer on record)'
        ELSE REPLACE(REPLACE(REPLACE(entity.DisplayName, CHAR(9), N' '), CHAR(10), N' '), CHAR(13), N' ')
    END AS [Entity name],
    ISNULL(a.Details, N'') AS [Details],
    a.Id AS [Row]
FROM dbo.AuditEvent AS a
LEFT JOIN dbo.AspNetUsers AS actor ON actor.OrganisationId = a.OrganisationId AND actor.Id = a.ActorUserId
LEFT JOIN dbo.Participant AS p ON p.OrganisationId = a.OrganisationId AND p.Id = a.ParticipantId
LEFT JOIN dbo.AspNetUsers AS entity
    ON entity.OrganisationId = a.OrganisationId AND entity.Id = a.EntityId AND a.EntityType = N'User'
WHERE a.OrganisationId = @Org
    AND a.OccurredAtUtc >= @FromUtc
    AND a.OccurredAtUtc < @ToUtc
    AND (@P IS NULL OR a.ParticipantId = @P)
    AND (@U IS NULL OR a.ActorUserId = @U)
ORDER BY a.OccurredAtUtc, a.Id
OPTION (RECOMPILE);
