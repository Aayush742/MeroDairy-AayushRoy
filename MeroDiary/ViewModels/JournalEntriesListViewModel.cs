using MeroDiary.Domain.Models;
using MeroDiary.Services;
using MeroDiary.Data.Repositories;
using MeroDiary.Data.Sqlite;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace MeroDiary.ViewModels;

public sealed class JournalEntriesListViewModel : ViewModelBase
{
	private readonly IJournalEntryService _service;
	private readonly ICategoryRepository _categories;
	private readonly IMoodRepository _moods;
	private readonly ITagRepository _tags;
	private readonly IDatabaseInitializer _db;
	private readonly NavigationManager _nav;
	private readonly ILogger<JournalEntriesListViewModel> _logger;

	private bool _isBusy;
	private string? _errorMessage;
	private readonly List<JournalEntryListItem> _items = new();
	private bool _hasMore = true;
	private string _searchText = string.Empty;
	private DateTime? _startDateLocal;
	private DateTime? _endDateLocal;
	private Guid? _categoryId;
	private IReadOnlyList<Category> _categoryList = Array.Empty<Category>();
	private IReadOnlyList<Mood> _moodList = Array.Empty<Mood>();
	private IReadOnlyList<Tag> _tagList = Array.Empty<Tag>();
	private readonly HashSet<Guid> _selectedMoodIds = new();
	private readonly HashSet<Guid> _selectedTagIds = new();

	public JournalEntriesListViewModel(
		IJournalEntryService service,
		ICategoryRepository categories,
		IMoodRepository moods,
		ITagRepository tags,
		IDatabaseInitializer db,
		NavigationManager nav,
		ILogger<JournalEntriesListViewModel> logger)
	{
		_service = service;
		_categories = categories;
		_moods = moods;
		_tags = tags;
		_db = db;
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

	public IReadOnlyList<JournalEntryListItem> Items => _items;

	public bool HasMore
	{
		get => _hasMore;
		private set => SetProperty(ref _hasMore, value);
	}

	public int PageSize { get; set; } = 20;

	public IReadOnlyList<Category> Categories
	{
		get => _categoryList;
		private set => SetProperty(ref _categoryList, value);
	}

	public IReadOnlyList<Mood> Moods
	{
		get => _moodList;
		private set => SetProperty(ref _moodList, value);
	}

	public IReadOnlyList<Tag> Tags
	{
		get => _tagList;
		private set => SetProperty(ref _tagList, value);
	}

	public string SearchText
	{
		get => _searchText;
		set => SetProperty(ref _searchText, value);
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

	public string BuildExportUrl()
	{
		// Prefill export range from current date filter (or default to last 7 days).
		var start = StartDateLocal ?? DateTime.Today.AddDays(-7);
		var end = EndDateLocal ?? DateTime.Today;
		return $"export?start={Uri.EscapeDataString(start.ToString("yyyy-MM-dd"))}&end={Uri.EscapeDataString(end.ToString("yyyy-MM-dd"))}";
	}

	public Guid? CategoryId
	{
		get => _categoryId;
		set => SetProperty(ref _categoryId, value);
	}

	public bool IsMoodSelected(Guid id) => _selectedMoodIds.Contains(id);
	public bool IsTagSelected(Guid id) => _selectedTagIds.Contains(id);

	public void ToggleMood(Guid id)
	{
		if (id == Guid.Empty) return;
		if (!_selectedMoodIds.Add(id))
			_selectedMoodIds.Remove(id);
		OnPropertyChanged(nameof(SelectedMoodCount));
	}

	public void ToggleTag(Guid id)
	{
		if (id == Guid.Empty) return;
		if (!_selectedTagIds.Add(id))
			_selectedTagIds.Remove(id);
		OnPropertyChanged(nameof(SelectedTagCount));
	}

	public int SelectedMoodCount => _selectedMoodIds.Count;
	public int SelectedTagCount => _selectedTagIds.Count;

	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		await _db.InitializeAsync(cancellationToken).ConfigureAwait(false);
		await LoadFilterDataAsync(cancellationToken).ConfigureAwait(false);
		if (_items.Count == 0)
			await RefreshAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task RefreshAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			ErrorMessage = null;
			IsBusy = true;
			_items.Clear();
			HasMore = true;
			await LoadMoreCoreAsync(force: true, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Failed to refresh journal entry list.");
			ErrorMessage = ex.GetBaseException().Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
	{
		await LoadMoreCoreAsync(force: false, cancellationToken).ConfigureAwait(false);
	}

	private async Task LoadMoreCoreAsync(bool force, CancellationToken cancellationToken)
	{
		if (!HasMore)
			return;

		if (!force && IsBusy)
			return;

		try
		{
			ErrorMessage = null;
			IsBusy = true;

			var query = BuildQuery();
			var page = await _service
				.SearchListPageAsync(query, offset: _items.Count, limit: PageSize, cancellationToken)
				.ConfigureAwait(false);

			_items.AddRange(page);
			OnPropertyChanged(nameof(Items));

			if (page.Count < PageSize)
				HasMore = false;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Failed to load more journal entries.");
			ErrorMessage = ex.GetBaseException().Message;
		}
		finally
		{
			IsBusy = false;
		}
	}


	public void ClearFilters()
	{
		SearchText = string.Empty;
		StartDateLocal = null;
		EndDateLocal = null;
		CategoryId = null;
		_selectedMoodIds.Clear();
		_selectedTagIds.Clear();
		OnPropertyChanged(nameof(SelectedMoodCount));
		OnPropertyChanged(nameof(SelectedTagCount));
	}

	public void OpenEntry(JournalEntryListItem item)
	{
		// Dedicated view page
		_nav.NavigateTo($"entry/{item.Id:D}");
	}

	private JournalEntryQuery BuildQuery()
	{
		DateOnly? start = StartDateLocal.HasValue ? DateOnly.FromDateTime(StartDateLocal.Value) : null;
		DateOnly? end = EndDateLocal.HasValue ? DateOnly.FromDateTime(EndDateLocal.Value) : null;

		return new JournalEntryQuery
		{
			SearchText = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
			StartDateInclusive = start,
			EndDateInclusive = end,
			CategoryId = CategoryId is { } c && c != Guid.Empty ? c : null,
			MoodIds = _selectedMoodIds.ToList(),
			TagIds = _selectedTagIds.ToList(),
		};
	}

	private async Task LoadFilterDataAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _db.InitializeAsync(cancellationToken).ConfigureAwait(false);
			Categories = await _categories.GetAllAsync(cancellationToken).ConfigureAwait(false);
			Moods = await _moods.GetAllAsync(cancellationToken).ConfigureAwait(false);
			Tags = await _tags.GetAllAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Failed to load filter data.");
			ErrorMessage = ex.GetBaseException().Message;
		}
	}
}


