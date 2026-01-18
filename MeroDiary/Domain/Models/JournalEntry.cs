namespace MeroDiary.Domain.Models;

public sealed class JournalEntry
{
	public required Guid Id { get; init; }
	public required DateOnly EntryDate { get; init; }
	public required Guid CategoryId { get; init; }
	public required string Title { get; init; }
	public required string Content { get; init; } // Markdown

	public required DateTimeOffset CreatedAt { get; init; } // system-generated (UTC)
	public required DateTimeOffset UpdatedAt { get; init; } // system-generated (UTC)
}


