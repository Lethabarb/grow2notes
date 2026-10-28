using Grow2Notes.Web.Platform;

namespace Grow2Notes.Web.Features.Mcp.Tools;

/// <summary>The actor of a tool call, which every tool passes to the handler that the app's endpoint calls.</summary>
internal static class ToolActor
{
    /// <summary>
    /// The call's signed-in principal as <see cref="Actor.InMcp"/>. A call reaches a tool only through the
    /// authorisation of <c>/mcp</c>, so a missing principal is a bug.
    /// </summary>
    public static Actor Of(IHttpContextAccessor accessor) => Actor.InMcp(
        accessor.HttpContext?.User ?? throw new InvalidOperationException("The call has no signed-in principal."));
}
