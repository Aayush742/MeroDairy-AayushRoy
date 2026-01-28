namespace MeroDiary.Domain.Models.Analytics;

public sealed class TagUsage
{
	public required Guid TagId { get; init; }
	public required string Name { get; init; }
	public required int Count { get; init; }
}


