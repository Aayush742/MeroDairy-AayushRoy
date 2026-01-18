using MeroDiary.Domain.Enums;

namespace MeroDiary.Domain.Models.Analytics;

public sealed class MoodFrequency
{
	public required Guid MoodId { get; init; }
	public required string Name { get; init; }
	public required MoodCategory Category { get; init; }
	public required int Count { get; init; }
}


