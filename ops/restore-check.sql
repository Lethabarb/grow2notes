-- Checks a database after a restore (design.md §10.7, A83): which migration it is on, how recent its audit events and
-- note versions are, which note holds the latest version, how many rows each table has, and whether the database is
-- sound (DBCC CHECKDB). It only reads: the __EFMigrationsHistory, AuditEvent and NoteVersion tables, the names of the
-- tables, and the pages of the database for CHECKDB. It takes no variables and writes nothing, so it is the same
-- before and after go-live and on the restored copy and on production. It shows no name and no text, only times, counts
-- and one note ID, which the drill passes to ops/restore-note.sql.
--
-- Two result sets, then CHECKDB:
--   1  Check, Value:
--        Latest migration                 the newest migration applied;
--        Latest audit event (UTC)         the latest time of an audit event;
--        Latest note version (UTC)        the latest time a version of a note was saved;
--        Note with the latest version     the note and version number saved at that time, as "<note ID> version <n>".
--      A time is yyyy-mm-dd hh:mm:ss.fff in UTC, as Azure gives a restore point. A row whose table is empty says "none
--      yet", as before the first note is written.
--   2  Table, Rows: every table of the database, by name, each with its exact number of rows.
--   Last: DBCC CHECKDB prints nothing when the database is sound. An error stops the script, and sqlcmd -b gives
--   exit code 1.
--
-- Run it as a member of the Entra group "Grow2Notes SQL admins", the SQL Entra admin, from the root of a clone of this
-- repository, with the sign-in, the temporary firewall rule for your own IP address and the retry for error 40613 that
-- the header of infra/sql/check-database.sql describes. Name the database to check; for the restored copy that is
-- sqldb-grow2notes-restorecheck (ops/restore-drill.md):
--
--   sqlcmd -S <server>.database.windows.net -d <database> \
--     --authentication-method ActiveDirectoryDefault -N strict -b -W -i ops/restore-check.sql

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

SELECT c.[Check], c.[Value]
FROM (VALUES
    (1, N'Latest migration',
        ISNULL((SELECT TOP (1) CAST(h.MigrationId AS nvarchar(200))
                FROM dbo.__EFMigrationsHistory AS h
                ORDER BY h.MigrationId DESC), N'none yet')),
    (2, N'Latest audit event (UTC)',
        ISNULL(CONVERT(nvarchar(200), (SELECT MAX(a.OccurredAtUtc) FROM dbo.AuditEvent AS a), 121), N'none yet')),
    (3, N'Latest note version (UTC)',
        ISNULL(CONVERT(nvarchar(200), (SELECT MAX(v.CreatedAtUtc) FROM dbo.NoteVersion AS v), 121), N'none yet')),
    (4, N'Note with the latest version',
        ISNULL((SELECT TOP (1) CONVERT(nvarchar(36), v.NoteId) + N' version ' + CONVERT(nvarchar(11), v.VersionNumber)
                FROM dbo.NoteVersion AS v
                ORDER BY v.CreatedAtUtc DESC, v.Id DESC), N'none yet'))
) AS c(Position, [Check], [Value])
ORDER BY c.Position;

-- The tables come from the database itself, so a table added by a later migration is counted without a change here.
DECLARE @Counts nvarchar(max) = (
    SELECT STRING_AGG(
        CAST(N'SELECT N' + QUOTENAME(t.name, '''') + N' AS [Table], COUNT_BIG(*) AS [Rows] FROM dbo.'
            + QUOTENAME(t.name) AS nvarchar(max)),
        CAST(N' UNION ALL ' AS nvarchar(max))) WITHIN GROUP (ORDER BY t.name)
    FROM sys.tables AS t
    WHERE t.schema_id = SCHEMA_ID(N'dbo') AND t.is_ms_shipped = 0);
SET @Counts = N'SELECT c.[Table], c.[Rows] FROM (' + @Counts + N') AS c ORDER BY c.[Table];';
EXEC sys.sp_executesql @Counts;

DBCC CHECKDB WITH NO_INFOMSGS, ALL_ERRORMSGS;
