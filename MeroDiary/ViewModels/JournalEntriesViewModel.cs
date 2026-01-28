using MeroDiary.Domain.Models;
using MeroDiary.Data.Repositories;
using MeroDiary.Data.Sqlite;
using MeroDiary.Services;
using MeroDiary.Services.Markdown;
using Microsoft.Extensions.Logging;

namespace MeroDiary.ViewModels;

public sealed class JournalEntriesViewModel : ViewModelBase
{
	private readonly IJournalEntryService _service;
	private readonly IDatabaseInitializer _db;
	private readonly ICategoryRepository _categories;
	private readonly IMoodRepository _moodRepository;
	private readonly ITagRepository _tagRepository;
	private readonly IMarkdownRenderer _markdown;
	private readonly ILogger<JournalEntriesViewModel> _logger;

	private bool _isBusy;
	private string? _errorMessage;
	private IReadOnlyList<JournalEntry> _allEntries = Array.Empty<JournalEntry>();
	private IReadOnlyList<JournalEntry> _entries = Array.Empty<JournalEntry>();
	private IReadOnlyList<Category> _categoryList = Array.Empty<Category>();
	private Guid _selectedCategoryId = Guid.Empty;
	private Guid? _filterCategoryId;
	private bool _isViewMode;
	private string _renderedContentHtml = string.Empty;
	private IReadOnlyList<Mood> _moods = Array.Empty<Mood>();
	private Guid _primaryMoodId = Guid.Empty;
	private Guid? _secondaryMood1Id;
	private Guid? _secondaryMood2Id;
	private IReadOnlyList<Tag> _tags = Array.Empty<Tag>();
	private readonly HashSet<Guid> _selectedTagIds = new();
	private string _newTagName = string.Empty;

	private Guid? _editingId;
	private DateTime _entryDateLocal = DateTime.Today;
	private string _title = string.Empty;
	private string _content = string.Empty;
	private bool _isReadOnly;

	public JournalEntriesViewModel(
		IJournalEntryService service,
		IDatabaseInitializer db,
		ICategoryRepository categories,
		IMoodRepository moodRepository,
		ITagRepository tagRepository,
		IMarkdownRenderer markdown,
		ILogger<JournalEntriesViewModel> logger)
	{
		_service = service;
		_db = db;
		_categories = categories;
		_moodRepository = moodRepository;
		_tagRepository = tagRepository;
		_markdown = markdown;
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

	public IReadOnlyList<JournalEntry> Entries
	{
		get => _entries;
		private set => SetProperty(ref _entries, value);
	}

	public IReadOnlyList<Category> Categories
	{
		get => _categoryList;
		private set => SetProperty(ref _categoryList, value);
	}

	// Required selection for create/edit
	public Guid SelectedCategoryId
	{
		get => _selectedCategoryId;
		set => SetProperty(ref _selectedCategoryId, value);
	}

	// Optional filter for list/analytics
	public Guid? FilterCategoryId
	{
		get => _filterCategoryId;
		set
		{
			if (SetProperty(ref _filterCategoryId, value))
				ApplyFilter();
		}
	}

	public IReadOnlyList<Mood> Moods
	{
		get => _moods;
		private set => SetProperty(ref _moods, value);
	}

	public Guid PrimaryMoodId
	{
		get => _primaryMoodId;
		set => SetProperty(ref _primaryMoodId, value);
	}

	public Guid? SecondaryMood1Id
	{
		get => _secondaryMood1Id;
		set => SetProperty(ref _secondaryMood1Id, value);
	}

	public Guid? SecondaryMood2Id
	{
		get => _secondaryMood2Id;
		set => SetProperty(ref _secondaryMood2Id, value);
	}

	public IReadOnlyList<Tag> Tags
	{
		get => _tags;
		private set => SetProperty(ref _tags, value);
	}

	public string NewTagName
	{
		get => _newTagName;
		set => SetProperty(ref _newTagName, value);
	}

	public bool IsEditing => _editingId.HasValue;
	public Guid? EditingId => _editingId;

	public bool IsEntryDateEditable => !IsEditing;
	public bool IsReadOnly
	{
		get => _isReadOnly;
		private set => SetProperty(ref _isReadOnly, value);
	}

	public bool CanSave => !IsBusy && !IsReadOnly && !IsViewMode;

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
		set
		{
			if (SetProperty(ref _content, value))
				UpdateRenderedContent();
		}
	}

