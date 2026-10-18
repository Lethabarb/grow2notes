using System.Globalization;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// The <c>isodate</c> route constraint: a calendar date that exists, written <c>yyyy-MM-dd</c>, as design.md §6.1
/// writes dates, such as a note's date in its address (§5.5 item 1). Any other value matches no route, so the request
/// gets the unknown path's <c>404</c> (§6.1), which the screen shows as Page not found, rather than binding's
/// <c>400</c>.
/// </summary>
internal sealed class IsoDateRouteConstraint : IRouteConstraint
{
    /// <summary>The constraint's name in a route pattern, as in <c>{noteDate:isodate}</c>.</summary>
    public const string Name = "isodate";

    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection) =>
        values.TryGetValue(routeKey, out var value)
        && value is string text
        && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
