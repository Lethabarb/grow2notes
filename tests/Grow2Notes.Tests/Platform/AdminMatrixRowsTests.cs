using Grow2Notes.Web.Platform;
using static System.Net.HttpStatusCode;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The endpoint matrix's admin rows (design.md §14 M1: a worker calling any configuration endpoint gets <c>403</c>):
/// every row of <see cref="EndpointMatrix"/> whose route starts with <c>/api/admin</c> must expect <c>401</c> signed
/// out and <c>403</c> for A's worker and A's Support person, as the <see cref="Policies.Manager"/> policy of the
/// <c>/api/admin</c> group (<see cref="ApiGroup.MapAdminGroup"/>) answers them. The check reads the rows,
/// not the endpoints, so each admin endpoint's row is held to all three as it is added, and
/// <see cref="EndpointMatrixTests"/> shows that the endpoint answers as its row says. The row's other two statuses are
/// its story's: A's manager's, and B's manager's, which is <c>404</c> where the endpoint loads one of A's records
/// (§9.2). The rows need no database, so the tests run without Docker. Until the first admin endpoint, the matrix has
/// no admin row, so made-up rows show the check failing.
/// </summary>
public sealed class AdminMatrixRowsTests
{
    [Fact]
    public void Every_admin_row_expects_401_signed_out_and_403_for_the_worker_and_Support()
    {
        var breaches = BreachesOf(EndpointMatrix.Rows);

        // Every breach at once, rather than the first.
        Assert.True(
            breaches is [],
            string.Join(Environment.NewLine, ["Admin rows not as the Manager policy answers:", .. breaches]));
    }

    // Beside the matrix's rows, which let A's worker in at endpoints outside /api/admin, such as /api/auth/me, made-up
    // admin rows as a story that got its row wrong would write them, and one as it should be.
    [Fact]
    public void The_check_names_each_admin_row_that_lets_the_worker_Support_or_a_signed_out_caller_in_and_no_other()
    {
        EndpointRow[] madeUp =
        [
            // After the route: signed out, A's worker, A's manager, B's manager, A's Support.
            new(["PUT"], "/api/admin/made-up/worker-let-in", Unauthorized, OK, OK, NotFound, Forbidden),
            new(["GET"], "/api/admin/made-up/anyone-let-in", OK, OK, OK, OK, OK),
            new(["PUT"], "/api/admin/made-up/support-let-in", Unauthorized, Forbidden, OK, NotFound, OK),
            new(["PUT"], "/api/admin/made-up/as-it-should-be", Unauthorized, Forbidden, OK, NotFound, Forbidden),
        ];

        var breaches = BreachesOf([.. EndpointMatrix.Rows, .. madeUp]);

        Assert.Equal(
            [
                "GET /api/admin/made-up/anyone-let-in: " +
                    "signed out 200, not 401; A's worker 200, not 403; A's Support 200, not 403",
                "PUT /api/admin/made-up/support-let-in: A's Support 200, not 403",
                "PUT /api/admin/made-up/worker-let-in: A's worker 200, not 403",
            ],
            breaches.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Each row under <c>/api/admin</c> in <paramref name="rows"/> that does not expect <c>401</c> signed out and
    /// <c>403</c> for A's worker and A's Support person, with each status it expects instead.
    /// </summary>
    private static List<string> BreachesOf(IEnumerable<EndpointRow> rows) =>
    [
        .. from row in rows
           where EndpointMatrix.IsAdmin(row.Route)
           let wrong = WrongStatusesOf(row).ToList()
           where wrong is not []
           select $"{EndpointMatrix.NameOf(row)}: {string.Join("; ", wrong)}"
    ];

    private static IEnumerable<string> WrongStatusesOf(EndpointRow row)
    {
        if (row.SignedOut is not Unauthorized)
        {
            yield return $"signed out {(int)row.SignedOut}, not {(int)Unauthorized}";
        }

        if (row.WorkerOfA is not Forbidden)
        {
            yield return $"A's worker {(int)row.WorkerOfA}, not {(int)Forbidden}";
        }

        if (row.SupportOfA is not Forbidden)
        {
            yield return $"A's Support {(int)row.SupportOfA}, not {(int)Forbidden}";
        }
    }
}