	public bool IsViewMode
	{
		get => _isViewMode;
		set
		{
			if (SetProperty(ref _isViewMode, value))
			{
				OnPropertyChanged(nameof(CanSave));
				UpdateRenderedContent();
			}
		}
	}

	// Rendered HTML for View Mode (generated locally; raw HTML in Markdown is disabled).
	public string RenderedContentHtml
	{
		get => _renderedContentHtml;
		private set => SetProperty(ref _renderedContentHtml, value);
	}

	public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

	public bool CanEditOrDelete(JournalEntry entry) => true;

	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			// Clear any stale errors when the page is (re)opened.
			ErrorMessage = null;
			IsBusy = true;
			OnPropertyChanged(nameof(CanSave));

			// Ensure tables/seeds exist before any reads.
			await _db.InitializeAsync(cancellationToken).ConfigureAwait(false);

			await LoadCategoriesAsync(cancellationToken).ConfigureAwait(false);
			if (!string.IsNullOrWhiteSpace(ErrorMessage)) return;

			await LoadMoodsAsync(cancellationToken).ConfigureAwait(false);
			if (!string.IsNullOrWhiteSpace(ErrorMessage)) return;

			await LoadTagsAsync(cancellationToken).ConfigureAwait(false);
			if (!string.IsNullOrWhiteSpace(ErrorMessage)) return;

