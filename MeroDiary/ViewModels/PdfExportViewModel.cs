using MeroDiary.Services.Export;
using Microsoft.Maui.ApplicationModel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace MeroDiary.ViewModels;

public sealed class PdfExportViewModel : ViewModelBase
{
	private readonly IJournalPdfExportService _export;
	private readonly ILogger<PdfExportViewModel> _logger;

	private bool _isBusy;
	private string? _message;
	private string? _errorMessage;
	private DateTime _startDateLocal = DateTime.Today.AddDays(-7);
	private DateTime _endDateLocal = DateTime.Today;
	private string? _lastFilePath;
	private int _lastEntryCount;

	public PdfExportViewModel(IJournalPdfExportService export, ILogger<PdfExportViewModel> logger)
	{
		_export = export;
		_logger = logger;
	}

	public bool IsBusy
	{
		get => _isBusy;
		private set => SetProperty(ref _isBusy, value);
	}

	public string? Message
	{
		get => _message;
		private set => SetProperty(ref _message, value);
	}

	public string? ErrorMessage
	{
		get => _errorMessage;
		private set => SetProperty(ref _errorMessage, value);
	}

	public DateTime StartDateLocal
	{
		get => _startDateLocal;
		set => SetProperty(ref _startDateLocal, value.Date);
	}

	public DateTime EndDateLocal
	{
		get => _endDateLocal;
		set => SetProperty(ref _endDateLocal, value.Date);
	}

	public string? LastFilePath
	{
		get => _lastFilePath;
		private set => SetProperty(ref _lastFilePath, value);
	}

	public int LastEntryCount
	{
		get => _lastEntryCount;
		private set => SetProperty(ref _lastEntryCount, value);
	}

	public void ApplyPrefill(DateTime? start, DateTime? end)
	{
		if (start.HasValue) StartDateLocal = start.Value.Date;
		if (end.HasValue) EndDateLocal = end.Value.Date;
	}

	public async Task ExportAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			ErrorMessage = null;
			Message = null;
			IsBusy = true;

			var start = DateOnly.FromDateTime(StartDateLocal);
			var end = DateOnly.FromDateTime(EndDateLocal);

			var result = await _export.ExportAsync(start, end, cancellationToken).ConfigureAwait(false);
			LastEntryCount = result.EntryCount;
			LastFilePath = result.FilePath;

			if (!result.Success)
			{
				Message = result.Message ?? "Nothing exported.";
				return;
			}

			Message = $"Exported {result.EntryCount} entries to: {result.FilePath}";
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "PDF export failed.");
			ErrorMessage = ex.GetBaseException().Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	public async Task ViewLastExportAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			ErrorMessage = null;
			Message = null;

			var path = LastFilePath;
			if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
			{
				Message = "No exported PDF found to view yet. Please export first.";
				return;
			}

			await Launcher.Default.OpenAsync(new OpenFileRequest
			{
				File = new ReadOnlyFile(path),
			}).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Failed to open exported PDF.");
			ErrorMessage = ex.GetBaseException().Message;
		}
	}
}


