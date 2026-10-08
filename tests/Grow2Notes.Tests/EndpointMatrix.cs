using System.Net;
using Grow2Notes.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticAssets;
using static System.Net.HttpStatusCode;

namespace Grow2Notes.Tests;

/// <summary>
/// The endpoint matrix (design.md §2, §9.1, §9.2): a row for each endpoint the app maps, with the status that each
/// <see cref="Caller"/> gets from it. <see cref="EndpointMatrixTests"/> calls every endpoint in the app's
/// <c>EndpointDataSource</c> as each caller and checks the status against the endpoint's row, and an endpoint with no
/// row, or a row with no endpoint, fails the run. So each story that maps an endpoint adds its row here
/// (tests/README.md).
/// </summary>
internal static class EndpointMatrix
{
    /// <summary>
    /// The route of the one row for every endpoint that <c>MapStaticAssets</c> maps, a file of the SPA build each,
    /// whose names change with each build.
    /// </summary>
    public const string StaticAssets = "(MapStaticAssets)";

    /// <summary>
    /// The methods of an endpoint that names none, such as one mapped with <c>Map</c> or <c>MapFallback</c>, which
    /// answers every method; the matrix calls it with GET, POST, PUT and DELETE.
    /// </summary>
    public static readonly IReadOnlyList<string> AnyMethod = [];

    private static readonly IReadOnlyList<string> GetOrHead = ["GET", "HEAD"];

    private static readonly HttpMethod[] CalledForAnyMethod =
        [HttpMethod.Get, HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete];

    /// <summary>The rows, one for each endpoint, found by the methods it names and its route pattern.</summary>
    public static IReadOnlyList<EndpointRow> Rows { get; } =
    [
        // The SPA, for everyone, so a signed-out user gets the sign-in page: each file of the build, and index.html at
        // each client route.
        //  Methods    Route              Signed out A's worker A's manager B's manager Request
        new(GetOrHead, StaticAssets,      OK,        OK,        OK,         OK),
        new(GetOrHead, "{*path:nonfile}", OK,        OK,        OK,         OK,         At("/")),

        // A request that no other endpoint answers is a 404, never a 401: a path with a file extension that is no
        // file of the build, such as /favicon.ico, or a client route called with a method other than GET or HEAD.
        new(AnyMethod, "{*path}",         NotFound,  NotFound,  NotFound,   NotFound,   At("/favicon.ico")),

        // An unknown /api path is a 404 for every method, never the SPA page or a 401 (design.md §6.1).
        new(AnyMethod, "/api/{**rest}",   NotFound,  NotFound,  NotFound,   NotFound,   At("/api/no-such-endpoint")),

        // App Service's health check and the deploy's smoke test send no credentials. Readiness runs the database
        // check, which passes here.
        new(AnyMethod, "/healthz",        OK,        OK,        OK,         OK),
        new(AnyMethod, "/healthz/ready",  OK,        OK,        OK,         OK),
    ];

    // Two rows for one endpoint stop the type initializer here, naming the endpoint.
    private static readonly Dictionary<string, EndpointRow> RowsByKey =
        Rows.ToDictionary(row => KeyOf(row.Methods, row.Route));

    /// <summary>
    /// Every call of the endpoints in <paramref name="endpoints"/>: each endpoint with each method it names, or with
    /// GET, POST, PUT and DELETE when it names none.
    /// </summary>
    public static IEnumerable<EndpointCall> CallsTo(EndpointDataSource endpoints) =>
        from endpoint in endpoints.Endpoints.OfType<RouteEndpoint>()
        let methods = MethodsOf(endpoint)
        from method in methods is [] ? CalledForAnyMethod : methods.Select(name => HttpMethod.Parse(name))
        select new EndpointCall(method, endpoint);

    /// <summary>The row of <paramref name="endpoint"/>, or <see langword="null"/> when the matrix has none.</summary>
    public static EndpointRow? RowOf(RouteEndpoint endpoint) => RowsByKey.GetValueOrDefault(KeyOf(endpoint));

