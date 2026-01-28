using MeroDiary.Domain.Models;

namespace MeroDiary.Services.Streak;

public interface IStreakService
{
	/// <summary>
	/// Calculates streak metrics from persisted entry dates.
	/// Defaults: RangeStart = first entry date, RangeEnd = today.
	/// </summary>
	Task<StreakReport> CalculateAsync(DateOnly? rangeStart = null, DateOnly? rangeEnd = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Pure calculation helper (useful for testing).
	/// </summary>
	StreakReport CalculateFromDates(IEnumerable<DateOnly> entryDates, DateOnly rangeStart, DateOnly rangeEnd);
}


