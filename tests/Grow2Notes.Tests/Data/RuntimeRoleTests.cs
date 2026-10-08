using Grow2Notes.Tests.Fixtures;
using Microsoft.Data.SqlClient;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The database role <c>grow2notes_runtime</c>, of which the app's database identity is a member (design.md §5.8,
/// §10.6). The migrations give it its grants and denies, so the database refuses the changes and deletes that the app's
/// code must never make. A migration that adds a deny adds it to these tests too.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class RuntimeRoleTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task The_role_is_a_member_of_db_datareader_and_db_datawriter()
    {
        var roles = await ReadColumnAsync("""
            SELECT r.[name]
            FROM sys.database_role_members AS m
            JOIN sys.database_principals AS r ON r.[principal_id] = m.[role_principal_id]
            WHERE m.[member_principal_id] = DATABASE_PRINCIPAL_ID(N'grow2notes_runtime')
            ORDER BY r.[name];
            """);

        Assert.Equal(["db_datareader", "db_datawriter"], roles);
    }

    [Fact]
    public async Task The_role_has_no_permissions_of_its_own_except_its_denies()
    {
        var permissions = await ReadColumnAsync("""
            SELECT CONCAT(p.[state_desc], N' ', p.[permission_name], N' ON ',
                          OBJECT_SCHEMA_NAME(p.[major_id]), N'.', OBJECT_NAME(p.[major_id]))
            FROM sys.database_permissions AS p
            WHERE p.[grantee_principal_id] = DATABASE_PRINCIPAL_ID(N'grow2notes_runtime')
            ORDER BY 1;
            """);

        Assert.Equal(
            [
                "DENY DELETE ON dbo.AspNetUsers",
                "DENY DELETE ON dbo.AuditEvent",
                "DENY DELETE ON dbo.Organisation",
                "DENY UPDATE ON dbo.AuditEvent",
            ],
            permissions);
    }

    [Theory]
    [InlineData("Organisation")]
    [InlineData("AspNetUsers")]
    [InlineData("AuditEvent")]
    public async Task A_member_of_the_role_gets_error_229_for_a_delete(string table)
    {
        await AsMemberOfTheRoleAsync(async session =>
        {
            // SQL Server checks the permission before it touches a row, so what the table holds does not matter. Were
            // the delete allowed, the rollback would undo it.
            var error = await Assert.ThrowsAsync<SqlException>(() =>
                session.ExecuteAsync($"DELETE FROM [dbo].[{table}];"));

            // 229: the permission was denied on the object.
            Assert.Equal(229, error.Number);
        });
    }

    [Theory]
    [InlineData("AuditEvent")]
    public async Task A_member_of_the_role_gets_error_229_for_an_update(string table)
    {
        await AsMemberOfTheRoleAsync(async session =>
        {
            // As for a delete, the permission is checked before any row is touched. Every append-only table is
            // tenant-owned, so each has the column.
            var error = await Assert.ThrowsAsync<SqlException>(() =>
                session.ExecuteAsync($"UPDATE [dbo].[{table}] SET [OrganisationId] = [OrganisationId];"));

            Assert.Equal(229, error.Number);
        });
    }

    [Fact]
    public async Task A_member_of_the_role_can_add_and_read_an_audit_event()
    {
        await AsMemberOfTheRoleAsync(async session =>
        {
            // AuditEvent has no foreign keys (design.md §5.3), so the row needs no organisation row to point at.
            var row = await session.ReadRowAsync("""
                DECLARE @organisationId uniqueidentifier = NEWID();

                INSERT INTO [dbo].[AuditEvent] ([OrganisationId], [OccurredAtUtc], [EventType])
                VALUES (@organisationId, SYSUTCDATETIME(), 'auth.signin_succeeded');

                SELECT [EventType] FROM [dbo].[AuditEvent] WHERE [OrganisationId] = @organisationId;
                """);

            Assert.Equal(["auth.signin_succeeded"], row);
        });
    }

    [Fact]
    public async Task A_member_of_the_role_can_read_add_and_change_rows_and_delete_an_authenticator_key()
    {
        await AsMemberOfTheRoleAsync(async session =>
        {
            // A denied statement fails the whole call with error 229, naming the permission and the table. The
            // authenticator key is one of the rows the app does delete, in a sign-in reset (design.md §5.1, Deletes).
            var row = await session.ReadRowAsync("""
                DECLARE @organisationId uniqueidentifier = NEWID(), @userId uniqueidentifier = NEWID();

                INSERT INTO [dbo].[Organisation] ([Id], [Name], [CreatedAtUtc])
                VALUES (@organisationId, N'Added', SYSUTCDATETIME());
                INSERT INTO [dbo].[AspNetUsers]
                    ([Id], [OrganisationId], [DisplayName], [Role], [Status], [InvitedAtUtc], [EmailConfirmed],
                     [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnabled], [AccessFailedCount])
                VALUES (@userId, @organisationId, N'Added', 1, 0, SYSUTCDATETIME(), 0, 0, 0, 0, 0);
                INSERT INTO [dbo].[AspNetUserTokens] ([UserId], [LoginProvider], [Name], [Value])
                VALUES (@userId, N'[AspNetUserStore]', N'AuthenticatorKey', N'key');

                UPDATE [dbo].[Organisation] SET [Name] = N'Changed' WHERE [Id] = @organisationId;
                UPDATE [dbo].[AspNetUsers] SET [DisplayName] = N'Changed' WHERE [Id] = @userId;
                DELETE FROM [dbo].[AspNetUserTokens] WHERE [UserId] = @userId;

                SELECT o.[Name], u.[DisplayName],
                       (SELECT COUNT(*) FROM [dbo].[AspNetUserTokens] AS t WHERE t.[UserId] = u.[Id])
                FROM [dbo].[AspNetUsers] AS u
                JOIN [dbo].[Organisation] AS o ON o.[Id] = u.[OrganisationId]
                WHERE u.[Id] = @userId;
                """);

            Assert.Equal(["Changed", "Changed", 0], row);
        });
    }

    /// <summary>
    /// Runs <paramref name="work"/> as a member of the role, as the app's identity is in Azure. A database user without
    /// a login stands in for that identity, and <c>EXECUTE AS</c> leaves the statements with that user's permissions
    /// alone. It all happens in a transaction that is rolled back, the user included, so the shared database is left as
    /// it was.
    /// </summary>
    private async Task AsMemberOfTheRoleAsync(Func<Session, Task> work)
    {
        await using var connection = new SqlConnection(sqlServer.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = connection.BeginTransaction();
        var session = new Session(connection, transaction);

        await session.ExecuteAsync("""
            CREATE USER [runtime_role_member] WITHOUT LOGIN;
            ALTER ROLE [grow2notes_runtime] ADD MEMBER [runtime_role_member];
            EXECUTE AS USER = N'runtime_role_member';
            """);
        try
        {
            await work(session);
        }
        finally
        {
            // Back to sa first: rolling back the creation of the user that the session is impersonating kills the
            // session.
            await session.ExecuteAsync("REVERT;");
        }
    }

    private async Task<List<string>> ReadColumnAsync(string sql)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(sqlServer.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        List<string> values = [];
        while (await reader.ReadAsync(cancellationToken))
            values.Add(reader.GetString(0));
        return values;
    }

    /// <summary>A connection with its open transaction, which SqlClient requires every command on it to name.</summary>
    private sealed class Session(SqlConnection connection, SqlTransaction transaction)
    {
        public async Task ExecuteAsync(string sql)
        {
            await using var command = new SqlCommand(sql, connection, transaction);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        public async Task<object[]> ReadRowAsync(string sql)
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            await using var command = new SqlCommand(sql, connection, transaction);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            Assert.True(await reader.ReadAsync(cancellationToken));

            var row = new object[reader.FieldCount];
            reader.GetValues(row);
            return row;
        }
    }
}
