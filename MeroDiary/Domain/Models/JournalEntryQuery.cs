namespace MeroDiary.Domain.Models;

public sealed class JournalEntryQuery
{
	/// <summary>
	/// Searches in Title or Content using SQLite LIKE (case-insensitive by default in SQLite for ASCII).
	/// </summary>
	public string? SearchText { get; init; }

	public DateOnly? StartDateInclusive { get; init; }
	public DateOnly? EndDateInclusive { get; init; }

	public Guid? CategoryId { get; init; }

	/// <summary>
	/// Matches entries that contain ALL of these mood ids (primary or secondary).
	/// </summary>
	public IReadOnlyList<Guid> MoodIds { get; init; } = Array.Empty<Guid>();

	/// <summary>
	/// Matches entries that contain ALL of these tag ids.
	/// </summary>
	public IReadOnlyList<Guid> TagIds { get; init; } = Array.Empty<Guid>();
}


