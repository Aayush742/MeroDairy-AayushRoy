using System.Text.RegularExpressions;
using MeroDiary.Data.Repositories;
using MeroDiary.Domain.Enums;
using MeroDiary.Domain.Models.Analytics;

namespace MeroDiary.Services.Analytics;

public sealed partial class DashboardAnalyticsService : IDashboardAnalyticsService
{
	private readonly IJournalEntryRepository _entries;
	private readonly IAnalyticsRepository _analytics;

	public DashboardAnalyticsService(IJournalEntryRepository entries, IAnalyticsRepository analytics)
	{
		_entries = entries;
		_analytics = analytics;
	}

	public async Task<DashboardAnalyticsReport> GetReportAsync(
		DateOnly? rangeStart = null,
		DateOnly? rangeEnd = null,
		int topTags = 10,
		CancellationToken cancellationToken = default)
	{
		rangeEnd ??= DateOnly.FromDateTime(DateTime.Now);

		if (!rangeStart.HasValue)
		{
			var minMax = await _entries.GetMinMaxEntryDateAsync(cancellationToken).ConfigureAwait(false);
			rangeStart = minMax.MinDate ?? rangeEnd.Value;
		}

		if (rangeStart.Value > rangeEnd.Value)
			(rangeStart, rangeEnd) = (rangeEnd, rangeStart);

		var start = rangeStart.Value;
		var end = rangeEnd.Value;

		var moodCounts = await _analytics.GetPrimaryMoodCategoryCountsAsync(start, end, cancellationToken).ConfigureAwait(false);
		var totalMoodCount = moodCounts.Values.Sum();

		int GetCount(MoodCategory c) => moodCounts.TryGetValue((int)c, out var v) ? v : 0;
		double Pct(int count) => totalMoodCount == 0 ? 0d : (count * 100d) / totalMoodCount;

		var moodDistribution = new[]
		{
			new MoodDistributionPoint { Category = MoodCategory.Positive, Count = GetCount(MoodCategory.Positive), Percentage = Pct(GetCount(MoodCategory.Positive)) },
			new MoodDistributionPoint { Category = MoodCategory.Neutral, Count = GetCount(MoodCategory.Neutral), Percentage = Pct(GetCount(MoodCategory.Neutral)) },
			new MoodDistributionPoint { Category = MoodCategory.Negative, Count = GetCount(MoodCategory.Negative), Percentage = Pct(GetCount(MoodCategory.Negative)) },
		};

		var mostFrequentMood = await _analytics.GetMostFrequentPrimaryMoodAsync(start, end, cancellationToken).ConfigureAwait(false);
		var topTagsList = await _analytics.GetTopTagsAsync(start, end, topTags, cancellationToken).ConfigureAwait(false);
		var categoryBreakdown = await _analytics.GetCategoryBreakdownAsync(start, end, cancellationToken).ConfigureAwait(false);

		var wordTrend = await BuildWordCountTrendDailyAsync(start, end, cancellationToken).ConfigureAwait(false);

		return new DashboardAnalyticsReport
		{
			RangeStart = start,
			RangeEnd = end,
			MoodDistribution = moodDistribution,
			MostFrequentMood = mostFrequentMood,
			TopTags = topTagsList,
			CategoryBreakdown = categoryBreakdown,
			WordCountTrendDaily = wordTrend,
		};
	}

	private async Task<IReadOnlyList<WordCountPoint>> BuildWordCountTrendDailyAsync(
		DateOnly start,
		DateOnly end,
		CancellationToken cancellationToken)
	{
		var rows = await _analytics.GetEntryContentsInRangeAsync(start, end, cancellationToken).ConfigureAwait(false);

		var byDate = rows
			.GroupBy(r => r.Date)
			.ToDictionary(g => g.Key, g => g.Sum(x => CountWords(x.Content)));

		var points = new List<WordCountPoint>();
		for (var d = start; d <= end; d = d.AddDays(1))
		{
			byDate.TryGetValue(d, out var wc);
			points.Add(new WordCountPoint { Date = d, WordCount = wc });
		}

		return points;
	}

	private static int CountWords(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return 0;
		return WordRegex().Matches(text).Count;
	}

	// Unicode letters/numbers plus apostrophes.
	[GeneratedRegex(@"\b[\p{L}\p{N}']+\b", RegexOptions.Compiled)]
	private static partial Regex WordRegex();
}


