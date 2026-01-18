namespace MeroDiary.Domain.Models;

public sealed class JournalEntrySummary
{
	public required Guid Id { get; init; }
	public required DateOnly EntryDate { get; init; }
	public required string Title { get; init; }
	public required Guid CategoryId { get; init; }
}


