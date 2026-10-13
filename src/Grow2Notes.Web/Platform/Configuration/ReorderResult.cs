using System.Text.Json.Nodes;

namespace Grow2Notes.Web.Platform.Configuration;

/// <summary>
/// What <see cref="ListOrder.Reorder"/> did with a list: put its active rows in the requested order, or refused it and
/// changed no row; and the list's old and new order, <c>{"order": […]}</c> with the active rows' IDs, which the handler
/// gives <see cref="ConfigurationChanges.SaveAsync"/> as the values before and after for its <c>*.reordered</c> event.
/// </summary>
/// <param name="Error">
/// <see cref="ErrorCode.OrderMismatch"/> when the request was refused, or <see langword="null"/> when it was taken.
/// </param>
/// <param name="Before">The order of the rows as the list's query gave them.</param>
/// <param name="After">
/// The requested order, or, after a refusal, which changed nothing, the order of <paramref name="Before"/>.
/// </param>
internal sealed record ReorderResult(ErrorCode? Error, JsonObject Before, JsonObject After);
