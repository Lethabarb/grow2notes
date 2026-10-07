using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// All that logs and telemetry may record about a database failure (design.md §9.5): the exception's type and SQL
/// Server's error number. Never the message, which can hold the values that failed, such as a truncated value or a
/// duplicate key.
/// </summary>
internal readonly record struct DatabaseFailure(string ExceptionType, int? SqlErrorNumber)
{
    /// <summary>
    /// Finds a database failure in <paramref name="exception"/> or its inner exceptions: a <see cref="DbException"/>,
    /// such as <see cref="SqlException"/>, or EF Core's <see cref="DbUpdateException"/>. The type is the outermost
    /// exception's, because EF Core and the retrying execution strategy wrap the one SqlClient threw; the number is the
    /// first <see cref="SqlException"/>'s.
    /// </summary>
    public static DatabaseFailure? Find(Exception exception)
    {
        var isDatabaseFailure = false;
        for (var inner = exception; inner is not null; inner = inner.InnerException)
        {
            if (inner is SqlException sql)
            {
                return new(exception.GetType().FullName!, sql.Number);
            }

            isDatabaseFailure |= inner is DbException or DbUpdateException;
        }

        return isDatabaseFailure ? new(exception.GetType().FullName!, null) : null;
    }
}
