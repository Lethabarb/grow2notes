-- Checks one environment's database after a deploy (design.md §9.3, §9.4, §10.6; S00.02.02's check in test). It only
-- reads: catalog views, two DMVs and the key ring table, and EXECUTE AS USER ... REVERT to see what each identity may
-- do. It creates, changes and deletes nothing, so it is safe to run as often as you like.
--
-- The first result set has one row per check, with PASS, FAIL or INFO, and a summary row at the end:
--   A  id-grow2notes-<env>-app, which the web app runs as, is a member of grow2notes_runtime and of no other role, owns
--      nothing, and has no DDL right: no CREATE or ALTER, and no CONTROL or TAKE OWNERSHIP on the database or the dbo
--      schema.
--   B  id-grow2notes-<env>-deploy, which runs the migrations, is a member of db_owner and has CONTROL on the database.
--      dbo owns the dbo schema and every table in it: a db_owner member that creates a table does not own it. The
--      tables and the migrations history are the migrations'.
--   C  The Data Protection key ring, in dbo.DataProtectionKeys, holds one key, and Key Vault wraps it: the key has an
--      encryptedSecret whose decryptorType is AzureKeyVaultXmlDecryptor and whose kid is the data-protection key in
--      kv-grow2notes-<env>, and no unencrypted masterKey. The app identity can read and add keys.
--   D  grow2notes_runtime is in db_datareader and db_datawriter only, and is denied DELETE on dbo.Organisation and
--      dbo.AspNetUsers, and UPDATE and DELETE on dbo.AuditEvent; impersonating the app identity shows the denies in
--      effect.
--   E  Entra-only authentication is on, no database user signs in with a SQL password, and no Entra group is a
--      database user. For information only: the app identity's open sessions.
-- The second result set has one row per row of the key ring, as dates and 1/0 flags. To check that a restart of the
-- web app keeps the key ring, run this before and after the restart: C2 and the same single row (row_id and
-- creation_date) show it.
--
-- When a migration adds tables or denies (design.md §10.6), update B7's table list, B8's expected history, D3's
-- denies and D5's probes below; D3 fails until it is updated.
--
-- It prints nothing secret: no SID (an Entra user's SID is the identity's client or object ID), no key ring XML, key
-- ID or Key Vault key version, and no principal name but the two identities, grow2notes_runtime and the built-in
-- names; any other shows as "<other ...>". It never calls ORIGINAL_LOGIN() or SUSER_SNAME(), which would show the
-- operator's sign-in.
--
-- Run it as a member of the Entra group "Grow2Notes SQL admins", the server's Entra admin, who enters the database as
-- dbo (which may impersonate every user and view definitions and database state), with sqlcmd (Go)
-- (https://aka.ms/go-sqlcmd), which signs in with your Azure CLI sign-in (az login). The server accepts connections
-- only from the VNet, so first add a firewall rule for your own IP address (az sql server firewall-rule create), and
-- delete it straight after, even if the script fails. For test, run this from the root of a clone of this repository,
-- in Bash (Git Bash on Windows):
--
--   sqlcmd -S sql-grow2notes-test.database.windows.net -d sqldb-grow2notes \
--     --authentication-method ActiveDirectoryDefault -N strict -b -W -v Environment=test \
--     -i infra/sql/check-database.sql
--
-- For prod, use sql-grow2notes-prod and Environment=prod. -b makes sqlcmd exit with 1 if a guard stops the script.
-- If the test database has paused, the first attempt fails with error 40613 while it resumes; run the command again
-- after a minute.
--
-- EXECUTE AS USER works for users created FROM EXTERNAL PROVIDER: Microsoft Learn's "Microsoft Entra server
-- principals" (Limitations and remarks) says impersonating Entra database users in a user database is supported. Azure
-- SQL Database cannot impersonate Entra logins, so this script impersonates users only, and A6 shows that the switch
-- worked. An impersonated user has its database roles and permissions but no server permissions and no Entra group
-- memberships; E3 shows that no group is a database user, so a group could give the app nothing.

SET NOCOUNT ON;
-- The xml methods in section C need these session options, and sqlcmd can start with QUOTED_IDENTIFIER OFF.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF N'$(Environment)' NOT IN (N'test', N'prod')
    THROW 50000, N'Environment must be test or prod.', 1;
IF DB_NAME() <> N'sqldb-grow2notes'
    THROW 50000, N'Run this in the database sqldb-grow2notes (sqlcmd -d sqldb-grow2notes), not in master.', 1;
IF ISNULL(IS_ROLEMEMBER(N'db_owner'), 0) <> 1
    THROW 50000, N'Run this as the SQL Entra admin (a member of "Grow2Notes SQL admins"), who enters as dbo.', 1;

-- Names from infra/main.bicep, infra/modules/keyvault.bicep, grant-identities.sql and the InitialCreate migration.
DECLARE @AppUser sysname = N'id-grow2notes-$(Environment)-app';
DECLARE @DeployUser sysname = N'id-grow2notes-$(Environment)-deploy';
DECLARE @RuntimeRole sysname = N'grow2notes_runtime';
-- The versionless key URI that the app wraps keys with (DataProtection__KeyVaultKeyUri). Each wrapped key's kid is
-- this prefix followed by a key version, which this script compares but never prints.
DECLARE @KidPrefix nvarchar(200) = N'https://kv-grow2notes-$(Environment).vault.azure.net/keys/data-protection/';

DECLARE @StartUser sysname = USER_NAME();
DECLARE @AppId int = DATABASE_PRINCIPAL_ID(@AppUser);
DECLARE @DeployId int = DATABASE_PRINCIPAL_ID(@DeployUser);
DECLARE @RuntimeId int = DATABASE_PRINCIPAL_ID(@RuntimeRole);
DECLARE @DboId int = DATABASE_PRINCIPAL_ID(N'dbo');
-- Used only to recognise the app identity's sessions in E4. Never printed.
DECLARE @AppSid varbinary(85) = (SELECT sid FROM sys.database_principals WHERE principal_id = @AppId);

PRINT CONCAT(N'Database checks, database ', DB_NAME(), N', environment $(Environment), at ',
             CONVERT(nvarchar(30), SYSUTCDATETIME(), 126), N'Z');

------------------------------------------------------------------------------------------------------------------------
-- A. The app identity: grow2notes_runtime only, owns nothing, no DDL rights
------------------------------------------------------------------------------------------------------------------------

DECLARE @a1 nvarchar(400) = (
    SELECT CONCAT(type_desc, N', authentication ', authentication_type_desc,
                  N', default schema ', ISNULL(default_schema_name, N'(none)'))
    FROM sys.database_principals
    WHERE principal_id = @AppId);

-- Roles the user is a direct member of.
DECLARE @a2 nvarchar(max) = (
    SELECT STRING_AGG(r.name, N', ') WITHIN GROUP (ORDER BY r.name)
    FROM sys.database_role_members AS rm
    JOIN sys.database_principals AS r ON r.principal_id = rm.role_principal_id
    WHERE rm.member_principal_id = @AppId);

-- Roles the user belongs to directly or through other roles.
DECLARE @a3 nvarchar(max), @a3Unexpected int;
WITH chain AS (
    SELECT rm.role_principal_id
    FROM sys.database_role_members AS rm
    WHERE rm.member_principal_id = @AppId
    UNION ALL
    SELECT rm.role_principal_id
    FROM sys.database_role_members AS rm
    JOIN chain AS c ON rm.member_principal_id = c.role_principal_id
)
SELECT @a3 = STRING_AGG(USER_NAME(d.role_principal_id), N', ') WITHIN GROUP (ORDER BY USER_NAME(d.role_principal_id)),
       @a3Unexpected = SUM(CASE WHEN USER_NAME(d.role_principal_id)
                                     IN (@RuntimeRole, N'db_datareader', N'db_datawriter')
                                THEN 0 ELSE 1 END)
FROM (SELECT DISTINCT role_principal_id FROM chain) AS d;

-- Permissions granted or denied to the user itself, as opposed to its roles. CREATE USER grants CONNECT.
DECLARE @a4 nvarchar(max), @a4Unexpected int, @a4Connect int;
SELECT @a4 = STRING_AGG(CONCAT(p.state_desc, N' ', p.permission_name, N' on ', p.class_desc,
                               CASE p.class
                                   WHEN 0 THEN N''
                                   WHEN 1 THEN CONCAT(N' ', OBJECT_SCHEMA_NAME(p.major_id), N'.',
                                                      OBJECT_NAME(p.major_id))
                                   WHEN 3 THEN CONCAT(N' ', SCHEMA_NAME(p.major_id))
                                   ELSE N' (target not shown)'
                               END), N'; ') WITHIN GROUP (ORDER BY p.class, p.permission_name),
       @a4Unexpected = SUM(CASE WHEN p.class = 0 AND p.permission_name = N'CONNECT' AND p.state = 'G'
                                THEN 0 ELSE 1 END),
       @a4Connect = SUM(CASE WHEN p.class = 0 AND p.permission_name = N'CONNECT' AND p.state = 'G'
                             THEN 1 ELSE 0 END)
FROM sys.database_permissions AS p
WHERE p.grantee_principal_id = @AppId;

-- Ownership gives CONTROL, so the user must own nothing.
DECLARE @a5 int =
      (SELECT COUNT(*) FROM sys.schemas WHERE principal_id = @AppId)
    + (SELECT COUNT(*) FROM sys.objects WHERE principal_id = @AppId)
    + (SELECT COUNT(*) FROM sys.database_principals WHERE owning_principal_id = @AppId)
    + (SELECT COUNT(*) FROM sys.types WHERE principal_id = @AppId)
    + (SELECT COUNT(*) FROM sys.xml_schema_collections WHERE principal_id = @AppId)
    + (SELECT COUNT(*) FROM sys.assemblies WHERE principal_id = @AppId)
    + (SELECT COUNT(*) FROM sys.certificates WHERE principal_id = @AppId)
    + (SELECT COUNT(*) FROM sys.asymmetric_keys WHERE principal_id = @AppId)
    + (SELECT COUNT(*) FROM sys.symmetric_keys WHERE principal_id = @AppId);
DECLARE @a5DatabaseOwner int = CASE WHEN EXISTS (
    SELECT 1
    FROM sys.databases AS d
    JOIN sys.database_principals AS p ON p.sid = d.owner_sid
    WHERE d.database_id = DB_ID() AND p.principal_id = @AppId) THEN 1 ELSE 0 END;

-- What the app identity can actually do, seen from inside its own security context.
DECLARE @Impersonating bit = 0;
DECLARE @a6 sysname, @a6Error nvarchar(4000);
DECLARE @a7 nvarchar(max), @a7Ddl int;
DECLARE @a8 nvarchar(max), @a8Ddl int;
DECLARE @a9Granted nvarchar(max), @a9Unknown int, @a9Probes int;
DECLARE @a10 nvarchar(600), @a10Pass int;
DECLARE @c7 nvarchar(200), @c7Pass int;
DECLARE @d5 nvarchar(800), @d5Pass int;

BEGIN TRY
    EXECUTE AS USER = @AppUser;
    SET @Impersonating = 1;

    SET @a6 = USER_NAME();

    -- DDL and control: any CREATE or ALTER permission, CONTROL, TAKE OWNERSHIP or IMPERSONATE.
    SELECT @a7 = STRING_AGG(permission_name, N', ') WITHIN GROUP (ORDER BY permission_name),
           @a7Ddl = SUM(CASE WHEN permission_name LIKE N'CREATE %' OR permission_name LIKE N'ALTER%'
                              OR permission_name LIKE N'IMPERSONATE%'
                              OR permission_name IN (N'CONTROL', N'TAKE OWNERSHIP') THEN 1 ELSE 0 END)
    FROM fn_my_permissions(NULL, N'DATABASE');

    SELECT @a8 = STRING_AGG(permission_name, N', ') WITHIN GROUP (ORDER BY permission_name),
           @a8Ddl = SUM(CASE WHEN permission_name LIKE N'CREATE %' OR permission_name LIKE N'ALTER%'
                              OR permission_name LIKE N'IMPERSONATE%'
                              OR permission_name IN (N'CONTROL', N'TAKE OWNERSHIP') THEN 1 ELSE 0 END)
    FROM fn_my_permissions(N'dbo', N'SCHEMA');

    -- Direct probes of the rights a migration would need. Any 1 is a failure, and so is NULL (not resolvable).
    SELECT @a9Granted = STRING_AGG(CASE WHEN probe.granted = 1 THEN probe.label END, N', '),
           @a9Unknown = SUM(CASE WHEN probe.granted IS NULL THEN 1 ELSE 0 END),
           @a9Probes = COUNT(*)
    FROM (VALUES
        (N'CREATE TABLE',                 HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CREATE TABLE')),
        (N'CREATE VIEW',                  HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CREATE VIEW')),
        (N'CREATE PROCEDURE',             HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CREATE PROCEDURE')),
        (N'CREATE FUNCTION',              HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CREATE FUNCTION')),
        (N'CREATE SCHEMA',                HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CREATE SCHEMA')),
        (N'CREATE ROLE',                  HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CREATE ROLE')),
        (N'ALTER on DATABASE',            HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'ALTER')),
        (N'ALTER ANY SCHEMA',             HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'ALTER ANY SCHEMA')),
        (N'ALTER ANY USER',               HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'ALTER ANY USER')),
        (N'ALTER ANY ROLE',               HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'ALTER ANY ROLE')),
        (N'CONTROL on DATABASE',          HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CONTROL')),
        (N'TAKE OWNERSHIP on DATABASE',   HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'TAKE OWNERSHIP')),
        (N'ALTER on SCHEMA dbo',          HAS_PERMS_BY_NAME(N'dbo', N'SCHEMA', N'ALTER')),
        (N'CONTROL on SCHEMA dbo',        HAS_PERMS_BY_NAME(N'dbo', N'SCHEMA', N'CONTROL')),
        (N'TAKE OWNERSHIP on SCHEMA dbo', HAS_PERMS_BY_NAME(N'dbo', N'SCHEMA', N'TAKE OWNERSHIP')),
        (N'ALTER on dbo.Organisation',    HAS_PERMS_BY_NAME(N'dbo.Organisation', N'OBJECT', N'ALTER')),
        (N'ALTER on dbo.AspNetUsers',     HAS_PERMS_BY_NAME(N'dbo.AspNetUsers', N'OBJECT', N'ALTER')),
        (N'ALTER on dbo.__EFMigrationsHistory', HAS_PERMS_BY_NAME(N'dbo.__EFMigrationsHistory', N'OBJECT', N'ALTER'))
    ) AS probe(label, granted);

    SET @a10 = CONCAT(
        N'grow2notes_runtime=', ISNULL(CONVERT(nvarchar(4), IS_ROLEMEMBER(@RuntimeRole)), N'NULL'),
        N', db_datareader=', ISNULL(CONVERT(nvarchar(4), IS_ROLEMEMBER(N'db_datareader')), N'NULL'),
        N', db_datawriter=', ISNULL(CONVERT(nvarchar(4), IS_ROLEMEMBER(N'db_datawriter')), N'NULL'),
        N', db_owner=', ISNULL(CONVERT(nvarchar(4), IS_ROLEMEMBER(N'db_owner')), N'NULL'),
        N', db_ddladmin=', ISNULL(CONVERT(nvarchar(4), IS_ROLEMEMBER(N'db_ddladmin')), N'NULL'),
        N', db_securityadmin=', ISNULL(CONVERT(nvarchar(4), IS_ROLEMEMBER(N'db_securityadmin')), N'NULL'),
        N', db_accessadmin=', ISNULL(CONVERT(nvarchar(4), IS_ROLEMEMBER(N'db_accessadmin')), N'NULL'),
        N', db_backupoperator=', ISNULL(CONVERT(nvarchar(4), IS_ROLEMEMBER(N'db_backupoperator')), N'NULL'));
    SET @a10Pass = CASE WHEN IS_ROLEMEMBER(@RuntimeRole) = 1
                          AND IS_ROLEMEMBER(N'db_datareader') = 1
                          AND IS_ROLEMEMBER(N'db_datawriter') = 1
                          AND ISNULL(IS_ROLEMEMBER(N'db_owner'), 1) = 0
                          AND ISNULL(IS_ROLEMEMBER(N'db_ddladmin'), 1) = 0
                          AND ISNULL(IS_ROLEMEMBER(N'db_securityadmin'), 1) = 0
                          AND ISNULL(IS_ROLEMEMBER(N'db_accessadmin'), 1) = 0
                          AND ISNULL(IS_ROLEMEMBER(N'db_backupoperator'), 1) = 0 THEN 1 ELSE 0 END;

    -- The app reads the key ring and adds keys to it.
    SET @c7 = CONCAT(
        N'SELECT=',
        ISNULL(CONVERT(nvarchar(4), HAS_PERMS_BY_NAME(N'dbo.DataProtectionKeys', N'OBJECT', N'SELECT')), N'NULL'),
        N', INSERT=',
        ISNULL(CONVERT(nvarchar(4), HAS_PERMS_BY_NAME(N'dbo.DataProtectionKeys', N'OBJECT', N'INSERT')), N'NULL'));
    SET @c7Pass = CASE WHEN HAS_PERMS_BY_NAME(N'dbo.DataProtectionKeys', N'OBJECT', N'SELECT') = 1
                         AND HAS_PERMS_BY_NAME(N'dbo.DataProtectionKeys', N'OBJECT', N'INSERT') = 1 THEN 1 ELSE 0 END;

    -- The role's denies, in effect for the app identity.
    SET @d5 = CONCAT(
        N'Organisation SELECT=',
        ISNULL(CONVERT(nvarchar(4), HAS_PERMS_BY_NAME(N'dbo.Organisation', N'OBJECT', N'SELECT')), N'NULL'),
        N' INSERT=',
        ISNULL(CONVERT(nvarchar(4), HAS_PERMS_BY_NAME(N'dbo.Organisation', N'OBJECT', N'INSERT')), N'NULL'),
        N' UPDATE=',
        ISNULL(CONVERT(nvarchar(4), HAS_PERMS_BY_NAME(N'dbo.Organisation', N'OBJECT', N'UPDATE')), N'NULL'),
        N' DELETE=',
        ISNULL(CONVERT(nvarchar(4), HAS_PERMS_BY_NAME(N'dbo.Organisation', N'OBJECT', N'DELETE')), N'NULL'),
        N'; AspNetUsers SELECT=',
        ISNULL(CONVERT(nvarchar(4), HAS_PERMS_BY_NAME(N'dbo.AspNetUsers', N'OBJECT', N'SELECT')), N'NULL'),
        N' INSERT=',
        ISNULL(CONVERT(nvarchar(4), HAS_PERMS_BY_NAME(N'dbo.AspNetUsers', N'OBJECT', N'INSERT')), N'NULL'),
        N' UPDATE=',
        ISNULL(CONVERT(nvarchar(4), HAS_PERMS_BY_NAME(N'dbo.AspNetUsers', N'OBJECT', N'UPDATE')), N'NULL'),
        N' DELETE=',
        ISNULL(CONVERT(nvarchar(4), HAS_PERMS_BY_NAME(N'dbo.AspNetUsers', N'OBJECT', N'DELETE')), N'NULL'),
        N'; AuditEvent SELECT=',
        ISNULL(CONVERT(nvarchar(4), HAS_PERMS_BY_NAME(N'dbo.AuditEvent', N'OBJECT', N'SELECT')), N'NULL'),
        N' INSERT=',
        ISNULL(CONVERT(nvarchar(4), HAS_PERMS_BY_NAME(N'dbo.AuditEvent', N'OBJECT', N'INSERT')), N'NULL'),
        N' UPDATE=',
        ISNULL(CONVERT(nvarchar(4), HAS_PERMS_BY_NAME(N'dbo.AuditEvent', N'OBJECT', N'UPDATE')), N'NULL'),
        N' DELETE=',
        ISNULL(CONVERT(nvarchar(4), HAS_PERMS_BY_NAME(N'dbo.AuditEvent', N'OBJECT', N'DELETE')), N'NULL'),
        N'; for comparison, AspNetUserClaims DELETE=',
        ISNULL(CONVERT(nvarchar(4), HAS_PERMS_BY_NAME(N'dbo.AspNetUserClaims', N'OBJECT', N'DELETE')), N'NULL'));
    SET @d5Pass = CASE WHEN HAS_PERMS_BY_NAME(N'dbo.Organisation', N'OBJECT', N'SELECT') = 1
                         AND HAS_PERMS_BY_NAME(N'dbo.Organisation', N'OBJECT', N'INSERT') = 1
                         AND HAS_PERMS_BY_NAME(N'dbo.Organisation', N'OBJECT', N'UPDATE') = 1
                         AND HAS_PERMS_BY_NAME(N'dbo.Organisation', N'OBJECT', N'DELETE') = 0
                         AND HAS_PERMS_BY_NAME(N'dbo.AspNetUsers', N'OBJECT', N'SELECT') = 1
                         AND HAS_PERMS_BY_NAME(N'dbo.AspNetUsers', N'OBJECT', N'INSERT') = 1
                         AND HAS_PERMS_BY_NAME(N'dbo.AspNetUsers', N'OBJECT', N'UPDATE') = 1
                         AND HAS_PERMS_BY_NAME(N'dbo.AspNetUsers', N'OBJECT', N'DELETE') = 0
                         AND HAS_PERMS_BY_NAME(N'dbo.AuditEvent', N'OBJECT', N'SELECT') = 1
                         AND HAS_PERMS_BY_NAME(N'dbo.AuditEvent', N'OBJECT', N'INSERT') = 1
                         AND HAS_PERMS_BY_NAME(N'dbo.AuditEvent', N'OBJECT', N'UPDATE') = 0
                         AND HAS_PERMS_BY_NAME(N'dbo.AuditEvent', N'OBJECT', N'DELETE') = 0 THEN 1 ELSE 0 END;

    REVERT;
    SET @Impersonating = 0;
END TRY
BEGIN CATCH
    SET @a6Error = CONCAT(N'error ', ERROR_NUMBER(), N': ', ERROR_MESSAGE());
    IF @Impersonating = 1
    BEGIN
        REVERT;
        SET @Impersonating = 0;
    END
END CATCH;

------------------------------------------------------------------------------------------------------------------------
-- B. The deployment identity: db_owner; the schema and its tables belong to dbo; the migrations history
------------------------------------------------------------------------------------------------------------------------

DECLARE @b1 nvarchar(400) = (
    SELECT CONCAT(type_desc, N', authentication ', authentication_type_desc,
                  N', default schema ', ISNULL(default_schema_name, N'(none)'))
    FROM sys.database_principals
    WHERE principal_id = @DeployId);

DECLARE @b2 nvarchar(max) = (
    SELECT STRING_AGG(r.name, N', ') WITHIN GROUP (ORDER BY r.name)
    FROM sys.database_role_members AS rm
    JOIN sys.database_principals AS r ON r.principal_id = rm.role_principal_id
    WHERE rm.member_principal_id = @DeployId);

DECLARE @b3 nvarchar(400), @b3Pass int, @b3Error nvarchar(4000);
BEGIN TRY
    EXECUTE AS USER = @DeployUser;
    SET @Impersonating = 1;

    SET @b3 = CONCAT(
        N'user=', CASE WHEN USER_NAME() = @DeployUser THEN @DeployUser ELSE N'<other>' END,
        N', db_owner=',
        ISNULL(CONVERT(nvarchar(4), IS_ROLEMEMBER(N'db_owner')), N'NULL'),
        N', CONTROL on DATABASE=',
        ISNULL(CONVERT(nvarchar(4), HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CONTROL')), N'NULL'),
        N', ALTER on SCHEMA dbo=',
        ISNULL(CONVERT(nvarchar(4), HAS_PERMS_BY_NAME(N'dbo', N'SCHEMA', N'ALTER')), N'NULL'),
        N', CREATE TABLE=',
        ISNULL(CONVERT(nvarchar(4), HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CREATE TABLE')), N'NULL'));
    SET @b3Pass = CASE WHEN USER_NAME() = @DeployUser
                         AND IS_ROLEMEMBER(N'db_owner') = 1
                         AND HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CONTROL') = 1
                         AND HAS_PERMS_BY_NAME(N'dbo', N'SCHEMA', N'ALTER') = 1
                         AND HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CREATE TABLE') = 1 THEN 1 ELSE 0 END;

    REVERT;
    SET @Impersonating = 0;
END TRY
BEGIN CATCH
    SET @b3Error = CONCAT(N'error ', ERROR_NUMBER(), N': ', ERROR_MESSAGE());
    IF @Impersonating = 1
    BEGIN
        REVERT;
        SET @Impersonating = 0;
    END
END CATCH;

-- Members of db_owner. dbo is always a member and may be listed. The Entra admin enters as dbo, so it has no row here.
DECLARE @b4 nvarchar(max) = (
    SELECT STRING_AGG(CASE WHEN m.type = 'R' OR m.name IN (@AppUser, @DeployUser, N'dbo') THEN m.name
                           ELSE CONCAT(N'<other ', LOWER(m.type_desc) COLLATE DATABASE_DEFAULT, N'>') END, N', ')
               WITHIN GROUP (ORDER BY m.name)
    FROM sys.database_role_members AS rm
    JOIN sys.database_principals AS m ON m.principal_id = rm.member_principal_id
    WHERE rm.role_principal_id = DATABASE_PRINCIPAL_ID(N'db_owner'));

DECLARE @b5 sysname = (
    SELECT CASE WHEN p.type = 'R' OR p.name IN (@AppUser, @DeployUser, N'dbo') THEN p.name
                ELSE CONCAT(N'<other ', LOWER(p.type_desc) COLLATE DATABASE_DEFAULT, N'>') END
    FROM sys.schemas AS s
    JOIN sys.database_principals AS p ON p.principal_id = s.principal_id
    WHERE s.name = N'dbo');

-- Owner of every user table. A table with no principal_id of its own belongs to its schema's owner.
DECLARE @b6 nvarchar(max), @b6Total int, @b6NotDbo int;
SELECT @b6 = STRING_AGG(CONCAT(d.owner_name, N' owns ', d.n, N' table(s) in schema ', d.schema_name), N'; '),
       @b6Total = SUM(d.n),
       @b6NotDbo = SUM(CASE WHEN d.owner_id = @DboId AND d.schema_name = N'dbo' THEN 0 ELSE d.n END)
FROM (
    SELECT s.name AS schema_name, p.principal_id AS owner_id, o.owner_name, COUNT(*) AS n
    FROM sys.tables AS t
    JOIN sys.schemas AS s ON s.schema_id = t.schema_id
    JOIN sys.database_principals AS p ON p.principal_id = COALESCE(t.principal_id, s.principal_id)
    CROSS APPLY (SELECT CASE WHEN p.type = 'R' OR p.name IN (@AppUser, @DeployUser, N'dbo') THEN p.name
                             ELSE CONCAT(N'<other ', LOWER(p.type_desc) COLLATE DATABASE_DEFAULT, N'>')
                        END AS owner_name) AS o
    WHERE t.is_ms_shipped = 0
    GROUP BY s.name, p.principal_id, o.owner_name
) AS d;

-- The tables that the migrations create, and the history table.
DECLARE @b7Missing nvarchar(max), @b7Extra nvarchar(max), @b7Present int;
SELECT @b7Missing = STRING_AGG(CASE WHEN t.object_id IS NULL THEN e.name END, N', '),
       @b7Extra = STRING_AGG(CASE WHEN e.name IS NULL THEN t.name END, N', '),
       @b7Present = SUM(CASE WHEN t.object_id IS NOT NULL AND e.name IS NOT NULL THEN 1 ELSE 0 END)
FROM (SELECT object_id, name FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo') AND is_ms_shipped = 0) AS t
FULL JOIN (VALUES (N'__EFMigrationsHistory'), (N'AspNetUserClaims'), (N'AspNetUserLogins'), (N'AspNetUserPasskeys'),
                  (N'AspNetUsers'), (N'AspNetUserTokens'), (N'AuditEvent'), (N'DataProtectionKeys'),
                  (N'Organisation')) AS e(name)
    ON e.name = t.name COLLATE DATABASE_DEFAULT;

DECLARE @b8 nvarchar(max) = (
    SELECT STRING_AGG(CONCAT(MigrationId, N' (EF Core ', ProductVersion, N')'), N', ')
               WITHIN GROUP (ORDER BY MigrationId)
    FROM dbo.[__EFMigrationsHistory]);
DECLARE @b8Expected int = (
    SELECT COUNT(*) FROM dbo.[__EFMigrationsHistory]
    WHERE MigrationId IN (N'20261004032521_InitialCreate', N'20261008130254_AddAuditEvent'));

------------------------------------------------------------------------------------------------------------------------
-- C. The Data Protection key ring in SQL, wrapped by the Key Vault key
------------------------------------------------------------------------------------------------------------------------

DECLARE @c1 nvarchar(400) = (
    SELECT STRING_AGG(CONCAT(c.name, N' ', TYPE_NAME(c.user_type_id),
                             CASE WHEN c.max_length = -1 THEN N'(max)' ELSE N'' END), N', ')
               WITHIN GROUP (ORDER BY c.column_id)
    FROM sys.columns AS c
    WHERE c.object_id = OBJECT_ID(N'dbo.DataProtectionKeys', N'U'));

-- One row per stored element: <key> for a key, <revocation> for a revocation. When Key Vault wraps a key, its
-- <masterKey> is replaced by <encryptedSecret decryptorType="...AzureKeyVaultXmlDecryptor, ..."> holding an
-- <encryptedKey> with the <kid> of the Key Vault key version that wrapped it. An unwrapped key keeps <masterKey> and
-- the comment "Warning: the key below is in an unencrypted form."
DECLARE @cRows int, @cKeys int, @cRevocations int, @cNotXml int, @cDecryptorOk int, @cKidOk int, @cUnencrypted int,
        @cWrapped int;
SELECT @cRows = COUNT(*),
       @cKeys = ISNULL(SUM(CASE WHEN v.root_element = N'key' THEN 1 ELSE 0 END), 0),
       @cRevocations = ISNULL(SUM(CASE WHEN v.root_element = N'revocation' THEN 1 ELSE 0 END), 0),
       @cNotXml = ISNULL(SUM(CASE WHEN px.x IS NULL THEN 1 ELSE 0 END), 0),
       @cDecryptorOk = ISNULL(SUM(CASE WHEN v.root_element = N'key' AND v.decryptor_is_key_vault = 1
                                       THEN 1 ELSE 0 END), 0),
       @cKidOk = ISNULL(SUM(CASE WHEN v.root_element = N'key' AND LEFT(v.kid, LEN(@KidPrefix)) = @KidPrefix
                                 THEN 1 ELSE 0 END), 0),
       @cUnencrypted = ISNULL(SUM(CASE WHEN v.has_master_key = 1 OR k.[Xml] LIKE N'%unencrypted form%'
                                       THEN 1 ELSE 0 END), 0),
       @cWrapped = ISNULL(SUM(CASE WHEN v.root_element = N'key' AND v.decryptor_is_key_vault = 1
                                    AND LEFT(v.kid, LEN(@KidPrefix)) = @KidPrefix AND v.has_master_key = 0
                                    AND k.[Xml] NOT LIKE N'%unencrypted form%' THEN 1 ELSE 0 END), 0)
FROM dbo.DataProtectionKeys AS k
CROSS APPLY (SELECT TRY_CONVERT(xml, k.[Xml]) AS x) AS px
CROSS APPLY (SELECT
    px.x.value(N'local-name((/*)[1])', N'nvarchar(50)') AS root_element,
    px.x.exist(N'//*[local-name()="encryptedSecret"]/@decryptorType[contains(., "AzureKeyVaultXmlDecryptor")]')
        AS decryptor_is_key_vault,
    px.x.value(N'(//*[local-name()="encryptedKey"]/*[local-name()="kid"]/text())[1]', N'nvarchar(400)') AS kid,
    px.x.exist(N'//*[local-name()="masterKey"]') AS has_master_key
) AS v;

------------------------------------------------------------------------------------------------------------------------
-- D. grow2notes_runtime: memberships, grants and denies
------------------------------------------------------------------------------------------------------------------------

DECLARE @d1 nvarchar(400) = (
    SELECT CONCAT(r.type_desc, N', owned by ',
                  CASE WHEN o.principal_id IS NULL THEN N'(nobody)'
                       WHEN o.type = 'R' OR o.name IN (@AppUser, @DeployUser, N'dbo') THEN o.name
                       ELSE CONCAT(N'<other ', LOWER(o.type_desc) COLLATE DATABASE_DEFAULT, N'>') END)
    FROM sys.database_principals AS r
    LEFT JOIN sys.database_principals AS o ON o.principal_id = r.owning_principal_id
    WHERE r.principal_id = @RuntimeId);
DECLARE @d1Pass int = CASE WHEN EXISTS (
    SELECT 1 FROM sys.database_principals
    WHERE principal_id = @RuntimeId AND type = 'R'
      AND ISNULL(owning_principal_id, -1) <> ISNULL(@AppId, -2)) THEN 1 ELSE 0 END;

DECLARE @d2 nvarchar(max) = (
    SELECT STRING_AGG(r.name, N', ') WITHIN GROUP (ORDER BY r.name)
    FROM sys.database_role_members AS rm
    JOIN sys.database_principals AS r ON r.principal_id = rm.role_principal_id
    WHERE rm.member_principal_id = @RuntimeId);

DECLARE @d3 nvarchar(max), @d3Count int, @d3Expected int;
SELECT @d3 = STRING_AGG(CONCAT(p.state_desc, N' ', p.permission_name, N' on ', p.class_desc,
                               CASE p.class
                                   WHEN 0 THEN N''
                                   WHEN 1 THEN CONCAT(N' ', OBJECT_SCHEMA_NAME(p.major_id), N'.',
                                                      OBJECT_NAME(p.major_id))
                                   WHEN 3 THEN CONCAT(N' ', SCHEMA_NAME(p.major_id))
                                   ELSE N' (target not shown)'
                               END), N'; ') WITHIN GROUP (ORDER BY p.class, OBJECT_NAME(p.major_id), p.permission_name),
       @d3Count = COUNT(*),
       @d3Expected = SUM(CASE WHEN p.class = 1 AND p.minor_id = 0 AND p.state = 'D'
                               AND (   (p.permission_name = N'DELETE'
                                        AND p.major_id IN (OBJECT_ID(N'dbo.Organisation', N'U'),
                                                           OBJECT_ID(N'dbo.AspNetUsers', N'U'),
                                                           OBJECT_ID(N'dbo.AuditEvent', N'U')))
                                    OR (p.permission_name = N'UPDATE'
                                        AND p.major_id = OBJECT_ID(N'dbo.AuditEvent', N'U')))
                              THEN 1 ELSE 0 END)
FROM sys.database_permissions AS p
WHERE p.grantee_principal_id = @RuntimeId;

DECLARE @d4 nvarchar(max) = (
    SELECT STRING_AGG(CASE WHEN m.type = 'R' OR m.name IN (@AppUser, @DeployUser, N'dbo') THEN m.name
                           ELSE CONCAT(N'<other ', LOWER(m.type_desc) COLLATE DATABASE_DEFAULT, N'>') END, N', ')
               WITHIN GROUP (ORDER BY m.name)
    FROM sys.database_role_members AS rm
    JOIN sys.database_principals AS m ON m.principal_id = rm.member_principal_id
    WHERE rm.role_principal_id = @RuntimeId);

------------------------------------------------------------------------------------------------------------------------
-- E. Authentication
------------------------------------------------------------------------------------------------------------------------

DECLARE @e1 int = CONVERT(int, SERVERPROPERTY(N'IsExternalAuthenticationOnly'));

-- Users that sign in with a SQL password: login-based (INSTANCE) or contained (DATABASE). dbo is left out: in Azure
-- SQL Database it is mapped to the server admin, and Entra-only authentication (E1) stops that login.
DECLARE @e2 int = (
    SELECT COUNT(*) FROM sys.database_principals
    WHERE authentication_type_desc IN (N'INSTANCE', N'DATABASE') AND name <> N'dbo');

DECLARE @e3 int = (SELECT COUNT(*) FROM sys.database_principals WHERE type = 'X');

-- Information only: the app identity's sessions open now. The app keeps pooled connections open for a few minutes
-- after its last query, so there may be none. 0x74000004 is TDS 7.4. Encrypt=Strict uses TDS 8.0, which shows a
-- different value.
DECLARE @e4Count int, @e4 nvarchar(max), @e4Error nvarchar(4000);
BEGIN TRY
    SELECT @e4Count = ISNULL(SUM(d.n), 0),
           @e4 = STRING_AGG(CONCAT(d.n, N' with encrypt_option ', d.encrypt_option, N', protocol_version ',
                                   d.protocol_hex, N', auth_scheme ', d.auth_scheme), N'; ')
    FROM (
        SELECT c.encrypt_option, h.protocol_hex, c.auth_scheme, COUNT(*) AS n
        FROM sys.dm_exec_sessions AS s
        JOIN sys.dm_exec_connections AS c ON c.session_id = s.session_id
        CROSS APPLY (SELECT CONVERT(varchar(10), CONVERT(varbinary(4), c.protocol_version), 1) AS protocol_hex) AS h
        WHERE s.is_user_process = 1
          AND (s.original_security_id = @AppSid OR s.security_id = @AppSid OR s.login_name = @AppUser)
        GROUP BY c.encrypt_option, h.protocol_hex, c.auth_scheme
    ) AS d;
END TRY
BEGIN CATCH
    SET @e4Error = CONCAT(N'error ', ERROR_NUMBER(), N': ', ERROR_MESSAGE());
END CATCH;

DECLARE @EndUser sysname = USER_NAME();

------------------------------------------------------------------------------------------------------------------------
-- Result set 1: one row per check, then the summary
------------------------------------------------------------------------------------------------------------------------

WITH checks AS (
    SELECT seq, id, [check], expected, actual, pass
    FROM (VALUES
        (101, N'A1', N'App identity is an Entra database user (FROM EXTERNAL PROVIDER)',
              N'EXTERNAL_USER, authentication EXTERNAL',
              ISNULL(@a1, N'(no such user)'),
              CASE WHEN @a1 LIKE N'EXTERNAL[_]USER, authentication EXTERNAL%' THEN 1 ELSE 0 END),
        (102, N'A2', N'App identity is a direct member of grow2notes_runtime and of no other role',
              N'grow2notes_runtime',
              ISNULL(@a2, N'(no roles)'),
              CASE WHEN @a2 = @RuntimeRole THEN 1 ELSE 0 END),
        (103, N'A3', N'App identity''s roles, including those it gets through other roles',
              N'only db_datareader, db_datawriter, grow2notes_runtime',
              ISNULL(@a3, N'(no roles)'),
              CASE WHEN @a3 IS NOT NULL AND ISNULL(@a3Unexpected, 0) = 0 THEN 1 ELSE 0 END),
        (104, N'A4', N'Permissions granted or denied to the app identity itself, not through its roles',
              N'GRANT CONNECT on DATABASE only',
              ISNULL(@a4, N'(none)'),
              CASE WHEN ISNULL(@a4Unexpected, 0) = 0 AND @a4Connect = 1 THEN 1 ELSE 0 END),
        (105, N'A5', N'App identity owns nothing (schemas, objects, roles, types, keys) and is not the database owner',
              N'0 owned; database owner: no',
              CONCAT(@a5, N' owned; database owner: ', CASE WHEN @a5DatabaseOwner = 1 THEN N'yes' ELSE N'no' END),
              CASE WHEN @a5 = 0 AND @a5DatabaseOwner = 0 THEN 1 ELSE 0 END),
        (106, N'A6', N'EXECUTE AS USER switches to the app identity (USER_NAME() while impersonating)',
              @AppUser,
              COALESCE(@a6Error,
                       CASE WHEN @a6 = @AppUser THEN @a6 WHEN @a6 IS NULL THEN N'(not run)' ELSE N'<other>' END),
              CASE WHEN @a6Error IS NULL AND @a6 = @AppUser THEN 1 ELSE 0 END),
        (107, N'A7', N'App identity''s effective permissions on the database (fn_my_permissions)',
              N'no CREATE, ALTER, CONTROL, TAKE OWNERSHIP or IMPERSONATE',
              CASE WHEN @a6Error IS NOT NULL THEN N'(not run, see A6)'
                   ELSE CONCAT(ISNULL(@a7Ddl, 0), N' DDL or control permission(s); all: ', ISNULL(@a7, N'(none)')) END,
              CASE WHEN @a6Error IS NULL AND @a7 IS NOT NULL AND ISNULL(@a7Ddl, 0) = 0 THEN 1 ELSE 0 END),
        (108, N'A8', N'App identity''s effective permissions on schema dbo (fn_my_permissions)',
              N'no ALTER, CONTROL, TAKE OWNERSHIP',
              CASE WHEN @a6Error IS NOT NULL THEN N'(not run, see A6)'
                   ELSE CONCAT(ISNULL(@a8Ddl, 0), N' DDL or control permission(s); all: ', ISNULL(@a8, N'(none)')) END,
              CASE WHEN @a6Error IS NULL AND ISNULL(@a8Ddl, 0) = 0 THEN 1 ELSE 0 END),
        (109, N'A9', N'DDL rights probed with HAS_PERMS_BY_NAME as the app identity',
              N'none granted, none unresolved',
              CASE WHEN @a6Error IS NOT NULL THEN N'(not run, see A6)'
                   ELSE CONCAT(N'granted: ', ISNULL(@a9Granted, N'none'), N'; unresolved: ', ISNULL(@a9Unknown, 0),
                               N' of ', ISNULL(@a9Probes, 0), N' probes') END,
              CASE WHEN @a6Error IS NULL AND @a9Probes > 0 AND @a9Granted IS NULL AND @a9Unknown = 0 THEN 1 ELSE 0 END),
        (110, N'A10', N'IS_ROLEMEMBER as the app identity',
              N'grow2notes_runtime, db_datareader and db_datawriter 1; every other role 0',
              COALESCE(@a10, N'(not run, see A6)'),
              CASE WHEN @a6Error IS NULL AND @a10Pass = 1 THEN 1 ELSE 0 END),

        (201, N'B1', N'Deployment identity is an Entra database user (FROM EXTERNAL PROVIDER)',
              N'EXTERNAL_USER, authentication EXTERNAL',
              ISNULL(@b1, N'(no such user)'),
              CASE WHEN @b1 LIKE N'EXTERNAL[_]USER, authentication EXTERNAL%' THEN 1 ELSE 0 END),
        (202, N'B2', N'Deployment identity is a direct member of db_owner',
              N'db_owner',
              ISNULL(@b2, N'(no roles)'),
              CASE WHEN @b2 = N'db_owner' THEN 1 ELSE 0 END),
        (203, N'B3', N'As the deployment identity: db_owner, CONTROL on the database, ALTER on dbo, CREATE TABLE',
              N'all 1',
              COALESCE(@b3Error, @b3, N'(not run)'),
              CASE WHEN @b3Error IS NULL AND @b3Pass = 1 THEN 1 ELSE 0 END),
        (204, N'B4', N'Members of db_owner (dbo may be listed too; it is always a member)',
              CONCAT(@DeployUser, N', and possibly dbo'),
              ISNULL(@b4, N'(none)'),
              CASE WHEN @b4 IN (@DeployUser, CONCAT(N'dbo, ', @DeployUser)) THEN 1 ELSE 0 END),
        (205, N'B5', N'Owner of schema dbo',
              N'dbo',
              ISNULL(@b5, N'(no schema dbo)'),
              CASE WHEN @b5 = N'dbo' THEN 1 ELSE 0 END),
        (206, N'B6', N'Owner of every user table (no table owned by either identity or outside dbo)',
              N'dbo owns all tables, in schema dbo',
              ISNULL(@b6, N'(no tables)'),
              CASE WHEN @b6Total > 0 AND @b6NotDbo = 0 THEN 1 ELSE 0 END),
        (207, N'B7', N'Tables created by the migrations, plus __EFMigrationsHistory',
              N'9 present, none missing',
              CONCAT(ISNULL(@b7Present, 0), N' present; missing: ', ISNULL(@b7Missing, N'none'),
                     N'; other tables: ', ISNULL(@b7Extra, N'none')),
              CASE WHEN @b7Present = 9 AND @b7Missing IS NULL THEN 1 ELSE 0 END),
        (208, N'B8', N'Migrations history (dbo.__EFMigrationsHistory)',
              N'20261004032521_InitialCreate (EF Core 10.0.12), 20261008130254_AddAuditEvent (EF Core 10.0.12)',
              ISNULL(@b8, N'(empty)'),
              CASE WHEN @b8Expected = 2 THEN 1 ELSE 0 END),

        (301, N'C1', N'Key ring table dbo.DataProtectionKeys and its columns',
              N'Id int, FriendlyName nvarchar(max), Xml nvarchar(max)',
              ISNULL(@c1, N'(no such table)'),
              CASE WHEN @c1 = N'Id int, FriendlyName nvarchar(max), Xml nvarchar(max)' THEN 1 ELSE 0 END),
        (302, N'C2', N'Number of keys in the key ring (still 1 after a restart)',
              N'1 key',
              CONCAT(@cKeys, N' key(s), ', @cRevocations, N' revocation(s), ', @cRows, N' row(s) in all'),
              CASE WHEN @cKeys = 1 THEN 1 ELSE 0 END),
        (303, N'C3', N'Every key has an encryptedSecret whose decryptorType is AzureKeyVaultXmlDecryptor',
              N'all keys',
              CONCAT(@cDecryptorOk, N' of ', @cKeys, N' key(s)'),
              CASE WHEN @cKeys > 0 AND @cDecryptorOk = @cKeys THEN 1 ELSE 0 END),
        (304, N'C4', N'Every key was wrapped with the data-protection key in kv-grow2notes-$(Environment) (kid)',
              N'all keys',
              CONCAT(@cKidOk, N' of ', @cKeys, N' key(s)'),
              CASE WHEN @cKeys > 0 AND @cKidOk = @cKeys THEN 1 ELSE 0 END),
        (305, N'C5', N'No row holds an unencrypted masterKey',
              N'0 rows',
              CONCAT(@cUnencrypted, N' row(s); wrapped keys: ', @cWrapped, N' of ', @cKeys),
              CASE WHEN @cUnencrypted = 0 AND @cKeys > 0 AND @cWrapped = @cKeys THEN 1 ELSE 0 END),
        (306, N'C6', N'Every row of the key ring is well-formed XML',
              N'0 rows that are not XML',
              CONCAT(@cNotXml, N' row(s) that are not XML'),
              CASE WHEN @cNotXml = 0 THEN 1 ELSE 0 END),
        (307, N'C7', N'As the app identity: SELECT and INSERT on dbo.DataProtectionKeys',
              N'SELECT=1, INSERT=1',
              COALESCE(@c7, N'(not run, see A6)'),
              CASE WHEN @a6Error IS NULL AND @c7Pass = 1 THEN 1 ELSE 0 END),

        (401, N'D1', N'Role grow2notes_runtime exists and the app identity does not own it',
              N'DATABASE_ROLE, owned by dbo',
              ISNULL(@d1, N'(no such role)'),
              @d1Pass),
        (402, N'D2', N'Roles that grow2notes_runtime is a member of',
              N'db_datareader, db_datawriter',
              ISNULL(@d2, N'(none)'),
              CASE WHEN @d2 = N'db_datareader, db_datawriter' THEN 1 ELSE 0 END),
        (403, N'D3', N'Grants and denies on grow2notes_runtime (from the migrations)',
              CONCAT(N'DENY DELETE on OBJECT_OR_COLUMN dbo.AspNetUsers; ',
                     N'DENY DELETE on OBJECT_OR_COLUMN dbo.AuditEvent; ',
                     N'DENY UPDATE on OBJECT_OR_COLUMN dbo.AuditEvent; ',
                     N'DENY DELETE on OBJECT_OR_COLUMN dbo.Organisation'),
              ISNULL(@d3, N'(none)'),
              CASE WHEN @d3Count = 4 AND @d3Expected = 4 THEN 1 ELSE 0 END),
        (404, N'D4', N'Members of grow2notes_runtime',
              @AppUser,
              ISNULL(@d4, N'(none)'),
              CASE WHEN @d4 = @AppUser THEN 1 ELSE 0 END),
        (405, N'D5',
              CONCAT(N'As the app identity: SELECT and INSERT allowed; DELETE refused on Organisation, ',
                     N'AspNetUsers and AuditEvent, and UPDATE on AuditEvent'),
              CONCAT(N'SELECT=1 INSERT=1 UPDATE=1 DELETE=0 on Organisation and AspNetUsers; ',
                     N'SELECT=1 INSERT=1 UPDATE=0 DELETE=0 on AuditEvent'),
              COALESCE(@d5, N'(not run, see A6)'),
              CASE WHEN @a6Error IS NULL AND @d5Pass = 1 THEN 1 ELSE 0 END),

        (501, N'E1', N'Microsoft Entra-only authentication (SERVERPROPERTY IsExternalAuthenticationOnly)',
              N'1',
              ISNULL(CONVERT(nvarchar(10), @e1), N'NULL'),
              CASE WHEN @e1 = 1 THEN 1 ELSE 0 END),
        (502, N'E2', N'Database users that sign in with a SQL password (other than dbo)',
              N'0',
              CONVERT(nvarchar(10), @e2),
              CASE WHEN @e2 = 0 THEN 1 ELSE 0 END),
        (503, N'E3', N'Entra groups that are database users (a group would add rights that EXECUTE AS cannot show)',
              N'0',
              CONVERT(nvarchar(10), @e3),
              CASE WHEN @e3 = 0 THEN 1 ELSE 0 END),
        (504, N'E4', N'App identity''s open sessions (information only)',
              N'encrypt_option TRUE',
              COALESCE(@e4Error,
                       CONCAT(@e4Count, N' session(s)', CASE WHEN @e4 IS NULL THEN N'' ELSE CONCAT(N': ', @e4) END)),
              CAST(NULL AS int)),
        (505, N'E5', N'This session runs as dbo and is back in its own context after the impersonation',
              N'dbo, restored',
              CONCAT(CASE WHEN @StartUser = N'dbo' THEN N'dbo' ELSE N'<not dbo>' END, N', ',
                     CASE WHEN @EndUser = @StartUser AND @Impersonating = 0 THEN N'restored' ELSE N'NOT restored' END),
              CASE WHEN @StartUser = N'dbo' AND @EndUser = @StartUser AND @Impersonating = 0 THEN 1 ELSE 0 END)
    ) AS v(seq, id, [check], expected, actual, pass)
)
SELECT id, [check], expected, actual,
       CASE pass WHEN 1 THEN N'PASS' WHEN 0 THEN N'FAIL' ELSE N'INFO' END AS result,
       seq
FROM checks
UNION ALL
SELECT N'ALL', N'Summary', N'no FAIL',
       CONCAT(SUM(CASE WHEN pass = 1 THEN 1 ELSE 0 END), N' PASS, ',
              SUM(CASE WHEN pass = 0 THEN 1 ELSE 0 END), N' FAIL, ',
              SUM(CASE WHEN pass IS NULL THEN 1 ELSE 0 END), N' INFO'),
       CASE WHEN SUM(CASE WHEN pass = 0 THEN 1 ELSE 0 END) = 0 THEN N'PASS' ELSE N'FAIL' END,
       999
FROM checks
ORDER BY seq;

------------------------------------------------------------------------------------------------------------------------
-- Result set 2: one row per row of the key ring, as dates and 1/0 flags. Compare row_id and creation_date before and
-- after a restart of the web app: the same single row means the key ring survived the restart.
------------------------------------------------------------------------------------------------------------------------

SELECT k.Id AS row_id,
       ISNULL(v.root_element, N'(not XML)') AS element,
       v.creation_date,
       v.activation_date,
       v.expiration_date,
       ISNULL(CONVERT(int, v.decryptor_is_key_vault), 0) AS decryptor_is_key_vault,
       CASE WHEN LEFT(v.kid, LEN(@KidPrefix)) = @KidPrefix THEN 1 ELSE 0 END AS kid_is_vault_data_protection_key,
       CASE WHEN v.has_master_key = 1 OR k.[Xml] LIKE N'%unencrypted form%' THEN 1 ELSE 0 END
           AS has_unencrypted_master_key,
       CASE WHEN k.[Xml] LIKE N'%This key is encrypted with Azure Key Vault%' THEN 1 ELSE 0 END
           AS says_encrypted_with_key_vault,
       CASE WHEN v.root_element = N'key' AND v.decryptor_is_key_vault = 1 AND LEFT(v.kid, LEN(@KidPrefix)) = @KidPrefix
                 AND v.has_master_key = 0 AND k.[Xml] NOT LIKE N'%unencrypted form%' THEN 1 ELSE 0 END
           AS wrapped_by_key_vault
FROM dbo.DataProtectionKeys AS k
CROSS APPLY (SELECT TRY_CONVERT(xml, k.[Xml]) AS x) AS px
CROSS APPLY (SELECT
    px.x.value(N'local-name((/*)[1])', N'nvarchar(50)') AS root_element,
    px.x.value(N'(/key/creationDate/text())[1]', N'nvarchar(40)') AS creation_date,
    px.x.value(N'(/key/activationDate/text())[1]', N'nvarchar(40)') AS activation_date,
    px.x.value(N'(/key/expirationDate/text())[1]', N'nvarchar(40)') AS expiration_date,
    px.x.exist(N'//*[local-name()="encryptedSecret"]/@decryptorType[contains(., "AzureKeyVaultXmlDecryptor")]')
        AS decryptor_is_key_vault,
    px.x.value(N'(//*[local-name()="encryptedKey"]/*[local-name()="kid"]/text())[1]', N'nvarchar(400)') AS kid,
    px.x.exist(N'//*[local-name()="masterKey"]') AS has_master_key
) AS v
ORDER BY k.Id;
