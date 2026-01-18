using MeroDiary.Data.Entities;
using MeroDiary.Data.Sqlite;
using MeroDiary.Domain.Exceptions;
using MeroDiary.Domain.Models;

namespace MeroDiary.Data.Repositories;

public sealed class JournalEntryRepository : IJournalEntryRepository
{
	private readonly ISqliteConnectionProvider _connectionProvider;

	public JournalEntryRepository(ISqliteConnectionProvider connectionProvider)
	{
		_connectionProvider = connectionProvider;
	}

	public async Task<IReadOnlyList<JournalEntry>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var entities = await _connectionProvider.Connection
				.Table<JournalEntryEntity>()
				.OrderByDescending(x => x.EntryDate)
				.ToListAsync()
				.ConfigureAwait(false);

			return entities.Select(MapToDomain).ToList();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load journal entries.", ex);
		}
	}

	private sealed class JournalEntrySummaryRow
	{
		// ReSharper disable once InconsistentNaming
		public string Id { get; set; } = string.Empty;
		// ReSharper disable once InconsistentNaming
		public string EntryDate { get; set; } = string.Empty;
		// ReSharper disable once InconsistentNaming
		public string Title { get; set; } = string.Empty;
		// ReSharper disable once InconsistentNaming
		public string CategoryId { get; set; } = string.Empty;
	}

	public async Task<IReadOnlyList<JournalEntrySummary>> GetSummariesPageAsync(int offset, int limit, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (offset < 0) offset = 0;
			if (limit <= 0) limit = 20;

			var rows = await _connectionProvider.Connection.QueryAsync<JournalEntrySummaryRow>(
					"SELECT Id, EntryDate, Title, CategoryId FROM JournalEntries ORDER BY EntryDate DESC LIMIT ? OFFSET ?",
					limit,
					offset)
				.ConfigureAwait(false);

			return rows.Select(r => new JournalEntrySummary
				{
					Id = Guid.Parse(r.Id),
					EntryDate = DateOnly.ParseExact(r.EntryDate, "yyyy-MM-dd"),
					Title = r.Title,
					CategoryId = Guid.Parse(r.CategoryId),
				})
				.ToList();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load journal entry summaries.", ex);
		}
	}

	public async Task<IReadOnlyList<JournalEntrySummary>> SearchSummariesPageAsync(
		JournalEntryQuery query,
		int offset,
		int limit,
		CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			query ??= new JournalEntryQuery();

			if (offset < 0) offset = 0;
			if (limit <= 0) limit = 20;

			var sql = new List<string>
			{
				"SELECT e.Id, e.EntryDate, e.Title, e.CategoryId",
				"FROM JournalEntries e",
				"WHERE 1=1",
			};

			var args = new List<object>();

			// Date range (ISO strings => lexical compare works)
			if (query.StartDateInclusive.HasValue)
			{
				sql.Add("AND e.EntryDate >= ?");
				args.Add(query.StartDateInclusive.Value.ToString("yyyy-MM-dd"));
			}
			if (query.EndDateInclusive.HasValue)
			{
				sql.Add("AND e.EntryDate <= ?");
				args.Add(query.EndDateInclusive.Value.ToString("yyyy-MM-dd"));
			}

			// Category
			if (query.CategoryId.HasValue && query.CategoryId.Value != Guid.Empty)
			{
				sql.Add("AND e.CategoryId = ?");
				args.Add(query.CategoryId.Value.ToString("D"));
			}

			// Search in Title or Content (escape LIKE wildcards)
			var q = (query.SearchText ?? string.Empty).Trim();
			if (!string.IsNullOrWhiteSpace(q))
			{
				var like = $"%{EscapeLike(q)}%";
				sql.Add("AND (e.Title LIKE ? ESCAPE '\\' OR e.Content LIKE ? ESCAPE '\\')");
				args.Add(like);
				args.Add(like);
			}

			// Mood(s): must contain ALL selected moods (primary or secondary)
			var moodIds = (query.MoodIds ?? Array.Empty<Guid>()).Where(x => x != Guid.Empty).Distinct().ToList();
			if (moodIds.Count > 0)
			{
				var placeholders = string.Join(",", moodIds.Select(_ => "?"));
				sql.Add(
					$"AND (SELECT COUNT(DISTINCT MoodId) FROM JournalEntryMoods jem " +
					$"     WHERE jem.JournalEntryId = e.Id AND jem.MoodId IN ({placeholders})) = {moodIds.Count}");
				args.AddRange(moodIds.Select(x => (object)x.ToString("D")));
			}

			// Tags: must contain ALL selected tags
			var tagIds = (query.TagIds ?? Array.Empty<Guid>()).Where(x => x != Guid.Empty).Distinct().ToList();
			if (tagIds.Count > 0)
			{
				var placeholders = string.Join(",", tagIds.Select(_ => "?"));
				sql.Add(
					$"AND (SELECT COUNT(DISTINCT TagId) FROM JournalEntryTags jet " +
					$"     WHERE jet.JournalEntryId = e.Id AND jet.TagId IN ({placeholders})) = {tagIds.Count}");
				args.AddRange(tagIds.Select(x => (object)x.ToString("D")));
			}

			sql.Add("ORDER BY e.EntryDate DESC");
			sql.Add("LIMIT ? OFFSET ?");
			args.Add(limit);
			args.Add(offset);

			var rows = await _connectionProvider.Connection
				.QueryAsync<JournalEntrySummaryRow>(string.Join("\n", sql), args.ToArray())
				.ConfigureAwait(false);

			return rows.Select(r => new JournalEntrySummary
				{
					Id = Guid.Parse(r.Id),
					EntryDate = DateOnly.ParseExact(r.EntryDate, "yyyy-MM-dd"),
					Title = r.Title,
					CategoryId = Guid.Parse(r.CategoryId),
				})
				.ToList();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to search journal entry summaries.", ex);
		}
	}

	private static string EscapeLike(string input)
	{
		// Escape LIKE wildcards (%) and (_) and the escape char itself (\)
		return input
			.Replace("\\", "\\\\", StringComparison.Ordinal)
			.Replace("%", "\\%", StringComparison.Ordinal)
			.Replace("_", "\\_", StringComparison.Ordinal);
	}

	public async Task<IReadOnlyList<JournalEntry>> GetAllByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var catId = categoryId.ToString("D");
			var entities = await _connectionProvider.Connection
				.Table<JournalEntryEntity>()
				.Where(x => x.CategoryId == catId)
				.OrderByDescending(x => x.EntryDate)
				.ToListAsync()
				.ConfigureAwait(false);

			return entities.Select(MapToDomain).ToList();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load journal entries by category.", ex);
		}
	}

	public async Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var idStr = id.ToString("D");
			var entity = await _connectionProvider.Connection
				.Table<JournalEntryEntity>()
				.Where(x => x.Id == idStr)
				.FirstOrDefaultAsync()
				.ConfigureAwait(false);

			return entity is null ? null : MapToDomain(entity);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load journal entry.", ex);
		}
	}

	public async Task<JournalEntry?> GetByDateAsync(DateOnly entryDate, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var key = entryDate.ToString("yyyy-MM-dd");
			var entity = await _connectionProvider.Connection
				.Table<JournalEntryEntity>()
				.Where(x => x.EntryDate == key)
				.FirstOrDefaultAsync()
				.ConfigureAwait(false);

			return entity is null ? null : MapToDomain(entity);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load journal entry by date.", ex);
		}
	}

	private sealed class MinMaxRow
	{
		// ReSharper disable once InconsistentNaming
		public string? MinDate { get; set; }
		// ReSharper disable once InconsistentNaming
		public string? MaxDate { get; set; }
	}

	public async Task<EntryDateRange> GetMinMaxEntryDateAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var row = await _connectionProvider.Connection
				.QueryAsync<MinMaxRow>("SELECT MIN(EntryDate) AS MinDate, MAX(EntryDate) AS MaxDate FROM JournalEntries")
				.ConfigureAwait(false);

			var first = row.FirstOrDefault();
			DateOnly? min = null;
			DateOnly? max = null;

			if (!string.IsNullOrWhiteSpace(first?.MinDate))
				min = DateOnly.ParseExact(first.MinDate!, "yyyy-MM-dd");
			if (!string.IsNullOrWhiteSpace(first?.MaxDate))
				max = DateOnly.ParseExact(first.MaxDate!, "yyyy-MM-dd");

			return new EntryDateRange { MinDate = min, MaxDate = max };
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load min/max entry dates.", ex);
		}
	}

	public async Task<IReadOnlyList<JournalEntry>> GetEntriesInRangeAsync(
		DateOnly startInclusive,
		DateOnly endInclusive,
		CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var start = startInclusive.ToString("yyyy-MM-dd");
			var end = endInclusive.ToString("yyyy-MM-dd");

			// EntryDate is stored as ISO "yyyy-MM-dd", so lexical ordering matches date ordering.
			var entities = await _connectionProvider.Connection
				.QueryAsync<JournalEntryEntity>(
					"SELECT * FROM JournalEntries WHERE EntryDate >= ? AND EntryDate <= ? ORDER BY EntryDate DESC",
					start,
					end)
				.ConfigureAwait(false);

			return entities.Select(MapToDomain).ToList();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load entries in range.", ex);
		}
	}

	private sealed class EntryDateRow
	{
		// ReSharper disable once InconsistentNaming
		public string EntryDate { get; set; } = string.Empty;
	}

	public async Task<IReadOnlyList<DateOnly>> GetEntryDatesInRangeAsync(
		DateOnly startInclusive,
		DateOnly endInclusive,
		CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var start = startInclusive.ToString("yyyy-MM-dd");
			var end = endInclusive.ToString("yyyy-MM-dd");

			// EntryDate is stored as ISO "yyyy-MM-dd", so lexical ordering matches date ordering.
			var rows = await _connectionProvider.Connection
				.QueryAsync<EntryDateRow>(
					"SELECT EntryDate FROM JournalEntries WHERE EntryDate >= ? AND EntryDate <= ?",
					start,
					end)
				.ConfigureAwait(false);

			return rows
				.Select(r => DateOnly.ParseExact(r.EntryDate, "yyyy-MM-dd"))
				.Distinct()
				.ToList();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load entry dates for calendar.", ex);
		}
	}

	public async Task AddAsync(JournalEntry entry, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			await _connectionProvider.Connection.InsertAsync(MapToEntity(entry)).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to add journal entry.", ex);
		}
	}

	public async Task UpdateAsync(JournalEntry entry, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			await _connectionProvider.Connection.UpdateAsync(MapToEntity(entry)).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to update journal entry.", ex);
		}
	}

	public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			await _connectionProvider.Connection.DeleteAsync<JournalEntryEntity>(id.ToString("D")).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to delete journal entry.", ex);
		}
	}

	private static JournalEntry MapToDomain(JournalEntryEntity entity)
	{
		return new JournalEntry
		{
			Id = Guid.Parse(entity.Id),
			EntryDate = DateOnly.ParseExact(entity.EntryDate, "yyyy-MM-dd"),
			CategoryId = Guid.Parse(entity.CategoryId),
			Title = entity.Title,
			Content = entity.Content,
			CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAtUtc, DateTimeKind.Utc)),
			UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(entity.UpdatedAtUtc, DateTimeKind.Utc)),
		};
	}

	private static JournalEntryEntity MapToEntity(JournalEntry entry)
	{
		return new JournalEntryEntity
		{
			Id = entry.Id.ToString("D"),
			EntryDate = entry.EntryDate.ToString("yyyy-MM-dd"),
			CategoryId = entry.CategoryId.ToString("D"),
			Title = entry.Title,
			Content = entry.Content,
			CreatedAtUtc = entry.CreatedAt.UtcDateTime,
			UpdatedAtUtc = entry.UpdatedAt.UtcDateTime,
		};
	}

	private sealed class CategoryCountRow
	{
		// ReSharper disable once InconsistentNaming
		public string CategoryId { get; set; } = string.Empty;

		// ReSharper disable once InconsistentNaming
		public int Count { get; set; }
	}

	public async Task<IReadOnlyDictionary<Guid, int>> GetCountsByCategoryAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var rows = await _connectionProvider.Connection
				.QueryAsync<CategoryCountRow>("SELECT CategoryId, COUNT(*) AS Count FROM JournalEntries GROUP BY CategoryId")
				.ConfigureAwait(false);

			return rows
				.Where(r => Guid.TryParse(r.CategoryId, out _))
				.ToDictionary(r => Guid.Parse(r.CategoryId), r => r.Count);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load journal analytics.", ex);
		}
	}
}