    /// <summary>
    /// The endpoints in <paramref name="endpoints"/> that have no row, and the rows that no endpoint there has, each
    /// named by its methods and route.
    /// </summary>
    public static (IReadOnlyList<string> EndpointsWithNoRow, IReadOnlyList<string> RowsWithNoEndpoint) Gaps(
        EndpointDataSource endpoints)
    {
        var endpointKeys = endpoints.Endpoints.OfType<RouteEndpoint>().Select(KeyOf).ToList();
        return ([.. endpointKeys.Except(RowsByKey.Keys)], [.. RowsByKey.Keys.Except(endpointKeys)]);
    }

    private static IReadOnlyList<string> MethodsOf(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? AnyMethod;

    private static string KeyOf(RouteEndpoint endpoint) => KeyOf(
        MethodsOf(endpoint),
        endpoint.Metadata.GetMetadata<StaticAssetDescriptor>() is null
            ? endpoint.RoutePattern.RawText ?? ""
            : StaticAssets);

    // The methods in any order, because a row lists them as written and an endpoint as mapped.
    private static string KeyOf(IReadOnlyList<string> methods, string route) =>
        methods is []
            ? $"any method {route}"
            : $"{string.Join(" or ", methods.Order(StringComparer.Ordinal))} {route}";

    // For a route with parameters: a path that its endpoint answers.
    private static Func<EndpointCall, Task<HttpRequestMessage>> At(string path) =>
        call => Task.FromResult(new HttpRequestMessage(call.Method, path));
}

/// <summary>
/// A row of the <see cref="EndpointMatrix"/>: an endpoint, found by the <paramref name="Methods"/> it names, in any
/// order, or <see cref="EndpointMatrix.AnyMethod"/>, and its <paramref name="Route"/> pattern as mapped; the status
/// each <see cref="Caller"/> gets from it; and how to call it.
/// </summary>
/// <remarks>
/// <paramref name="Request"/> builds the request for one call. Left out, the request goes to the route pattern, which
/// must then have no parameters, so a row whose route has them builds a request to a path its endpoint answers. It is
/// asked again for each call, and may first add the record that a call acts on: a call commits, so a row whose calls
/// change something gives each call a record of its own (tests/README.md).
/// </remarks>
internal sealed record EndpointRow(
    IReadOnlyList<string> Methods,
    string Route,
    HttpStatusCode SignedOut,
    HttpStatusCode WorkerOfA,
    HttpStatusCode ManagerOfA,
    HttpStatusCode ManagerOfB,
    Func<EndpointCall, Task<HttpRequestMessage>>? Request = null)
{
    /// <summary>The status that <paramref name="caller"/> gets from the endpoint.</summary>
    public HttpStatusCode StatusFor(Caller caller) => caller switch
    {
        Caller.SignedOut => SignedOut,
        Caller.WorkerOfA => WorkerOfA,
        Caller.ManagerOfA => ManagerOfA,
        Caller.ManagerOfB => ManagerOfB,
        _ => throw new ArgumentOutOfRangeException(nameof(caller), caller, null),
    };
}

/// <summary>A call of an endpoint with one of the methods that <see cref="EndpointMatrix.CallsTo"/> gives it.</summary>
internal sealed record EndpointCall(HttpMethod Method, RouteEndpoint Endpoint)
{
    /// <summary>
    /// The request for this call, as the endpoint's row builds it. An endpoint with no row, such as one that a test
    /// maps for itself, is called at its route pattern.
    /// </summary>
    public Task<HttpRequestMessage> RequestAsync() =>
        (EndpointMatrix.RowOf(Endpoint)?.Request ?? AtRoutePattern)(this);

    // MapStaticAssets' patterns have no leading slash.
    private static Task<HttpRequestMessage> AtRoutePattern(EndpointCall call) =>
        call.Endpoint.RoutePattern is { Parameters: [], RawText: { } pattern }
            ? Task.FromResult(new HttpRequestMessage(call.Method, "/" + pattern.TrimStart('/')))
            : throw new NotSupportedException(
                $"{call.Endpoint.RoutePattern.RawText} has route parameters, so its row builds the request.");
}
