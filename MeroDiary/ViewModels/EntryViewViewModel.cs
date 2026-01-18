using MeroDiary.Data.Repositories;
using MeroDiary.Domain.Models;
using MeroDiary.Services;
using MeroDiary.Services.Markdown;
using Microsoft.Extensions.Logging;

namespace MeroDiary.ViewModels;

public sealed class EntryViewViewModel : ViewModelBase
{
	private readonly IJournalEntryService _journal;
	private readonly ICategoryRepository _categories;
	private readonly IMoodRepository _moods;
	private readonly ITagRepository _tags;
	private readonly IMarkdownRenderer _markdown;
	private readonly ILogger<EntryViewViewModel> _logger;

	private bool _isBusy;
	private string? _errorMessage;

	private JournalEntry? _entry;
	private string _categoryName = string.Empty;
	private string _primaryMoodName = string.Empty;
	private IReadOnlyList<string> _tagNames = Array.Empty<string>();
	private string _renderedHtml = string.Empty;

	public EntryViewViewModel(
		IJournalEntryService journal,
		ICategoryRepository categories,
		IMoodRepository moods,
		ITagRepository tags,
		IMarkdownRenderer markdown,
		ILogger<EntryViewViewModel> logger)
	{
		_journal = journal;
		_categories = categories;
		_moods = moods;
		_tags = tags;
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

	public JournalEntry? Entry
	{
		get => _entry;
		private set => SetProperty(ref _entry, value);
	}

	public string CategoryName
	{
		get => _categoryName;
		private set => SetProperty(ref _categoryName, value);
	}

	public string PrimaryMoodName
	{
		get => _primaryMoodName;
		private set => SetProperty(ref _primaryMoodName, value);
	}

	public IReadOnlyList<string> TagNames
	{
		get => _tagNames;
		private set => SetProperty(ref _tagNames, value);
	}

	public string RenderedHtml
	{
		get => _renderedHtml;
		private set => SetProperty(ref _renderedHtml, value);
	}

	public async Task LoadAsync(Guid entryId, CancellationToken cancellationToken = default)
	{
		try
		{
			ErrorMessage = null;
			IsBusy = true;

			var entry = await _journal.GetByIdAsync(entryId, cancellationToken).ConfigureAwait(false);
			if (entry is null)
			{
				Entry = null;
				ErrorMessage = "Entry not found.";
				return;
			}

			Entry = entry;

			var category = await _categories.GetByIdAsync(entry.CategoryId, cancellationToken).ConfigureAwait(false);
			CategoryName = category?.Name ?? "Unknown";

			var moodSelection = await _journal.GetMoodSelectionAsync(entryId, cancellationToken).ConfigureAwait(false);
			var moodMap = (await _moods.GetAllAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(m => m.Id, m => m.Name);
			PrimaryMoodName = (moodSelection is not null && moodMap.TryGetValue(moodSelection.PrimaryMoodId, out var mn)) ? mn : "Unknown";

			var tagIds = await _journal.GetTagIdsAsync(entryId, cancellationToken).ConfigureAwait(false);
			var tags = await _tags.GetByIdsAsync(tagIds, cancellationToken).ConfigureAwait(false);
			TagNames = tags.Select(t => t.Name).OrderBy(n => n).ToList();

			RenderedHtml = _markdown.RenderToHtml(entry.Content);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Failed to load entry view.");
			ErrorMessage = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}
}


