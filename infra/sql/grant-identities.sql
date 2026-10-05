-- Gives one environment's Azure identities their access to its database (design.md §9.4, §10.6):
--   id-grow2notes-<env>-deploy, which runs the migrations, becomes a member of db_owner;
--   id-grow2notes-<env>-app, which the web app runs as, becomes a member of grow2notes_runtime and nothing else, so it
--   has no DDL rights.
-- Run it before the first migration. Running it again is safe: it adds only what is missing.
--
-- Run it as a member of the Entra group "Grow2Notes SQL admins", the server's Entra admin, with sqlcmd (Go)
-- (https://aka.ms/go-sqlcmd), which signs in with your Azure CLI sign-in (az login). The server accepts connections
-- only from the VNet, so first add a firewall rule for your own IP address (az sql server firewall-rule create), and
-- delete it straight after, even if the script fails. For test, run this from the root of a clone of this repository,
-- in Bash (Git Bash on Windows):
--
--   sqlcmd -S sql-grow2notes-test.database.windows.net -d sqldb-grow2notes \
--     --authentication-method ActiveDirectoryDefault -N strict -b -v Environment=test \
--     -i infra/sql/grant-identities.sql
--
-- For prod, use sql-grow2notes-prod and Environment=prod. -b makes sqlcmd exit with 1 when the script fails, in which
-- case it has changed nothing. If the test database has paused, the first attempt fails with error 40613 while it
-- resumes; run the command again after a minute.

SET NOCOUNT ON;
-- Any error rolls back the whole transaction, so a failed run leaves the database as it was.
SET XACT_ABORT ON;

IF N'$(Environment)' NOT IN (N'test', N'prod')
    THROW 50000, N'Environment must be test or prod.', 1;
IF DB_NAME() <> N'sqldb-grow2notes'
    THROW 50000, N'Run this in the database sqldb-grow2notes (sqlcmd -d sqldb-grow2notes), not in master.', 1;

BEGIN TRANSACTION;

IF DATABASE_PRINCIPAL_ID(N'id-grow2notes-$(Environment)-deploy') IS NULL
BEGIN
    CREATE USER [id-grow2notes-$(Environment)-deploy] FROM EXTERNAL PROVIDER;
    PRINT N'Created the user id-grow2notes-$(Environment)-deploy.';
END
ELSE
    PRINT N'The user id-grow2notes-$(Environment)-deploy already exists.';

IF NOT EXISTS (SELECT * FROM sys.database_role_members
               WHERE [role_principal_id] = DATABASE_PRINCIPAL_ID(N'db_owner')
                 AND [member_principal_id] = DATABASE_PRINCIPAL_ID(N'id-grow2notes-$(Environment)-deploy'))
BEGIN
    ALTER ROLE [db_owner] ADD MEMBER [id-grow2notes-$(Environment)-deploy];
    PRINT N'Added id-grow2notes-$(Environment)-deploy to db_owner.';
END
ELSE
    PRINT N'id-grow2notes-$(Environment)-deploy is already a member of db_owner.';

-- InitialCreate's own guard, so whichever of this script and the migration runs first creates the role, and the other
-- leaves it as it is. The migration gives the role its grants and denies either way.
IF DATABASE_PRINCIPAL_ID(N'grow2notes_runtime') IS NULL
BEGIN
    CREATE ROLE [grow2notes_runtime];
    PRINT N'Created the role grow2notes_runtime, which the first migration gives its grants and denies.';
END
ELSE
    PRINT N'The role grow2notes_runtime already exists.';

IF DATABASE_PRINCIPAL_ID(N'id-grow2notes-$(Environment)-app') IS NULL
BEGIN
    CREATE USER [id-grow2notes-$(Environment)-app] FROM EXTERNAL PROVIDER;
    PRINT N'Created the user id-grow2notes-$(Environment)-app.';
END
ELSE
    PRINT N'The user id-grow2notes-$(Environment)-app already exists.';

IF NOT EXISTS (SELECT * FROM sys.database_role_members
               WHERE [role_principal_id] = DATABASE_PRINCIPAL_ID(N'grow2notes_runtime')
                 AND [member_principal_id] = DATABASE_PRINCIPAL_ID(N'id-grow2notes-$(Environment)-app'))
BEGIN
    ALTER ROLE [grow2notes_runtime] ADD MEMBER [id-grow2notes-$(Environment)-app];
    PRINT N'Added id-grow2notes-$(Environment)-app to grow2notes_runtime.';
END
ELSE
    PRINT N'id-grow2notes-$(Environment)-app is already a member of grow2notes_runtime.';

COMMIT;
PRINT N'Committed.';
