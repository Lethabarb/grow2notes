using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The database role <c>grow2notes_runtime</c>, of which the app's database identity is a member (design.md §5.8,
/// §10.6). The migrations give it its grants and denies, so the database refuses the changes and deletes that the app's
/// code must never make. <see cref="AppendOnly"/> and <see cref="NeverDeleted"/> are the one place a table is named
/// here: a new append-only table goes in <see cref="AppendOnly"/>, a new never-deleted table goes in
/// <see cref="NeverDeleted"/>, and the migration that adds the table adds its denies. A guard fails while an
/// <see cref="IAppendOnly"/> entity's table is missing from <see cref="AppendOnly"/>, the exact-permission test fails
/// until the migration adds the denies, and the error-229 theories prove them.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class RuntimeRoleTests(SqlServerFixture sqlServer)
{
    // Denied UPDATE and DELETE: history that is only ever added to (design.md §5.1, Deletes; §5.8).
    private static readonly string[] AppendOnly =
        ["AuditEvent", "NoteReview", "NoteVersion", "NoteVersionGroup", "NoteVersionItem"];

    // Denied DELETE only: rows that are archived or deactivated, never deleted. Disjoint from AppendOnly.
    private static readonly string[] NeverDeleted =
        ["AspNetUsers", "CommonItem", "CommonItemGroup", "Goal", "Note", "Organisation", "Participant"];

    public static TheoryData<string> DeleteDenied { get; } = [.. AppendOnly, .. NeverDeleted];

    public static TheoryData<string> UpdateDenied { get; } = [.. AppendOnly];

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

        // Ordinal, because the query's ORDER BY follows the database collation.
        var expected = AppendOnly.Concat(NeverDeleted).Select(table => $"DENY DELETE ON dbo.{table}")
            .Concat(AppendOnly.Select(table => $"DENY UPDATE ON dbo.{table}"));
        Assert.Equal(expected.Order(StringComparer.Ordinal), permissions.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(DeleteDenied))]
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
    [MemberData(nameof(UpdateDenied))]
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
    public async Task Every_IAppendOnly_entity_s_table_is_in_the_append_only_list()
    {
        // Building the app's model opens no connection (ModelTenancyTests does the same).
        await using var app = new AppWithoutDatabase("Production");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        var tables = db.Model.GetEntityTypes()
            .Where(entity => typeof(IAppendOnly).IsAssignableFrom(entity.ClrType))
            .Select(entity => entity.GetTableName()!);

        Assert.Equal(AppendOnly.Order(StringComparer.Ordinal), tables.Order(StringComparer.Ordinal));
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
    public async Task
        A_member_of_the_role_can_read_add_and_change_rows_and_delete_an_authenticator_key_a_passkey_and_a_draft()
    {
        await AsMemberOfTheRoleAsync(async session =>
        {
            // A denied statement fails the whole call with error 229, naming the permission and the table. The
            // authenticator key, the passkeys and the draft are among the rows the app does delete: in a sign-in
            // reset, and as the note's first version is saved, as here: the header moves on, the version and its rows
            // are added, and the working copy is deleted (design.md §5.1, Deletes; §5.8).
            var row = await session.ReadRowAsync("""
                DECLARE @organisationId uniqueidentifier = NEWID(), @userId uniqueidentifier = NEWID(),
                        @participantId uniqueidentifier = NEWID(), @goalId uniqueidentifier = NEWID(),
                        @groupId uniqueidentifier = NEWID(), @itemId uniqueidentifier = NEWID(),
                        @noteId uniqueidentifier = NEWID(), @versionId uniqueidentifier = NEWID();

                INSERT INTO [dbo].[Organisation] ([Id], [Name], [CreatedAtUtc])
                VALUES (@organisationId, N'Added', SYSUTCDATETIME());
                INSERT INTO [dbo].[AspNetUsers]
                    ([Id], [OrganisationId], [DisplayName], [Role], [Status], [InvitedAtUtc], [EmailConfirmed],
                     [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnabled], [AccessFailedCount])
                VALUES (@userId, @organisationId, N'Added', 1, 0, SYSUTCDATETIME(), 0, 0, 0, 0, 0);
                INSERT INTO [dbo].[AspNetUserTokens] ([UserId], [LoginProvider], [Name], [Value])
                VALUES (@userId, N'[AspNetUserStore]', N'AuthenticatorKey', N'key');
                INSERT INTO [dbo].[Participant]
                    ([Id], [OrganisationId], [GivenName], [FamilyName], [DateOfBirth], [Status], [CreatedAtUtc])
                VALUES (@participantId, @organisationId, N'Added', N'Added', '1990-01-01', 1, SYSUTCDATETIME());
                INSERT INTO [dbo].[Goal] ([Id], [OrganisationId], [ParticipantId], [Text], [SortOrder], [CreatedAtUtc])
                VALUES (@goalId, @organisationId, @participantId, N'Added', 0, SYSUTCDATETIME());
                INSERT INTO [dbo].[CommonItemGroup]
                    ([Id], [OrganisationId], [Name], [SortOrder], [IsEveryNote], [CreatedAtUtc])
                VALUES (@groupId, @organisationId, N'Added', 0, 0, SYSUTCDATETIME());
                INSERT INTO [dbo].[CommonItem] ([Id], [OrganisationId], [GroupId], [Text], [SortOrder], [CreatedAtUtc])
                VALUES (@itemId, @organisationId, @groupId, N'Added', 0, SYSUTCDATETIME());
                INSERT INTO [dbo].[Note]
                    ([Id], [OrganisationId], [ParticipantId], [NoteDate], [AuthorUserId], [Status], [CreatedAtUtc],
                     [CurrentVersionNumber], [FlagStatus])
                VALUES (@noteId, @organisationId, @participantId, '2026-10-04', @userId, 0, SYSUTCDATETIME(), 0, 0);
                INSERT INTO [dbo].[NoteDraft]
                    ([NoteId], [EditorUserId], [OrganisationId], [BaseVersionNumber], [Groups], [Items], [Narrative],
                     [IsFlagged], [LastClientId], [LastSeq], [CreatedAtUtc], [UpdatedAtUtc])
                VALUES (@noteId, @userId, @organisationId, 0, N'[]', N'[]', N'Added', 0, NEWID(), 1, SYSUTCDATETIME(),
                        SYSUTCDATETIME());

                UPDATE [dbo].[Organisation] SET [Name] = N'Changed' WHERE [Id] = @organisationId;
                UPDATE [dbo].[AspNetUsers] SET [DisplayName] = N'Changed' WHERE [Id] = @userId;
                UPDATE [dbo].[Participant] SET [FamilyName] = N'Changed' WHERE [Id] = @participantId;
                UPDATE [dbo].[Goal] SET [Text] = N'Changed' WHERE [Id] = @goalId;
                UPDATE [dbo].[CommonItemGroup] SET [Name] = N'Changed' WHERE [Id] = @groupId;
                UPDATE [dbo].[CommonItem] SET [Text] = N'Changed' WHERE [Id] = @itemId;
                UPDATE [dbo].[NoteDraft] SET [Narrative] = N'Changed' WHERE [NoteId] = @noteId;
                UPDATE [dbo].[Note]
                SET [Status] = 1, [CurrentVersionNumber] = 1, [FirstSubmittedAtUtc] = SYSUTCDATETIME()
                WHERE [Id] = @noteId;
                INSERT INTO [dbo].[NoteVersion]
                    ([Id], [OrganisationId], [NoteId], [VersionNumber], [Narrative], [IsFlagged], [CreatedByUserId],
                     [CreatedAtUtc], [IdempotencyKey])
                VALUES (@versionId, @organisationId, @noteId, 1, N'Added', 0, @userId, SYSUTCDATETIME(), NEWID());
                INSERT INTO [dbo].[NoteVersionGroup]
                    ([NoteVersionId], [Position], [OrganisationId], [CommonItemGroupId], [Name], [IsEveryNote],
                     [IsPicked])
                VALUES (@versionId, 0, @organisationId, @groupId, N'Added', 0, 1);
                INSERT INTO [dbo].[NoteVersionItem]
                    ([NoteVersionId], [Section], [Position], [OrganisationId], [GoalId], [CommonItemId],
                     [GroupPosition], [Text], [IsTicked])
                VALUES (@versionId, 1, 0, @organisationId, @goalId, NULL, NULL, N'Added', 1),
                       (@versionId, 2, 0, @organisationId, NULL, @itemId, 0, N'Added', 0);
                INSERT INTO [dbo].[NoteReview]
                    ([Id], [OrganisationId], [NoteId], [VersionNumber], [ReviewedByUserId], [ReviewedAtUtc], [Comment])
                VALUES (NEWID(), @organisationId, @noteId, 1, @userId, SYSUTCDATETIME(), NULL);
                INSERT INTO [dbo].[AspNetUserPasskeys] ([CredentialId], [UserId], [Data])
                VALUES (CONVERT(varbinary(1024), NEWID()), @userId, N'{}');
                DELETE FROM [dbo].[AspNetUserPasskeys] WHERE [UserId] = @userId;
                DELETE FROM [dbo].[AspNetUserTokens] WHERE [UserId] = @userId;
                DELETE FROM [dbo].[NoteDraft] WHERE [NoteId] = @noteId;

                SELECT o.[Name], u.[DisplayName],
                       (SELECT COUNT(*) FROM [dbo].[AspNetUserTokens] AS t WHERE t.[UserId] = u.[Id]),
                       (SELECT p.[FamilyName] FROM [dbo].[Participant] AS p WHERE p.[Id] = @participantId),
                       (SELECT g.[Text] FROM [dbo].[Goal] AS g WHERE g.[Id] = @goalId),
                       (SELECT cg.[Name] FROM [dbo].[CommonItemGroup] AS cg WHERE cg.[Id] = @groupId),
                       (SELECT ci.[Text] FROM [dbo].[CommonItem] AS ci WHERE ci.[Id] = @itemId),
                       (SELECT n.[Status] FROM [dbo].[Note] AS n WHERE n.[Id] = @noteId),
                       (SELECT v.[VersionNumber] FROM [dbo].[NoteVersion] AS v WHERE v.[NoteId] = @noteId),
                       (SELECT COUNT(*) FROM [dbo].[NoteVersionItem] AS i WHERE i.[NoteVersionId] = @versionId),
                       (SELECT COUNT(*) FROM [dbo].[NoteDraft] AS d WHERE d.[NoteId] = @noteId),
                       (SELECT COUNT(*) FROM [dbo].[NoteReview] AS r WHERE r.[NoteId] = @noteId),
                       (SELECT COUNT(*) FROM [dbo].[AspNetUserPasskeys] AS k WHERE k.[UserId] = u.[Id])
                FROM [dbo].[AspNetUsers] AS u
                JOIN [dbo].[Organisation] AS o ON o.[Id] = u.[OrganisationId]
                WHERE u.[Id] = @userId;
                """);

            Assert.Equal(
                ["Changed", "Changed", 0, "Changed", "Changed", "Changed", "Changed", (byte)1, 1, 2, 0, 1, 0], row);
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
