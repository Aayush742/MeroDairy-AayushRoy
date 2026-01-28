using MeroDiary.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace MeroDiary.ViewModels.Calendar;

public sealed class CalendarViewModel : ViewModelBase
{
	private readonly IJournalEntryService _journal;
	private readonly NavigationManager _nav;
	private readonly ILogger<CalendarViewModel> _logger;

	private bool _isBusy;
	private string? _errorMessage;
	private DateOnly _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
	private IReadOnlyList<CalendarDayItem> _days = Array.Empty<CalendarDayItem>();

	public CalendarViewModel(IJournalEntryService journal, NavigationManager nav, ILogger<CalendarViewModel> logger)
	{
		_journal = journal;
		_nav = nav;
		_logger = logger;
	}

	public bool IsBusy
	{
		get => _isBusy;
		private set => SetProperty(ref _isBusy, value);
	}

	public string? ErrorMessage
	{
		get => _errorMessage;
		private set => SetProperty(ref _errorMessage, value);
	}

	public DateOnly Month
	{
		get => _month;
		private set => SetProperty(ref _month, value);
	}

	public string MonthTitle => Month.ToString("MMMM yyyy");

	/// <summary>
	/// Always 42 items (6 weeks) starting from Monday.
	/// </summary>
	public IReadOnlyList<CalendarDayItem> Days
	{
		get => _days;
		private set => SetProperty(ref _days, value);
	}

	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		await LoadMonthAsync(Month, cancellationToken).ConfigureAwait(false);
	}

	public async Task GoToPreviousMonthAsync(CancellationToken cancellationToken = default)
	{
		var prev = Month.AddMonths(-1);
		await LoadMonthAsync(prev, cancellationToken).ConfigureAwait(false);
	}

	public async Task GoToNextMonthAsync(CancellationToken cancellationToken = default)
	{
		var next = Month.AddMonths(1);
		await LoadMonthAsync(next, cancellationToken).ConfigureAwait(false);
	}

	public async Task LoadMonthAsync(DateOnly month, CancellationToken cancellationToken = default)
	{
		try
		{
			ErrorMessage = null;
			IsBusy = true;

			Month = new DateOnly(month.Year, month.Month, 1);
			OnPropertyChanged(nameof(MonthTitle));

			var firstOfMonth = Month;
			var lastOfMonth = Month.AddMonths(1).AddDays(-1);

			var entryDates = await _journal
				.GetEntryDatesInRangeAsync(firstOfMonth, lastOfMonth, cancellationToken)
				.ConfigureAwait(false);

			var entrySet = new HashSet<DateOnly>(entryDates);

			// Build a 6-week grid starting Monday.
			var start = StartOfWeek(firstOfMonth, DayOfWeek.Monday);
			var today = DateOnly.FromDateTime(DateTime.Now);

			var items = new List<CalendarDayItem>(42);
			for (var i = 0; i < 42; i++)
			{
				var d = start.AddDays(i);
				items.Add(new CalendarDayItem
				{
					Date = d,
					IsInCurrentMonth = d.Month == Month.Month && d.Year == Month.Year,
					HasEntry = entrySet.Contains(d),
					IsToday = d == today,
				});
			}

			Days = items;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Failed to load calendar month.");
			ErrorMessage = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	public void SelectDate(CalendarDayItem day)
	{
		// Navigation: Entries page reads query string and opens either existing in view mode or create with date prefilled.
		var date = day.Date.ToString("yyyy-MM-dd");
		_nav.NavigateTo($"entries?date={Uri.EscapeDataString(date)}");
	}

	private static DateOnly StartOfWeek(DateOnly date, DayOfWeek firstDayOfWeek)
	{
		var diff = (7 + (date.DayOfWeek - firstDayOfWeek)) % 7;
		return date.AddDays(-diff);
	}
}


