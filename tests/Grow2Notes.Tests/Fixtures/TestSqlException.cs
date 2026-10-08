using System.Reflection;
using Microsoft.Data.SqlClient;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Builds a <see cref="SqlException"/> as SqlClient does when SQL Server reports an error, for tests that need one
/// without a server. SqlException has no public constructor, so this calls SqlClient's internal ones by reflection; if
/// a SqlClient update moves them, the tests that use this fail rather than pass unchecked.
/// </summary>
internal static class TestSqlException
{
    public static SqlException Create(int number, string message)
    {
        const BindingFlags Internal = BindingFlags.Instance | BindingFlags.NonPublic;
        Type[] errorParameters =
        [
            typeof(int), typeof(byte), typeof(byte), typeof(string), typeof(string), typeof(string), typeof(int),
            typeof(Exception),
        ];
        var error = (SqlError)typeof(SqlError).GetConstructor(Internal, errorParameters)!
            .Invoke([number, (byte)0, (byte)14, "test", message, "", 1, null]);

        var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        typeof(SqlErrorCollection).GetMethod("Add", Internal)!.Invoke(errors, [error]);

        var create = typeof(SqlException).GetMethod("CreateException", BindingFlags.Static | BindingFlags.NonPublic,
            [typeof(SqlErrorCollection), typeof(string)]);
        return (SqlException)create!.Invoke(null, [errors, null])!;
    }
}
