namespace MeroDiary.Domain.Models;

public sealed class JournalEntryListItem
{
	public required Guid Id { get; init; }
	public required DateOnly EntryDate { get; init; }
	public required string Title { get; init; }

	public required string CategoryName { get; init; }
	public required string PrimaryMoodName { get; init; }
	public required IReadOnlyList<string> Tags { get; init; }
}


