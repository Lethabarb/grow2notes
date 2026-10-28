namespace Grow2Notes.Web.Platform.Audit;

/// <summary>
/// The one MCP tool call that a scope serves (mcp-server.md §9): who the assistant is and which tool it called, which
/// <see cref="AuditWriter"/> adds to every event written during the call, and the IDs that the tool reports it
/// returned, which become the call's <c>mcp.read</c> event. It is scoped, like the writer, and stateless MCP serves one
/// call in a scope. It holds no audit event and has no way to read one, so <see cref="IAuditWriter"/> stays write-only.
/// </summary>
internal sealed class McpCall
{
    /// <summary>
    /// The key of the participants that a look-up returned, which the call's <c>ParticipantId</c> comes from.
    /// </summary>
    public const string ParticipantIds = "participantIds";

    /// <summary>The key of the goals that a look-up returned.</summary>
    public const string GoalIds = "goalIds";

    /// <summary>The key of the user accounts that a look-up returned.</summary>
    public const string UserIds = "userIds";

    /// <summary>The key of the common item groups that a tool returned.</summary>
    public const string GroupIds = "groupIds";

    /// <summary>The key of the common items that a tool returned.</summary>
    public const string CommonItemIds = "commonItemIds";

    private readonly Dictionary<string, IReadOnlyCollection<Guid>> returned = [];

    /// <summary>
    /// The assistant's client ID, from its token; null until <see cref="Begin"/>, as outside an MCP call.
    /// </summary>
    public string? Client { get; private set; }

    /// <summary>The tool that was called.</summary>
    public string? Tool { get; private set; }

    /// <summary>Whether an event has been saved since <see cref="Begin"/>, so that the call has its audit row.</summary>
    public bool EventWritten { get; private set; }

    /// <summary>The IDs reported by <see cref="Returned"/>, by key.</summary>
    internal IReadOnlyDictionary<string, IReadOnlyCollection<Guid>> ReturnedIds => returned;

    /// <summary>
    /// Starts the call of <paramref name="tool"/> by <paramref name="client"/>, forgetting any earlier one.
    /// </summary>
    public void Begin(string client, string tool)
    {
        Client = client;
        Tool = tool;
        EventWritten = false;
        returned.Clear();
    }

    /// <summary>
    /// Reports the IDs of the rows that the tool returned, under <paramref name="key"/> (<see cref="ParticipantIds"/>,
    /// <c>userIds</c>, <c>goalIds</c>, <c>groupIds</c> or <c>commonItemIds</c>). Every look-up reports what it returns,
    /// even when that is nothing, because the audit log answers what was disclosed (mcp-server.md §9).
    /// </summary>
    public void Returned(string key, IReadOnlyCollection<Guid> ids) => returned[key] = ids;

    internal void MarkEventWritten() => EventWritten = true;
}
