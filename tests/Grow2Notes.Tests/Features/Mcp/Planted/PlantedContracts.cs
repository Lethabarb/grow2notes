using Grow2Notes.Web.Data;

namespace Grow2Notes.Tests.Features.Mcp.Planted;

// Output types that the contract rules must refuse (ContractRulesTests), each for one reason. They are walked with this
// namespace allowed in place of the contracts', so that each fails for the reason it is here for.

/// <summary>A date of birth: on the deny-list, and a type that no leaf may have.</summary>
internal sealed record PlantedDateOfBirth(DateOnly DateOfBirth);

/// <summary>A narrative.</summary>
internal sealed record PlantedNarrative(string Narrative);

/// <summary>A property of a type outside the contracts.</summary>
internal sealed record PlantedParticipantReference(Participant Participant);

/// <summary>A list whose items hold a comment.</summary>
internal sealed record PlantedNestedComment(IReadOnlyList<PlantedCommentItem> Items);

internal sealed record PlantedCommentItem(string Comment);

/// <summary>A count that is named for flags.</summary>
internal sealed record PlantedFlagCount(int ToFlagCount);

/// <summary>A date that is named for notes, as a string.</summary>
internal sealed record PlantedFirstNoteDate(string FirstNoteDate);
