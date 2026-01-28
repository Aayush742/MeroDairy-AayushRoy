namespace MeroDiary.Domain.Models.Analytics;

public sealed class CategoryBreakdownItem
{
	public required Guid CategoryId { get; init; }
	public required string Name { get; init; }
	public required int Count { get; init; }
}


