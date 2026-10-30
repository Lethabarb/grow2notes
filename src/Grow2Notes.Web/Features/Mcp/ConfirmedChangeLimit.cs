using Grow2Notes.Web.Features.Mcp.Contracts;

namespace Grow2Notes.Web.Features.Mcp;

/// <summary>
/// The count of the confirmed invites, sign-in resets and deactivations that a user makes through an assistant
/// (mcp-server.md §5.6, A139): <see cref="AssistantRateLimitPermits.ConfirmedChanges"/> in one fixed hour that starts
/// with the user's first counted change. The framework's limiter would report the whole window as the time to wait
/// whatever is left of it, and the model is told the real time, so this is a small counter on the app's clock.
/// </summary>
/// <remarks>
/// The counts are in memory, in the one instance that the app runs (§10.1), so a restart clears them. There is one
/// entry for each user who has made a counted change, which the number of accounts bounds.
/// </remarks>
internal sealed class ConfirmedChangeLimit(AssistantRateLimitPermits permits, TimeProvider time)
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    private readonly Lock _gate = new();

    private readonly Dictionary<Guid, (DateTimeOffset WindowStart, int Used)> _windows = [];

    /// <summary>
    /// Counts a confirmed change of <paramref name="userId"/> and returns null, or, when the hour's permits are used,
    /// returns the refusal with the time left. A refusal uses no permit and does not move the window.
    /// </summary>
    public ToolError? TryCount(Guid userId)
    {
        var now = time.GetUtcNow();
        lock (_gate)
        {
            if (!_windows.TryGetValue(userId, out var window) || now >= window.WindowStart + Window)
            {
                window = (now, 0);
            }

            if (window.Used >= permits.ConfirmedChanges)
            {
                return ToolError.RateLimited(window.WindowStart + Window - now);
            }

            _windows[userId] = (window.WindowStart, window.Used + 1);
            return null;
        }
    }
}
