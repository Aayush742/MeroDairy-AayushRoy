namespace MeroDiary.Domain.Models;

public sealed class Tag
{
	public required Guid Id { get; init; }
	public required string Name { get; init; }

	public required bool IsPredefined { get; init; }

	public required DateTimeOffset CreatedAt { get; init; } // system-generated (UTC)
	public required DateTimeOffset UpdatedAt { get; init; } // system-generated (UTC)
}


