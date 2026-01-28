using MeroDiary.Domain.Enums;

namespace MeroDiary.Domain.Models;

public sealed class Mood
{
	public required Guid Id { get; init; }
	public required string Name { get; init; }
	public required MoodCategory Category { get; init; }

	public required bool IsPredefined { get; init; }
}


