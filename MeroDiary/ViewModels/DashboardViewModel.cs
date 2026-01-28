using MeroDiary.Domain.Models.Analytics;
using MeroDiary.Services.Analytics;
using Microsoft.Extensions.Logging;

namespace MeroDiary.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
	private readonly IDashboardAnalyticsService _analytics;
	private readonly ILogger<DashboardViewModel> _logger;

	private bool _isBusy;
	private string? _errorMessage;
	private DateTime? _startDateLocal;
	private DateTime? _endDateLocal;
	private DashboardAnalyticsReport? _report;

	public DashboardViewModel(IDashboardAnalyticsService analytics, ILogger<DashboardViewModel> logger)
	{
		_analytics = analytics;
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

	public DateTime? StartDateLocal
	{
		get => _startDateLocal;
		set => SetProperty(ref _startDateLocal, value?.Date);
	}

	public DateTime? EndDateLocal
	{
		get => _endDateLocal;
		set => SetProperty(ref _endDateLocal, value?.Date);
	}

	public DashboardAnalyticsReport? Report
	{
		get => _report;
		private set => SetProperty(ref _report, value);
	}

	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		if (Report is null)
			await RefreshAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task RefreshAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			ErrorMessage = null;
			IsBusy = true;

			DateOnly? start = StartDateLocal.HasValue ? DateOnly.FromDateTime(StartDateLocal.Value) : null;
			DateOnly? end = EndDateLocal.HasValue ? DateOnly.FromDateTime(EndDateLocal.Value) : null;

			Report = await _analytics.GetReportAsync(start, end, topTags: 10, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Failed to load dashboard analytics.");
			ErrorMessage = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	public void ClearDates()
	{
		StartDateLocal = null;
		EndDateLocal = null;
	}
}


