using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// One account per email address across the whole app (A27). The database refuses the second account itself, so the
/// rule holds even when two requests pass the app's own check at the same time.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class UniqueEmailTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    private static readonly DateTime TimeUtc = new(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("Sam.Taylor@example.org")]
    [InlineData("sam.taylor@example.org")]
    [InlineData("SAM.TAYLOR@EXAMPLE.ORG")]
    [InlineData("sAM.tAYLOR@eXAMPLE.oRG")]
    public async Task A_second_account_with_the_same_email_in_any_letter_case_is_refused_by_the_database(string email)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // In a transaction that is never committed, so neither account stays in the shared database. The retrying
        // execution strategy refuses a transaction begun outside it.
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            db.Users.Add(NewUser(NewOrganisation(), "Sam.Taylor@example.org"));
            await db.SaveChangesAsync(cancellationToken);

            // In another organisation, because the address is unique across the whole app.
            db.Users.Add(NewUser(NewOrganisation(), email));
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(cancellationToken));

            // 2601: a duplicate key in a unique index.
            var refusal = Assert.IsType<SqlException>(error.InnerException);
            Assert.Equal(2601, refusal.Number);
            Assert.Contains("'EmailIndex'", refusal.Message, StringComparison.Ordinal);
        });

        Organisation NewOrganisation()
        {
            var organisation = new Organisation { Name = "Unique email test", CreatedAtUtc = TimeUtc };
            db.Organisations.Add(organisation);
            return organisation;
        }

        // Added straight to the context, past UserManager's validation, which the database must not depend on. It has
        // no user name, so EmailIndex is the only unique index the row can break: UserNameIndex, also unique, will hold
        // the address too once accounts are created through UserManager.
        ApplicationUser NewUser(Organisation organisation, string address) => new()
        {
            OrganisationId = organisation.Id,
            DisplayName = "Sam Taylor",
            Role = UserRole.Worker,
            Status = UserStatus.Invited,
            InvitedAtUtc = TimeUtc,
            Email = address,
            NormalizedEmail = userManager.NormalizeEmail(address),
        };
    }
}
