-- Lists every organisation, participant and user with the ID that the other ops scripts ask for, so that nobody writes
-- a query to find one (design.md §9.4). It only reads: the Organisation, Participant and AspNetUsers tables, for the
-- whole database. It takes no variables, and it writes nothing. The IDs it prints are personal information about the
-- people named beside them: copy an ID from its output into the next command, never retype one, and do not keep the
-- output.
--
-- Three result sets:
--   1  Organisation, Organisation ID: by name.
--   2  Organisation, Family name, Given name, Status (Active or Archived), Participant ID: by organisation, then in
--      the order the app lists participants (A5).
--   3  Organisation, Name, Role (Worker or Manager), Status (Invited, Active or Deactivated), User ID: by
--      organisation, then name.
--
-- Run it as a member of the Entra group "Grow2Notes SQL admins", the SQL Entra admin, from the root of a clone of this
-- repository, with the sign-in, the temporary firewall rule for your own IP address and the retry for error 40613 that
-- the header of infra/sql/check-database.sql describes:
--
--   sqlcmd -S <server>.database.windows.net -d <database> \
--     --authentication-method ActiveDirectoryDefault -N strict -b -W -i ops/find-ids.sql

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

SELECT o.Name AS [Organisation], o.Id AS [Organisation ID]
FROM dbo.Organisation AS o
ORDER BY o.Name, o.Id;

SELECT
    o.Name AS [Organisation],
    p.FamilyName AS [Family name],
    p.GivenName AS [Given name],
    CASE p.Status WHEN 1 THEN N'Active' WHEN 2 THEN N'Archived' END AS [Status],
    p.Id AS [Participant ID]
FROM dbo.Participant AS p
INNER JOIN dbo.Organisation AS o ON o.Id = p.OrganisationId
ORDER BY o.Name, o.Id, p.FamilyName, p.GivenName, p.Id;

SELECT
    o.Name AS [Organisation],
    u.DisplayName AS [Name],
    CASE u.Role WHEN 1 THEN N'Worker' WHEN 2 THEN N'Manager' END AS [Role],
    CASE u.Status WHEN 0 THEN N'Invited' WHEN 1 THEN N'Active' WHEN 2 THEN N'Deactivated' END AS [Status],
    u.Id AS [User ID]
FROM dbo.AspNetUsers AS u
INNER JOIN dbo.Organisation AS o ON o.Id = u.OrganisationId
ORDER BY o.Name, o.Id, u.DisplayName, u.Id;
