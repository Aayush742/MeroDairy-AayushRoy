using MeroDiary.Domain.Enums;

namespace MeroDiary.Domain.Models.Analytics;

public sealed class MoodDistributionPoint
{
	public required MoodCategory Category { get; init; }
	public required int Count { get; init; }
	public required double Percentage { get; init; }
}


