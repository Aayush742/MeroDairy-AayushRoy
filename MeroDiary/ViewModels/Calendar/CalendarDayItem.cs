namespace MeroDiary.ViewModels.Calendar;

public sealed class CalendarDayItem
{
	public required DateOnly Date { get; init; }
	public required bool IsInCurrentMonth { get; init; }
	public required bool HasEntry { get; init; }
	public required bool IsToday { get; init; }
}


