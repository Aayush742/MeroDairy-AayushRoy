namespace MeroDiary.Domain.Models;

public sealed class MoodSelection
{
	public required Guid PrimaryMoodId { get; init; }
	public required IReadOnlyList<Guid> SecondaryMoodIds { get; init; } // 0..2
}


