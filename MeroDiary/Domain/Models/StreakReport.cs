namespace MeroDiary.Domain.Models;

public sealed class StreakReport
{
	/// <summary>
	/// Range used for missed-day detection.
	/// </summary>
	public required DateOnly RangeStart { get; init; }
	public required DateOnly RangeEnd { get; init; }

	/// <summary>
	/// Current consecutive daily streak ending at RangeEnd (typically today).
	/// If there is no entry on RangeEnd, CurrentStreak is 0.
	/// </summary>
	public required int CurrentStreak { get; init; }

	/// <summary>
	/// Longest consecutive daily streak found within RangeStart..RangeEnd.
	/// </summary>
	public required int LongestStreak { get; init; }

	/// <summary>
	/// Days in RangeStart..RangeEnd that have no entry.
	/// </summary>
	public required IReadOnlyList<DateOnly> MissedDays { get; init; }
}


