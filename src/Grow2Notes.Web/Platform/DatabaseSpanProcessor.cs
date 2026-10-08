using System.Diagnostics;
using OpenTelemetry;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// Removes the status description from database spans before any exporter reads them, leaving their
/// <see cref="ActivityStatusCode.Error"/> status (design.md §9.5).
/// </summary>
/// <remarks>
/// The distro's SqlClient instrumentation sets a failed command's description to the exception's message, which can
/// hold the values that failed. It is the only failure text the instrumentation records: in
/// Azure.Monitor.OpenTelemetry.AspNetCore 1.6.0 it adds no exception events, and command text only for stored
/// procedures, whose text is their name. The Azure Monitor exporter does not send descriptions today; removing them
/// here means no exporter can.
/// </remarks>
internal sealed class DatabaseSpanProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity data)
    {
        // The instrumentation tags every span it starts with db.system.
        if (data.StatusDescription is not null && data.GetTagItem("db.system") is not null)
        {
            // A status set without a description has none.
            data.SetStatus(data.Status);
        }
    }
}
