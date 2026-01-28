using MeroDiary.Domain.Models.Analytics;

namespace MeroDiary.Data.Repositories;

public interface IAnalyticsRepository
{
	Task<IReadOnlyDictionary<int, int>> GetPrimaryMoodCategoryCountsAsync(DateOnly startInclusive, DateOnly endInclusive, CancellationToken cancellationToken = default);
	Task<MoodFrequency?> GetMostFrequentPrimaryMoodAsync(DateOnly startInclusive, DateOnly endInclusive, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<TagUsage>> GetTopTagsAsync(DateOnly startInclusive, DateOnly endInclusive, int limit, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<CategoryBreakdownItem>> GetCategoryBreakdownAsync(DateOnly startInclusive, DateOnly endInclusive, CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns entry date + raw content for word-count computation (done in service for correctness).
	/// </summary>
	Task<IReadOnlyList<(DateOnly Date, string Content)>> GetEntryContentsInRangeAsync(DateOnly startInclusive, DateOnly endInclusive, CancellationToken cancellationToken = default);
}


