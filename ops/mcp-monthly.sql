-- The monthly figures for one organisation's AI-assistant access (design.md §10.9, mcp-server.md §9, §11.5). It only
-- reads: the AuditEvent table, and the AspNetUsers table to name who invited. It writes nothing. Run it as
-- ops/audit-extraction.md says, for the organisation and the Melbourne month that the request names, with the
-- organisation's ID copied from the output of ops/find-ids.sql and never retyped.
--
-- Variables, each a plain value (a letter, a digit or a hyphen, as the ID and the month are):
--   Organisation  the organisation's ID.
--   Month         the month to cover, as yyyy-mm, from Melbourne midnight at its start to Melbourne midnight after its
--                 end. The script refuses an organisation that is not an ID or does not exist, and a month that is not
--                 yyyy-mm.
--
-- Two result sets, and every query in them names the organisation:
--   1. One row. MCP calls is the number of audit rows that a tool call made: a row with the mcp.read or mcp.refused
--      event, or any row whose Details say via mcp (what an assistant changed). Each row counts once. mcp.authorised
--      and mcp.authorisation_refused are a person's answer to a consent screen, not a call, and are not counted.
--      Refused is the mcp.refused rows among them. These are counts of rows, as ops/audit-extraction.md says of every
--      count, so a call that is repeated after a failure may be counted twice. The 429 answers of the rate limits, and
--      the 401 answers, are not in the log (a rate_limited tool error is: it is an mcp.refused row);
--      ops/audit-extraction.md has the query for them.
--   2. The invite list: one row per user.invited row made by an assistant, oldest first (then by Row), with the
--      columns Time (Melbourne) with its offset, Invited, Email, Role (Worker or Manager), Reason (empty when none was
--      given), Invited by, Support, Assistant and Row. Invited, Email and Role are as they were at the invite, so they
--      are still here if the account has since changed or been destroyed. Invited by is the person's name now, or
--      "(no longer on record)". Support is "Yes" when the person who invited holds the Support role now, because the
--      event does not record the role; it is empty for anyone else, and for a person who is no longer on record. A
--      reason does not mark Support, because a manager may give one too. Assistant is the client's name as the audit
--      row holds it.
-- A cell with nothing to show is empty.
--
-- Run it as a member of the Entra group "Grow2Notes SQL admins", the SQL Entra admin, from the root of a clone of this
-- repository, with the sign-in, the temporary firewall rule for your own IP address and the retry for error 40613 that
-- the header of infra/sql/check-database.sql describes. To save the result for the provider, as ops/audit-extraction.md
-- says, add -s, -y 0 and -o, and then remove the line of dashes under each header:
--
--   sqlcmd -S <server>.database.windows.net -d <database> \
--     --authentication-method ActiveDirectoryDefault -N strict -b -W \
--     -v Organisation=<ID> -v Month=2026-10 \
--     -s $'\t' -y 0 -o <file>.tsv -i ops/mcp-monthly.sql

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

DECLARE @MonthText nvarchar(100) = N'$(Month)';
IF @MonthText NOT LIKE N'[0-9][0-9][0-9][0-9]-[0-9][0-9]' OR TRY_CONVERT(date, @MonthText + N'-01', 23) IS NULL
    THROW 50000, N'Month is not a month written yyyy-mm.', 1;
DECLARE @MonthStart date = TRY_CONVERT(date, @MonthText + N'-01', 23);

-- Melbourne midnight at the start of the month up to Melbourne midnight at the start of the next, as UTC. Melbourne
-- midnight always exists, because the clocks change at 2 or 3 am.
DECLARE @FromUtc datetime2(3) =
    CAST(CAST(@MonthStart AS datetime2(0)) AT TIME ZONE N'AUS Eastern Standard Time'
        AT TIME ZONE 'UTC' AS datetime2(3));
DECLARE @ToUtc datetime2(3) =
    CAST(CAST(DATEADD(month, 1, @MonthStart) AS datetime2(0)) AT TIME ZONE N'AUS Eastern Standard Time'
        AT TIME ZONE 'UTC' AS datetime2(3));

-- A row whose Details are not JSON has no via, and reading a path of it would fail the script, so such a row reads
-- as an empty document.
SELECT
    COUNT(*) AS [MCP calls],
    COUNT(CASE WHEN a.EventType = N'mcp.refused' THEN 1 END) AS [Refused]
FROM dbo.AuditEvent AS a
CROSS APPLY (SELECT CASE WHEN ISJSON(a.Details) = 1 THEN a.Details ELSE N'{}' END AS Document) AS d
WHERE a.OrganisationId = @Org
    AND a.OccurredAtUtc >= @FromUtc
    AND a.OccurredAtUtc < @ToUtc
    AND (a.EventType IN (N'mcp.read', N'mcp.refused') OR JSON_VALUE(d.Document, '$.via') = N'mcp')
OPTION (RECOMPILE);

-- A tab or a line break in a name would split a row of the saved file, so each becomes a space.
SELECT
    CAST(a.OccurredAtUtc AT TIME ZONE 'UTC' AT TIME ZONE N'AUS Eastern Standard Time' AS datetimeoffset(3))
        AS [Time (Melbourne)],
    ISNULL(REPLACE(REPLACE(REPLACE(JSON_VALUE(d.Document, '$.new.displayName'), CHAR(9), N' '), CHAR(10), N' '),
        CHAR(13), N' '), N'') AS [Invited],
    ISNULL(REPLACE(REPLACE(REPLACE(JSON_VALUE(d.Document, '$.new.email'), CHAR(9), N' '), CHAR(10), N' '),
        CHAR(13), N' '), N'') AS [Email],
    ISNULL(JSON_VALUE(d.Document, '$.new.role'), N'') AS [Role],
    ISNULL(REPLACE(REPLACE(REPLACE(JSON_VALUE(d.Document, '$.reason'), CHAR(9), N' '), CHAR(10), N' '),
        CHAR(13), N' '), N'') AS [Reason],
    CASE
        WHEN inviter.Id IS NULL THEN N'(no longer on record)'
        ELSE REPLACE(REPLACE(REPLACE(inviter.DisplayName, CHAR(9), N' '), CHAR(10), N' '), CHAR(13), N' ')
    END AS [Invited by],
    -- 3 is Support (UserRole.Support).
    CASE WHEN inviter.Role = 3 THEN N'Yes' ELSE N'' END AS [Support],
    ISNULL(REPLACE(REPLACE(REPLACE(JSON_VALUE(d.Document, '$.client'), CHAR(9), N' '), CHAR(10), N' '),
        CHAR(13), N' '), N'') AS [Assistant],
    a.Id AS [Row]
FROM dbo.AuditEvent AS a
CROSS APPLY (SELECT CASE WHEN ISJSON(a.Details) = 1 THEN a.Details ELSE N'{}' END AS Document) AS d
LEFT JOIN dbo.AspNetUsers AS inviter ON inviter.OrganisationId = a.OrganisationId AND inviter.Id = a.ActorUserId
WHERE a.OrganisationId = @Org
    AND a.OccurredAtUtc >= @FromUtc
    AND a.OccurredAtUtc < @ToUtc
    AND a.EventType = N'user.invited'
    AND JSON_VALUE(d.Document, '$.via') = N'mcp'
ORDER BY a.OccurredAtUtc, a.Id
OPTION (RECOMPILE);
