namespace Grow2Notes.Web.Features.Mcp.OAuth;

/// <summary>
/// The keys of the properties that <c>admin mcp-client</c> stores on an approved assistant's registration, in
/// OpenIddict's <c>Properties</c>. The consent screen reads them from the registration, never from the assistant's own
/// metadata (mcp-server.md §4.4).
/// </summary>
internal static class ApprovedAssistantProperties
{
    /// <summary>The company behind the assistant, in the operator's words.</summary>
    public const string Company = "company";

    /// <summary>Where the assistant keeps the data it is given, in the operator's words.</summary>
    public const string DataLocation = "dataLocation";
}
