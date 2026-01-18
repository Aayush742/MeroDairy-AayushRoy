using MeroDiary.Data.Repositories;
using MeroDiary.Domain.Exceptions;
using MeroDiary.Domain.Models;

namespace MeroDiary.Services;

public sealed class DiaryEntryService : IDiaryEntryService
{
	private readonly IDiaryEntryRepository _repository;

	public DiaryEntryService(IDiaryEntryRepository repository)
	{
		_repository = repository;
	}

	public Task<IReadOnlyList<DiaryEntry>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		return _repository.GetAllAsync(cancellationToken);
	}

	public async Task<DiaryEntry> CreateAsync(
		string title,
		string content,
		DateTimeOffset entryDate,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var now = DateTimeOffset.UtcNow;
			var entry = new DiaryEntry
			{
				Id = Guid.NewGuid(),
				EntryDate = entryDate.ToUniversalTime(),
				Title = NormalizeTitle(title),
				Content = NormalizeContent(content),
				CreatedAtUtc = now,
				UpdatedAtUtc = now,
			};

			await _repository.AddAsync(entry, cancellationToken).ConfigureAwait(false);
			return entry;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new ServiceException("Failed to create diary entry.", ex);
		}
	}

	public async Task<DiaryEntry> UpdateAsync(
		Guid id,
		string title,
		string content,
		DateTimeOffset entryDate,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var existing = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
			if (existing is null)
				throw new InvalidOperationException("Diary entry does not exist.");

			var updated = new DiaryEntry
			{
				Id = existing.Id,
				EntryDate = entryDate.ToUniversalTime(),
				Title = NormalizeTitle(title),
				Content = NormalizeContent(content),
				CreatedAtUtc = existing.CreatedAtUtc,
				UpdatedAtUtc = DateTimeOffset.UtcNow,
			};

			await _repository.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
			return updated;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new ServiceException("Failed to update diary entry.", ex);
		}
	}

	public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		try
		{
			await _repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new ServiceException("Failed to delete diary entry.", ex);
		}
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


