using Grow2Notes.Tests.Fixtures;
using ModelContextProtocol.Client;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// One app as production has it, with its <see cref="ToolScene"/> and a connected client for each
/// <see cref="ToolCaller"/>, for the cases of a test class that only read from the app or add what is unique to them
/// (a family name with the scene's token in it). Starting an app and its database costs seconds, so the cases that
/// change a person or the registration, or move the clock, still start an app of their own. The class's tests run one
/// after the other, so each audit row is read after its own call.
/// </summary>
public sealed class ToolWorld(SqlServerFixture sqlServer) : IAsyncLifetime
{
    /// <summary>The protocol revision that <see cref="ClientOf"/> speaks.</summary>
    public const string Revision = "2025-11-25";

    private readonly Dictionary<ToolCaller, McpClient> clients = [];
    private Grow2NotesFactory? factory;

    internal AssistantFlow Flow { get; private set; } = null!;

    internal ToolScene Scene { get; private set; } = null!;

    internal McpClient ClientOf(ToolCaller caller) => clients[caller];

    /// <summary>
    /// The access token of an assistant that <paramref name="caller"/> has just connected, for a raw call. Each call
    /// of this connects afresh.
    /// </summary>
    internal Task<string> TokenOf(ToolCaller caller) => Scene.ConnectAsync(caller);

    public async ValueTask InitializeAsync()
    {
        factory = new(sqlServer);
        Flow = await AssistantFlow.StartAsync(factory, sqlServer, "ToolWorld", probes: false);
        Scene = await ToolScene.CreateAsync(Flow);
        foreach (var caller in Enum.GetValues<ToolCaller>())
        {
            clients[caller] = await Flow.McpClientAsync(await Scene.ConnectAsync(caller), Revision);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in clients.Values)
        {
            await client.DisposeAsync();
        }

        if (Flow is not null)
        {
            await Flow.DisposeAsync();
        }

        factory?.Dispose();
    }
}
