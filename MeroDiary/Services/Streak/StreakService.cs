using MeroDiary.Data.Repositories;
using MeroDiary.Domain.Models;

namespace MeroDiary.Services.Streak;

public sealed class StreakService : IStreakService
{
	private readonly IJournalEntryRepository _repo;

	public StreakService(IJournalEntryRepository repo)
	{
		_repo = repo;
	}

	public async Task<StreakReport> CalculateAsync(
		DateOnly? rangeStart = null,
		DateOnly? rangeEnd = null,
		CancellationToken cancellationToken = default)
	{
		// Default end is today.
		rangeEnd ??= DateOnly.FromDateTime(DateTime.Now);

		// Default start is earliest entry date (or end if there are no entries).
		if (!rangeStart.HasValue)
		{
			var minMax = await _repo.GetMinMaxEntryDateAsync(cancellationToken).ConfigureAwait(false);
			rangeStart = minMax.MinDate ?? rangeEnd.Value;
		}

		if (rangeStart.Value > rangeEnd.Value)
			(rangeStart, rangeEnd) = (rangeEnd, rangeStart);

		var dates = await _repo.GetEntryDatesInRangeAsync(rangeStart.Value, rangeEnd.Value, cancellationToken).ConfigureAwait(false);
		return CalculateFromDates(dates, rangeStart.Value, rangeEnd.Value);
	}

	public StreakReport CalculateFromDates(IEnumerable<DateOnly> entryDates, DateOnly rangeStart, DateOnly rangeEnd)
	{
		if (rangeStart > rangeEnd)
			(rangeStart, rangeEnd) = (rangeEnd, rangeStart);

		var set = new HashSet<DateOnly>((entryDates ?? Array.Empty<DateOnly>()).Distinct());

		// Missed days (range can be large; caller can choose a tighter range via CalculateAsync params if needed).
		var missed = new List<DateOnly>();
		for (var d = rangeStart; d <= rangeEnd; d = d.AddDays(1))
		{
			if (!set.Contains(d))
				missed.Add(d);
		}

		var longest = CalculateLongestStreak(set, rangeStart, rangeEnd);
		var current = CalculateCurrentStreak(set, rangeEnd);

		return new StreakReport
		{
			RangeStart = rangeStart,
			RangeEnd = rangeEnd,
			CurrentStreak = current,
			LongestStreak = longest,
			MissedDays = missed,
		};
	}

	private static int CalculateCurrentStreak(HashSet<DateOnly> set, DateOnly end)
	{
		// “Current streak” requires an entry on the end date (typically today).
		if (!set.Contains(end))
			return 0;

		var count = 0;
		for (var d = end; set.Contains(d); d = d.AddDays(-1))
			count++;

		return count;
	}

	private static int CalculateLongestStreak(HashSet<DateOnly> set, DateOnly rangeStart, DateOnly rangeEnd)
	{
		var longest = 0;
		var current = 0;

		for (var d = rangeStart; d <= rangeEnd; d = d.AddDays(1))
		{
			if (set.Contains(d))
			{
				current++;
				if (current > longest)
					longest = current;
			}
			else
			{
				current = 0;
			}
		}

		return longest;
	}
}


