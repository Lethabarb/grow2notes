using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Data;

/// <summary>
/// The query filter that keeps every query on an <see cref="ITenantOwned"/> entity to the tenant's rows (design.md
/// §5.9 item 2, D3). It is a named filter, so other filters can sit beside it, and the operator commands can lift this
/// one alone with <c>IgnoreQueryFilters([TenantQueryFilter.Name])</c>, the only place that may.
/// </summary>
internal static class TenantQueryFilter
{
    public const string Name = "Tenant";

    /// <summary>
    /// Gives every <see cref="ITenantOwned"/> entity type in <paramref name="builder"/>'s model the filter
    /// <c>OrganisationId == tenantId</c>. It sees only the entity types the model has when it is called, so a context
    /// calls it at the end of <c>OnModelCreating</c>.
    /// </summary>
    /// <param name="builder">The model builder of the context's <c>OnModelCreating</c>.</param>
    /// <param name="tenantId">
    /// A property of the context that reads the tenant, written inside the context as <c>() => TenantId</c>. EF Core
    /// builds the model, filters included, once per context type and shares it. A property of the context is read
    /// again for each query, on the context that runs it, so each scope queries as its own tenant and no tenant is
    /// needed until then. Anything else, such as a captured variable, would keep the value from the context that built
    /// the model, so it is refused.
    /// </param>
    public static void Apply(ModelBuilder builder, Expression<Func<Guid>> tenantId)
    {
        if (tenantId.Body is not MemberExpression { Expression: ConstantExpression { Value: DbContext } })
        {
            throw new ArgumentException(
                "The tenant must be a property of the context, read as () => TenantId inside it.", nameof(tenantId));
        }

        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(entityType => typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
                     .ToList())
        {
            var row = Expression.Parameter(entityType.ClrType, "row");
            var filter = Expression.Lambda(
                Expression.Equal(Expression.Property(row, nameof(ITenantOwned.OrganisationId)), tenantId.Body),
                row);

            builder.Entity(entityType.ClrType).HasQueryFilter(Name, filter);
        }
    }
}
