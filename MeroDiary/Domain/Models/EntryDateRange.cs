namespace MeroDiary.Domain.Models;

public sealed class EntryDateRange
{
	public required DateOnly? MinDate { get; init; }
	public required DateOnly? MaxDate { get; init; }
}


