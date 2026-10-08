using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Tests.Platform;

public sealed class DatabaseFailureTests
{
    [Fact]
    public void A_failed_save_gives_EF_Core_s_exception_type_and_the_number_of_the_SqlException_inside_it()
    {
        var exception = new DbUpdateException(
            "An error occurred while saving the entity changes.", TestSqlException.Create(2601, "Duplicate key."));

        Assert.Equal(
            new DatabaseFailure("Microsoft.EntityFrameworkCore.DbUpdateException", 2601),
            DatabaseFailure.Find(exception));
    }

    [Fact]
    public void A_SqlException_deeper_inside_another_exception_is_found()
    {
        // As EF Core reports a transient failure that the retrying strategy did not retry.
        var exception = new InvalidOperationException(
            "An exception has been raised that is likely due to a transient failure.",
            new DbUpdateException("Saving failed.", TestSqlException.Create(1205, "Deadlocked.")));

        Assert.Equal(new DatabaseFailure("System.InvalidOperationException", 1205), DatabaseFailure.Find(exception));
    }

    [Fact]
    public void A_database_failure_without_a_SqlException_has_no_number()
    {
        var exception = new DbUpdateConcurrencyException("The row changed.");

        Assert.Equal(
            new DatabaseFailure("Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException", null),
            DatabaseFailure.Find(exception));
    }

    [Fact]
    public void Other_exceptions_are_not_database_failures()
    {
        var exception = new InvalidOperationException("Not the database.", new TimeoutException("Nor this."));

        Assert.Null(DatabaseFailure.Find(exception));
    }
}
