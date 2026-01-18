namespace MeroDiary.Domain.Models.Analytics;

public sealed class WordCountPoint
{
	public required DateOnly Date { get; init; }
	public required int WordCount { get; init; }
}


