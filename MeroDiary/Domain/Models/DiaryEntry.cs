namespace MeroDiary.Domain.Models;

public sealed class DiaryEntry
{
	public required Guid Id { get; init; }
	public required DateTimeOffset EntryDate { get; init; }
	public required string Title { get; init; }
	public required string Content { get; init; }

	public required DateTimeOffset CreatedAtUtc { get; init; }
	public required DateTimeOffset UpdatedAtUtc { get; init; }
}