			await RefreshAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Failed to initialize journal page.");
			ErrorMessage = ex.GetBaseException().Message;
		}
		finally
		{
			IsBusy = false;
			OnPropertyChanged(nameof(CanSave));
		}
	}

	public async Task RefreshAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			ErrorMessage = null;
			IsBusy = true;
			await _db.InitializeAsync(cancellationToken).ConfigureAwait(false);
			_allEntries = await _service.GetAllAsync(categoryId: null, cancellationToken).ConfigureAwait(false);
			ApplyFilter();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Failed to refresh journal entries.");
			ErrorMessage = ex.InnerException?.Message ?? ex.Message;
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
		OnPropertyChanged(nameof(IsEntryDateEditable));
		IsReadOnly = false;
		OnPropertyChanged(nameof(CanSave));

		EntryDateLocal = DateTime.Today;
		SelectedCategoryId = Categories.FirstOrDefault()?.Id ?? Guid.Empty;
		PrimaryMoodId = Moods.FirstOrDefault()?.Id ?? Guid.Empty;
		SecondaryMood1Id = null;
		SecondaryMood2Id = null;
		_selectedTagIds.Clear();
		OnPropertyChanged(nameof(SelectedTagCount));
		Title = string.Empty;
		Content = string.Empty;
		IsViewMode = false;
	}

	public async Task StartEditAsync(JournalEntry entry, CancellationToken cancellationToken = default)
	{
		ErrorMessage = null;
		_editingId = entry.Id;
		OnPropertyChanged(nameof(IsEditing));
		OnPropertyChanged(nameof(EditingId));
		OnPropertyChanged(nameof(IsEntryDateEditable));

		EntryDateLocal = entry.EntryDate.ToDateTime(TimeOnly.MinValue);
		SelectedCategoryId = entry.CategoryId;
		await LoadMoodSelectionAsync(entry.Id, cancellationToken).ConfigureAwait(false);
		await LoadTagSelectionAsync(entry.Id, cancellationToken).ConfigureAwait(false);
		Title = entry.Title;
		Content = entry.Content;
		IsViewMode = false;
		IsReadOnly = false;
		OnPropertyChanged(nameof(CanSave));
	}

	/// <summary>
	/// Open a date from Calendar navigation. If an entry exists, load it and default to View Mode.
	/// If it doesn't exist, start a new entry with the date pre-filled.
	/// </summary>
	public async Task OpenForDateAsync(DateOnly date, CancellationToken cancellationToken = default)
	{
		var existing = await _service.GetByDateAsync(date, cancellationToken).ConfigureAwait(false);
		if (existing is null)
		{
			StartNew();
			EntryDateLocal = existingDateToLocal(date);
			return;
		}

		// Load the existing entry, but default to View Mode.
		_editingId = existing.Id;
		OnPropertyChanged(nameof(IsEditing));
		OnPropertyChanged(nameof(EditingId));
		OnPropertyChanged(nameof(IsEntryDateEditable));

		EntryDateLocal = existingDateToLocal(existing.EntryDate);
		SelectedCategoryId = existing.CategoryId;
		await LoadMoodSelectionAsync(existing.Id, cancellationToken).ConfigureAwait(false);
		await LoadTagSelectionAsync(existing.Id, cancellationToken).ConfigureAwait(false);
		Title = existing.Title;
		Content = existing.Content;

		IsViewMode = true;
		IsReadOnly = false;
		OnPropertyChanged(nameof(CanSave));
	}

	private static DateTime existingDateToLocal(DateOnly date)
		=> date.ToDateTime(TimeOnly.MinValue);

	public async Task SaveAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			ErrorMessage = null;
			IsBusy = true;
			OnPropertyChanged(nameof(CanSave));

			var validationError = ValidateMoodSelection();
			if (!string.IsNullOrWhiteSpace(validationError))
			{
				ErrorMessage = validationError;
				return;
			}

			var moodSelection = BuildMoodSelection();
			var tagIds = _selectedTagIds.ToList();

			if (_editingId is null)
			{
				var date = DateOnly.FromDateTime(EntryDateLocal);
				await _service.CreateAsync(date, SelectedCategoryId, moodSelection, tagIds, Title, Content, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				await _service.UpdateAsync(_editingId.Value, SelectedCategoryId, moodSelection, tagIds, Title, Content, cancellationToken).ConfigureAwait(false);
			}

			StartNew();
			await RefreshAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Failed to save journal entry.");
			ErrorMessage = ex.GetBaseException().Message;
		}
		finally
		{
			IsBusy = false;
			OnPropertyChanged(nameof(CanSave));
		}
	}

	public async Task DeleteAsync(JournalEntry entry, CancellationToken cancellationToken = default)
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
			_logger.LogError(ex, "Failed to delete journal entry.");
			ErrorMessage = ex.GetBaseException().Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	public string GetCategoryName(Guid categoryId)
	{
		return Categories.FirstOrDefault(c => c.Id == categoryId)?.Name ?? "Unknown";
	}

	public bool IsTagSelected(Guid tagId) => _selectedTagIds.Contains(tagId);

	public void ToggleTag(Guid tagId)
	{
		if (tagId == Guid.Empty)
			return;

		if (!_selectedTagIds.Add(tagId))
			_selectedTagIds.Remove(tagId);

		OnPropertyChanged(nameof(SelectedTagCount));
	}

	public int SelectedTagCount => _selectedTagIds.Count;

	public async Task AddNewTagAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			ErrorMessage = null;
			IsBusy = true;

			var tag = await _tagRepository.GetOrCreateAsync(NewTagName, cancellationToken: cancellationToken).ConfigureAwait(false);
			NewTagName = string.Empty;

			await LoadTagsAsync(cancellationToken).ConfigureAwait(false);
			_selectedTagIds.Add(tag.Id);
			OnPropertyChanged(nameof(SelectedTagCount));
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Failed to add tag.");
			ErrorMessage = ex.GetBaseException().Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	private async Task LoadCategoriesAsync(CancellationToken cancellationToken)
	{
		try
		{
			Categories = await _categories.GetAllAsync(cancellationToken).ConfigureAwait(false);
			// Clear any stale category-load errors after a successful load.
			ErrorMessage = null;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Failed to load categories.");
			ErrorMessage = ex.GetBaseException().Message;
		}
	}

	private void ApplyFilter()
	{
		if (_filterCategoryId is null || _filterCategoryId == Guid.Empty)
		{
			Entries = _allEntries;
			return;
		}

		Entries = _allEntries.Where(e => e.CategoryId == _filterCategoryId.Value).ToList();
	}

	private void UpdateRenderedContent()
	{
		RenderedContentHtml = IsViewMode ? _markdown.RenderToHtml(Content) : string.Empty;
	}

	private async Task LoadMoodsAsync(CancellationToken cancellationToken)
	{
		try
		{
			Moods = await _moodRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
			// Clear any stale mood-load errors after a successful load.
			ErrorMessage = null;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Failed to load moods.");
			ErrorMessage = ex.GetBaseException().Message;
		}
	}

	private async Task LoadMoodSelectionAsync(Guid entryId, CancellationToken cancellationToken)
	{
		var selection = await _service.GetMoodSelectionAsync(entryId, cancellationToken).ConfigureAwait(false);
		PrimaryMoodId = selection?.PrimaryMoodId ?? (Moods.FirstOrDefault()?.Id ?? Guid.Empty);
		SecondaryMood1Id = selection?.SecondaryMoodIds.ElementAtOrDefault(0);
		SecondaryMood2Id = selection?.SecondaryMoodIds.ElementAtOrDefault(1);
	}

	private async Task LoadTagsAsync(CancellationToken cancellationToken)
	{
		try
		{
			Tags = await _tagRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
			// Clear any stale tag-load errors after a successful load.
			ErrorMessage = null;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Failed to load tags.");
			ErrorMessage = ex.GetBaseException().Message;
		}
	}

	private async Task LoadTagSelectionAsync(Guid entryId, CancellationToken cancellationToken)
	{
		var ids = await _service.GetTagIdsAsync(entryId, cancellationToken).ConfigureAwait(false);
		_selectedTagIds.Clear();
		foreach (var id in ids)
			_selectedTagIds.Add(id);
		OnPropertyChanged(nameof(SelectedTagCount));
	}

	private string? ValidateMoodSelection()
	{
		if (PrimaryMoodId == Guid.Empty)
			return "Primary mood is required.";

		var secondary = new List<Guid>();
		if (SecondaryMood1Id.HasValue && SecondaryMood1Id.Value != Guid.Empty)
			secondary.Add(SecondaryMood1Id.Value);
		if (SecondaryMood2Id.HasValue && SecondaryMood2Id.Value != Guid.Empty)
			secondary.Add(SecondaryMood2Id.Value);

		if (secondary.Count > 2)
			return "Up to two secondary moods are allowed.";

		if (secondary.Contains(PrimaryMoodId))
			return "Primary mood can’t also be selected as secondary.";

		if (secondary.Distinct().Count() != secondary.Count)
			return "Secondary moods must be different.";

		return null;
	}

	private MoodSelection BuildMoodSelection()
	{
		var secondary = new List<Guid>();
		if (SecondaryMood1Id.HasValue && SecondaryMood1Id.Value != Guid.Empty)
			secondary.Add(SecondaryMood1Id.Value);
		if (SecondaryMood2Id.HasValue && SecondaryMood2Id.Value != Guid.Empty)
			secondary.Add(SecondaryMood2Id.Value);

		return new MoodSelection
		{
			PrimaryMoodId = PrimaryMoodId,
			SecondaryMoodIds = secondary.Distinct().Take(2).ToList(),
		};
	}
}


