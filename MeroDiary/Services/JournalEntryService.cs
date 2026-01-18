using MeroDiary.Data.Repositories;
using MeroDiary.Domain.Exceptions;
using MeroDiary.Domain.Models;

namespace MeroDiary.Services;

public sealed class JournalEntryService : IJournalEntryService
{
	private readonly IJournalEntryRepository _repository;
	private readonly ICategoryRepository _categories;
	private readonly IMoodRepository _moods;
	private readonly IJournalEntryMoodRepository _entryMoods;
	private readonly ITagRepository _tags;
	private readonly IJournalEntryTagRepository _entryTags;

	public JournalEntryService(
		IJournalEntryRepository repository,
		ICategoryRepository categories,
		IMoodRepository moods,
		IJournalEntryMoodRepository entryMoods,
		ITagRepository tags,
		IJournalEntryTagRepository entryTags)
	{
		_repository = repository;
		_categories = categories;
		_moods = moods;
		_entryMoods = entryMoods;
		_tags = tags;
		_entryTags = entryTags;
	}

	public Task<IReadOnlyList<JournalEntry>> GetAllAsync(Guid? categoryId = null, CancellationToken cancellationToken = default)
	{
		return categoryId.HasValue
			? _repository.GetAllByCategoryAsync(categoryId.Value, cancellationToken)
			: _repository.GetAllAsync(cancellationToken);
	}

	public Task<JournalEntry?> GetByDateAsync(DateOnly entryDate, CancellationToken cancellationToken = default)
	{
		return _repository.GetByDateAsync(entryDate, cancellationToken);
	}

