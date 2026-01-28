using MeroDiary.Domain.Models;
using MeroDiary.Services;
using Microsoft.Extensions.Logging;

namespace MeroDiary.ViewModels;

public sealed class DiaryEntriesViewModel : ViewModelBase
{
	private readonly IDiaryEntryService _service;
	private readonly ILogger<DiaryEntriesViewModel> _logger;

	private bool _isBusy;
	private string? _errorMessage;
	private IReadOnlyList<DiaryEntry> _entries = Array.Empty<DiaryEntry>();

	private Guid? _editingId;
	private DateTime _entryDateLocal = DateTime.Today;
	private string _title = string.Empty;
	private string _content = string.Empty;

	public DiaryEntriesViewModel(IDiaryEntryService service, ILogger<DiaryEntriesViewModel> logger)
	{
		_service = service;
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

	public IReadOnlyList<DiaryEntry> Entries
	{
		get => _entries;
		private set => SetProperty(ref _entries, value);
	}

	public bool IsEditing => _editingId.HasValue;
	public Guid? EditingId => _editingId;

	public DateTime EntryDateLocal
	{
		get => _entryDateLocal;
		set => SetProperty(ref _entryDateLocal, value.Date);
	}

	public string Title
	{
		get => _title;
		set => SetProperty(ref _title, value);
	}

	public string Content
	{
		get => _content;
		set => SetProperty(ref _content, value);
	}

	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		// Safe to call multiple times.
		await RefreshAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task RefreshAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			ErrorMessage = null;
			IsBusy = true;

			Entries = await _service.GetAllAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Failed to refresh entries.");
			ErrorMessage = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	public void StartNew()
	{
		ErrorMessage = null;
		_editingId = null;
		OnPropertyChanged(nameof(IsEditing));
		OnPropertyChanged(nameof(EditingId));

		EntryDateLocal = DateTime.Today;
		Title = string.Empty;
		Content = string.Empty;
	}

	public void StartEdit(DiaryEntry entry)
	{
		ErrorMessage = null;
		_editingId = entry.Id;
		OnPropertyChanged(nameof(IsEditing));
		OnPropertyChanged(nameof(EditingId));

		EntryDateLocal = entry.EntryDate.ToLocalTime().DateTime.Date;
		Title = entry.Title;
		Content = entry.Content;
	}

	public async Task SaveAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			ErrorMessage = null;
			IsBusy = true;

			var entryDate = new DateTimeOffset(EntryDateLocal, TimeZoneInfo.Local.GetUtcOffset(EntryDateLocal));

			if (_editingId is null)
			{
				await _service.CreateAsync(Title, Content, entryDate, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				await _service.UpdateAsync(_editingId.Value, Title, Content, entryDate, cancellationToken).ConfigureAwait(false);
			}

			StartNew();
			await RefreshAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Failed to save entry.");
			ErrorMessage = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	public async Task DeleteAsync(DiaryEntry entry, CancellationToken cancellationToken = default)
	{
		try
		{
			ErrorMessage = null;
			IsBusy = true;

			await _service.DeleteAsync(entry.Id, cancellationToken).ConfigureAwait(false);

			if (_editingId == entry.Id)
				StartNew();

			await RefreshAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Failed to delete entry.");
			ErrorMessage = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}
}


