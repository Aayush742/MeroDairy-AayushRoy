using MeroDiary.Data.Sqlite;
using MeroDiary.Domain.Enums;
using MeroDiary.Domain.Exceptions;
using MeroDiary.Domain.Models.Analytics;

namespace MeroDiary.Data.Repositories;

public sealed class AnalyticsRepository : IAnalyticsRepository
{
	private readonly ISqliteConnectionProvider _connectionProvider;

	public AnalyticsRepository(ISqliteConnectionProvider connectionProvider)
	{
		_connectionProvider = connectionProvider;
	}

	private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

	private sealed class MoodCategoryCountRow
	{
		// ReSharper disable once InconsistentNaming
		public int Category { get; set; }
		// ReSharper disable once InconsistentNaming
		public int Count { get; set; }
	}

	public async Task<IReadOnlyDictionary<int, int>> GetPrimaryMoodCategoryCountsAsync(
		DateOnly startInclusive,
		DateOnly endInclusive,
		CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			var start = Iso(startInclusive);
			var end = Iso(endInclusive);

			var rows = await _connectionProvider.Connection
				.QueryAsync<MoodCategoryCountRow>(
					"SELECT m.Category AS Category, COUNT(*) AS Count " +
					"FROM JournalEntries e " +
					"JOIN JournalEntryMoods jem ON jem.JournalEntryId = e.Id AND jem.Role = 1 " +
					"JOIN Moods m ON m.Id = jem.MoodId " +
					"WHERE e.EntryDate >= ? AND e.EntryDate <= ? " +
					"GROUP BY m.Category",
					start,
					end)
				.ConfigureAwait(false);

			return rows.ToDictionary(r => r.Category, r => r.Count);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load mood distribution.", ex);
		}
	}

	private sealed class MoodFrequencyRow
	{
		// ReSharper disable once InconsistentNaming
		public string MoodId { get; set; } = string.Empty;
		// ReSharper disable once InconsistentNaming
		public string Name { get; set; } = string.Empty;
		// ReSharper disable once InconsistentNaming
		public int Category { get; set; }
		// ReSharper disable once InconsistentNaming
		public int Count { get; set; }
	}

	public async Task<MoodFrequency?> GetMostFrequentPrimaryMoodAsync(
		DateOnly startInclusive,
		DateOnly endInclusive,
		CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			var start = Iso(startInclusive);
			var end = Iso(endInclusive);

			var rows = await _connectionProvider.Connection
				.QueryAsync<MoodFrequencyRow>(
					"SELECT m.Id AS MoodId, m.Name AS Name, m.Category AS Category, COUNT(*) AS Count " +
					"FROM JournalEntries e " +
					"JOIN JournalEntryMoods jem ON jem.JournalEntryId = e.Id AND jem.Role = 1 " +
					"JOIN Moods m ON m.Id = jem.MoodId " +
					"WHERE e.EntryDate >= ? AND e.EntryDate <= ? " +
					"GROUP BY m.Id, m.Name, m.Category " +
					"ORDER BY Count DESC, m.Name ASC " +
					"LIMIT 1",
					start,
					end)
				.ConfigureAwait(false);

			var row = rows.FirstOrDefault();
			if (row is null || !Guid.TryParse(row.MoodId, out var moodId))
				return null;

			return new MoodFrequency
			{
				MoodId = moodId,
				Name = row.Name,
				Category = (MoodCategory)row.Category,
				Count = row.Count,
			};
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load most frequent mood.", ex);
		}
	}

	private sealed class TagUsageRow
	{
		// ReSharper disable once InconsistentNaming
		public string TagId { get; set; } = string.Empty;
		// ReSharper disable once InconsistentNaming
		public string Name { get; set; } = string.Empty;
		// ReSharper disable once InconsistentNaming
		public int Count { get; set; }
	}

	public async Task<IReadOnlyList<TagUsage>> GetTopTagsAsync(
		DateOnly startInclusive,
		DateOnly endInclusive,
		int limit,
		CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (limit <= 0) limit = 10;

			var start = Iso(startInclusive);
			var end = Iso(endInclusive);

			var rows = await _connectionProvider.Connection
				.QueryAsync<TagUsageRow>(
					"SELECT t.Id AS TagId, t.Name AS Name, COUNT(*) AS Count " +
					"FROM JournalEntries e " +
					"JOIN JournalEntryTags jet ON jet.JournalEntryId = e.Id " +
					"JOIN Tags t ON t.Id = jet.TagId " +
					"WHERE e.EntryDate >= ? AND e.EntryDate <= ? " +
					"GROUP BY t.Id, t.Name " +
					"ORDER BY Count DESC, t.Name ASC " +
					"LIMIT ?",
					start,
					end,
					limit)
				.ConfigureAwait(false);

			return rows
				.Where(r => Guid.TryParse(r.TagId, out _))
				.Select(r => new TagUsage { TagId = Guid.Parse(r.TagId), Name = r.Name, Count = r.Count })
				.ToList();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load top tags.", ex);
		}
	}

	private sealed class CategoryCountRow
	{
		// ReSharper disable once InconsistentNaming
		public string CategoryId { get; set; } = string.Empty;
		// ReSharper disable once InconsistentNaming
		public string Name { get; set; } = string.Empty;
		// ReSharper disable once InconsistentNaming
		public int Count { get; set; }
	}

	public async Task<IReadOnlyList<CategoryBreakdownItem>> GetCategoryBreakdownAsync(
		DateOnly startInclusive,
		DateOnly endInclusive,
		CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			var start = Iso(startInclusive);
			var end = Iso(endInclusive);

			var rows = await _connectionProvider.Connection
				.QueryAsync<CategoryCountRow>(
					"SELECT c.Id AS CategoryId, c.Name AS Name, COUNT(*) AS Count " +
					"FROM JournalEntries e " +
					"JOIN Categories c ON c.Id = e.CategoryId " +
					"WHERE e.EntryDate >= ? AND e.EntryDate <= ? " +
					"GROUP BY c.Id, c.Name " +
					"ORDER BY Count DESC, c.Name ASC",
					start,
					end)
				.ConfigureAwait(false);

			return rows
				.Where(r => Guid.TryParse(r.CategoryId, out _))
				.Select(r => new CategoryBreakdownItem { CategoryId = Guid.Parse(r.CategoryId), Name = r.Name, Count = r.Count })
				.ToList();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load category breakdown.", ex);
		}
	}

	private sealed class EntryContentRow
	{
		// ReSharper disable once InconsistentNaming
		public string EntryDate { get; set; } = string.Empty;
		// ReSharper disable once InconsistentNaming
		public string Content { get; set; } = string.Empty;
	}

	public async Task<IReadOnlyList<(DateOnly Date, string Content)>> GetEntryContentsInRangeAsync(
		DateOnly startInclusive,
		DateOnly endInclusive,
		CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			var start = Iso(startInclusive);
			var end = Iso(endInclusive);

			var rows = await _connectionProvider.Connection
				.QueryAsync<EntryContentRow>(
					"SELECT EntryDate, Content FROM JournalEntries WHERE EntryDate >= ? AND EntryDate <= ?",
					start,
					end)
				.ConfigureAwait(false);

			return rows
				.Select(r => (DateOnly.ParseExact(r.EntryDate, "yyyy-MM-dd"), r.Content ?? string.Empty))
				.ToList();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load content for word trend.", ex);
		}
	}
}