	public Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		return _repository.GetByIdAsync(id, cancellationToken);
	}

	public Task<IReadOnlyList<DateOnly>> GetEntryDatesInRangeAsync(
		DateOnly startInclusive,
		DateOnly endInclusive,
		CancellationToken cancellationToken = default)
	{
		return _repository.GetEntryDatesInRangeAsync(startInclusive, endInclusive, cancellationToken);
	}

	public Task<MoodSelection?> GetMoodSelectionAsync(Guid journalEntryId, CancellationToken cancellationToken = default)
	{
		return _entryMoods.GetSelectionAsync(journalEntryId, cancellationToken);
	}

	public Task<IReadOnlyList<Guid>> GetTagIdsAsync(Guid journalEntryId, CancellationToken cancellationToken = default)
	{
		return _entryTags.GetTagIdsAsync(journalEntryId, cancellationToken);
	}

	public async Task<IReadOnlyList<JournalEntryListItem>> GetListPageAsync(int offset, int limit, CancellationToken cancellationToken = default)
	{
		var summaries = await _repository.GetSummariesPageAsync(offset, limit, cancellationToken).ConfigureAwait(false);
		return await HydrateListItemsAsync(summaries, cancellationToken).ConfigureAwait(false);
	}

	public async Task<IReadOnlyList<JournalEntryListItem>> SearchListPageAsync(
		JournalEntryQuery query,
		int offset,
		int limit,
		CancellationToken cancellationToken = default)
	{
		var summaries = await _repository.SearchSummariesPageAsync(query, offset, limit, cancellationToken).ConfigureAwait(false);
		return await HydrateListItemsAsync(summaries, cancellationToken).ConfigureAwait(false);
	}

	private async Task<IReadOnlyList<JournalEntryListItem>> HydrateListItemsAsync(
		IReadOnlyList<JournalEntrySummary> summaries,
		CancellationToken cancellationToken)
	{
		if (summaries.Count == 0)
			return Array.Empty<JournalEntryListItem>();

		var entryIds = summaries.Select(s => s.Id).ToList();
		var categoryIds = summaries.Select(s => s.CategoryId).Distinct().ToList();

		var categories = await _categories.GetByIdsAsync(categoryIds, cancellationToken).ConfigureAwait(false);
		var categoryMap = categories.ToDictionary(c => c.Id, c => c.Name);

		var moodMapAll = (await _moods.GetAllAsync(cancellationToken).ConfigureAwait(false))
			.ToDictionary(m => m.Id, m => m.Name);

		var primaryMoodIdsByEntry = await _entryMoods.GetPrimaryMoodIdsByEntryAsync(entryIds, cancellationToken).ConfigureAwait(false);

		var tagIdsByEntry = await _entryTags.GetTagIdsByEntryAsync(entryIds, cancellationToken).ConfigureAwait(false);
		var allTagIds = tagIdsByEntry.Values.SelectMany(x => x).Distinct().ToList();
		var tags = await _tags.GetByIdsAsync(allTagIds, cancellationToken).ConfigureAwait(false);
		var tagMap = tags.ToDictionary(t => t.Id, t => t.Name);

		return summaries.Select(s =>
			{
				var categoryName = categoryMap.TryGetValue(s.CategoryId, out var cn) ? cn : "Unknown";
				var primaryMoodName = "Unknown";
				if (primaryMoodIdsByEntry.TryGetValue(s.Id, out var moodId) && moodMapAll.TryGetValue(moodId, out var mn))
					primaryMoodName = mn;

				var tagNames = tagIdsByEntry.TryGetValue(s.Id, out var tIds)
					? (IReadOnlyList<string>)tIds.Select(id => tagMap.TryGetValue(id, out var n) ? n : "Unknown").ToList()
					: Array.Empty<string>();

				return new JournalEntryListItem
				{
					Id = s.Id,
					EntryDate = s.EntryDate,
					Title = s.Title,
					CategoryName = categoryName,
					PrimaryMoodName = primaryMoodName,
					Tags = tagNames,
				};
			})
			.ToList();
	}

	public async Task<JournalEntry> CreateAsync(
		DateOnly entryDate,
		Guid categoryId,
		MoodSelection moodSelection,
		IReadOnlyList<Guid> tagIds,
		string title,
		string content,
		CancellationToken cancellationToken = default)
	{
		try
		{
			EnsureCategorySelected(categoryId);
			await EnsureCategoryExistsAsync(categoryId, cancellationToken).ConfigureAwait(false);
			await EnsureMoodSelectionValidAsync(moodSelection, cancellationToken).ConfigureAwait(false);
			await EnsureTagsValidAsync(tagIds, cancellationToken).ConfigureAwait(false);

			var normalizedTitle = NormalizeTitle(title);
			var normalizedContent = NormalizeContent(content);

			// Enforce: one journal entry per day.
			var existing = await _repository.GetByDateAsync(entryDate, cancellationToken).ConfigureAwait(false);
			if (existing is not null)
				throw new InvalidOperationException($"An entry for {entryDate:yyyy-MM-dd} already exists.");

			var now = DateTimeOffset.UtcNow;
			var entry = new JournalEntry
			{
				Id = Guid.NewGuid(),
				EntryDate = entryDate,
				CategoryId = categoryId,
				Title = normalizedTitle,
				Content = normalizedContent,
				CreatedAt = now,
				UpdatedAt = now,
			};

			await _repository.AddAsync(entry, cancellationToken).ConfigureAwait(false);
			await _entryMoods.ReplaceSelectionAsync(entry.Id, moodSelection, cancellationToken).ConfigureAwait(false);
			await _entryTags.ReplaceTagsAsync(entry.Id, tagIds, cancellationToken).ConfigureAwait(false);
			return entry;
		}
		catch (Exception ex) when (ex is DataAccessException)
		{
			throw new ServiceException("Failed to create journal entry.", ex);
		}
		catch (Exception ex) when (ex is not OperationCanceledException
		                           && ex is not ArgumentException
		                           && ex is not InvalidOperationException)
		{
			// Unexpected failures get wrapped; validation/business-rule failures bubble up with their message.
			throw new ServiceException("Failed to create journal entry.", ex);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// Business-rule failures (like duplicate entry per day) bubble up.
			throw;
		}
	}

	public async Task<JournalEntry> UpdateAsync(
		Guid id,
		Guid categoryId,
		MoodSelection moodSelection,
		IReadOnlyList<Guid> tagIds,
		string title,
		string content,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var existing = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
			if (existing is null)
				throw new InvalidOperationException("Journal entry does not exist.");

			EnsureCategorySelected(categoryId);
			await EnsureCategoryExistsAsync(categoryId, cancellationToken).ConfigureAwait(false);
			await EnsureMoodSelectionValidAsync(moodSelection, cancellationToken).ConfigureAwait(false);
			await EnsureTagsValidAsync(tagIds, cancellationToken).ConfigureAwait(false);

			var updated = new JournalEntry
			{
				Id = existing.Id,
				EntryDate = existing.EntryDate, // preserved
				CategoryId = categoryId,
				Title = NormalizeTitle(title),
				Content = NormalizeContent(content),
				CreatedAt = existing.CreatedAt, // preserved
				UpdatedAt = DateTimeOffset.UtcNow, // system-generated
			};

			await _repository.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
			await _entryMoods.ReplaceSelectionAsync(updated.Id, moodSelection, cancellationToken).ConfigureAwait(false);
			await _entryTags.ReplaceTagsAsync(updated.Id, tagIds, cancellationToken).ConfigureAwait(false);
			return updated;
		}
		catch (Exception ex) when (ex is DataAccessException)
		{
			throw new ServiceException("Failed to update journal entry.", ex);
		}
		catch (Exception ex) when (ex is not OperationCanceledException
		                           && ex is not ArgumentException
		                           && ex is not InvalidOperationException)
		{
			throw new ServiceException("Failed to update journal entry.", ex);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw;
		}
	}

	public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		try
		{
			var existing = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
			if (existing is null)
				return;

			// Clean up relationship rows first (no cascade guarantees across all platforms).
			await _entryTags.ReplaceTagsAsync(id, Array.Empty<Guid>(), cancellationToken).ConfigureAwait(false);
			await _entryMoods.DeleteSelectionAsync(id, cancellationToken).ConfigureAwait(false);

			await _repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is DataAccessException)
		{
			throw new ServiceException("Failed to delete journal entry.", ex);
		}
		catch (Exception ex) when (ex is not OperationCanceledException
		                           && ex is not ArgumentException
		                           && ex is not InvalidOperationException)
		{
			throw new ServiceException("Failed to delete journal entry.", ex);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw;
		}
	}

	private static void EnsureCategorySelected(Guid categoryId)
	{
		if (categoryId == Guid.Empty)
			throw new ArgumentException("Category is required.", nameof(categoryId));
	}

	private async Task EnsureCategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken)
	{
		var cat = await _categories.GetByIdAsync(categoryId, cancellationToken).ConfigureAwait(false);
		if (cat is null)
			throw new InvalidOperationException("Selected category does not exist.");
	}

	private async Task EnsureMoodSelectionValidAsync(MoodSelection moodSelection, CancellationToken cancellationToken)
	{
		if (moodSelection is null)
			throw new ArgumentNullException(nameof(moodSelection));

		if (moodSelection.PrimaryMoodId == Guid.Empty)
			throw new ArgumentException("Primary mood is required.", nameof(moodSelection));

		var secondary = moodSelection.SecondaryMoodIds?.Where(x => x != Guid.Empty).ToList() ?? new List<Guid>();
		if (secondary.Count > 2)
			throw new ArgumentException("Up to two secondary moods are allowed.", nameof(moodSelection));

		if (secondary.Contains(moodSelection.PrimaryMoodId))
			throw new ArgumentException("Primary mood cannot be also selected as secondary.", nameof(moodSelection));

		if (secondary.Distinct().Count() != secondary.Count)
			throw new ArgumentException("Secondary moods must be distinct.", nameof(moodSelection));

		// Ensure all mood ids exist
		if (!await _moods.ExistsAsync(moodSelection.PrimaryMoodId, cancellationToken).ConfigureAwait(false))
			throw new InvalidOperationException("Selected primary mood does not exist.");

		foreach (var id in secondary)
		{
			if (!await _moods.ExistsAsync(id, cancellationToken).ConfigureAwait(false))
				throw new InvalidOperationException("One of the selected secondary moods does not exist.");
		}
	}

	private async Task EnsureTagsValidAsync(IReadOnlyList<Guid> tagIds, CancellationToken cancellationToken)
	{
		tagIds ??= Array.Empty<Guid>();

		// Enforce distinctness.
		var distinct = tagIds.Where(x => x != Guid.Empty).Distinct().ToList();
		if (distinct.Count != tagIds.Count(x => x != Guid.Empty))
			throw new ArgumentException("Duplicate tags are not allowed.", nameof(tagIds));

		if (distinct.Count == 0)
			return;

		var existing = await _tags.GetByIdsAsync(distinct, cancellationToken).ConfigureAwait(false);
		if (existing.Count != distinct.Count)
			throw new InvalidOperationException("One or more selected tags do not exist.");
	}

	private static string NormalizeTitle(string title)
	{
		title = (title ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(title))
			throw new ArgumentException("Title is required.", nameof(title));

		if (title.Length > 200)
			title = title[..200];

		return title;
	}

	private static string NormalizeContent(string content)
	{
		return (content ?? string.Empty).Trim();
	}
}


