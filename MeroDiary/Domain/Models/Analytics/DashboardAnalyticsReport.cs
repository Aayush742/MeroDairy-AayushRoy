namespace MeroDiary.Domain.Models.Analytics;

public sealed class DashboardAnalyticsReport
{
	public required DateOnly RangeStart { get; init; }
	public required DateOnly RangeEnd { get; init; }

	public required IReadOnlyList<MoodDistributionPoint> MoodDistribution { get; init; }
	public required MoodFrequency? MostFrequentMood { get; init; }
	public required IReadOnlyList<TagUsage> TopTags { get; init; }
	public required IReadOnlyList<CategoryBreakdownItem> CategoryBreakdown { get; init; }
	public required IReadOnlyList<WordCountPoint> WordCountTrendDaily { get; init; }
}


