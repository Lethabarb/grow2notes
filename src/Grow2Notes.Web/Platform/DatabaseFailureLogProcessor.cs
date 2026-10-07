using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// Strips a database failure from a log entry before any exporter reads it, leaving the exception's type and SQL error
/// number under the names <see cref="DatabaseFailureHandler"/> logs them with (design.md §9.5).
/// </summary>
/// <remarks>
/// <para>
/// The app logs database failures without the exception, but framework components log their own with it attached: the
/// health check service when the database check throws, Data Protection when it cannot read the key ring, and the
/// exception handler middleware and Kestrel when a response had already started. The Azure Monitor exporter would send
/// the exception, and its inner exceptions, as exception telemetry with their messages.
/// </para>
/// <para>
/// The message can also be in the entry's text, because components put it in their own messages and values, so the
/// formatted message and every value that is text go too. The template stays, because it has placeholders rather than
/// values, and so do numbers, enums, GUIDs and times. The entry becomes an ordinary log entry at its own level.
/// </para>
/// </remarks>
internal sealed class DatabaseFailureLogProcessor : BaseProcessor<LogRecord>
{
    private const string TemplateKey = "{OriginalFormat}";

    public override void OnEnd(LogRecord data)
    {
        if (data.Exception is null || DatabaseFailure.Find(data.Exception) is not { } failure)
        {
            return;
        }

        string? template = null;
        List<KeyValuePair<string, object?>> attributes = [];
        foreach (var attribute in data.Attributes ?? [])
        {
            if (attribute.Key == TemplateKey)
            {
                template = attribute.Value as string;
                attributes.Add(attribute);
            }
            else if (CannotHoldText(attribute.Value))
            {
                attributes.Add(attribute);
            }
        }

        attributes.Add(new(nameof(DatabaseFailure.ExceptionType), failure.ExceptionType));
        attributes.Add(new(nameof(DatabaseFailure.SqlErrorNumber), failure.SqlErrorNumber));

        data.Exception = null;
        data.FormattedMessage = null;
        // The body is the template, or the formatted message when the entry has none.
        data.Body = template;
        data.Attributes = attributes;
    }

    private static bool CannotHoldText(object? value) =>
        value is null or Enum or Guid or DateTime or DateTimeOffset or TimeSpan or decimal
        || value.GetType().IsPrimitive;
}
