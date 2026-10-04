using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The design.md §5.1 conventions that no entity in the app's model shows yet, applied to a stand-in entity of the
/// kind later stories add: exposed through a plural DbSet, with a calendar date. Building a model opens no connection.
/// </summary>
public sealed class ModelConventionsOnNewEntitiesTests
{
    [Fact]
    public void A_table_takes_its_entity_name_not_the_DbSet_name()
    {
        using var db = new StandInContext();

        Assert.Equal(nameof(Visit), db.Model.FindEntityType(typeof(Visit))!.GetTableName());
    }

    [Fact]
    public void A_DateOnly_column_is_date()
    {
        using var db = new StandInContext();

        var visitDate = db.Model.FindEntityType(typeof(Visit))!.FindProperty(nameof(Visit.VisitDate))!;

        Assert.Equal("date", visitDate.GetColumnType());
    }

    private sealed class StandInContext : DbContext
    {
        public DbSet<Visit> Visits => Set<Visit>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlServer();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
            ModelConventions.Apply(configurationBuilder);
    }

    private sealed class Visit
    {
        public Guid Id { get; set; }

        public DateOnly VisitDate { get; set; }
    }
}
