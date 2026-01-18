using MeroDiary.Data.Entities;
using MeroDiary.Data.Sqlite;
using MeroDiary.Domain.Exceptions;

namespace MeroDiary.Data.Repositories;

public sealed class JournalEntryTagRepository : IJournalEntryTagRepository
{
	private readonly ISqliteConnectionProvider _connectionProvider;

	public JournalEntryTagRepository(ISqliteConnectionProvider connectionProvider)
	{
		_connectionProvider = connectionProvider;
	}

	public async Task<IReadOnlyList<Guid>> GetTagIdsAsync(Guid journalEntryId, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			var entryId = journalEntryId.ToString("D");

			var rows = await _connectionProvider.Connection
				.Table<JournalEntryTagEntity>()
				.Where(x => x.JournalEntryId == entryId)
				.ToListAsync()
				.ConfigureAwait(false);

			return rows
				.Select(r => Guid.TryParse(r.TagId, out var id) ? id : Guid.Empty)
				.Where(id => id != Guid.Empty)
				.Distinct()
				.ToList();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load journal entry tags.", ex);
		}
	}

	private sealed class EntryTagRow
	{
		// ReSharper disable once InconsistentNaming
		public string JournalEntryId { get; set; } = string.Empty;
		// ReSharper disable once InconsistentNaming
		public string TagId { get; set; } = string.Empty;
	}

	public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetTagIdsByEntryAsync(
		IEnumerable<Guid> journalEntryIds,
		CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var ids = journalEntryIds?.Distinct().ToList() ?? new List<Guid>();
			if (ids.Count == 0)
				return new Dictionary<Guid, IReadOnlyList<Guid>>();

			var idStrings = ids.Select(x => x.ToString("D")).ToList();
			var placeholders = string.Join(",", idStrings.Select(_ => "?"));

			var rows = await _connectionProvider.Connection
				.QueryAsync<EntryTagRow>(
					$"SELECT JournalEntryId, TagId FROM JournalEntryTags WHERE JournalEntryId IN ({placeholders})",
					idStrings.Cast<object>().ToArray())
				.ConfigureAwait(false);

			return rows
				.Where(r => Guid.TryParse(r.JournalEntryId, out _) && Guid.TryParse(r.TagId, out _))
				.GroupBy(r => Guid.Parse(r.JournalEntryId))
				.ToDictionary(
					g => g.Key,
					g => (IReadOnlyList<Guid>)g.Select(x => Guid.Parse(x.TagId)).Distinct().ToList());
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load tags for entries.", ex);
		}
	}

	public async Task ReplaceTagsAsync(Guid journalEntryId, IEnumerable<Guid> tagIds, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			var entryId = journalEntryId.ToString("D");
			var ids = (tagIds ?? Array.Empty<Guid>()).Where(x => x != Guid.Empty).Distinct().ToList();

			await _connectionProvider.Connection.RunInTransactionAsync(conn =>
				{
					conn.Execute("DELETE FROM JournalEntryTags WHERE JournalEntryId = ?", entryId);

					foreach (var tagId in ids)
					{
						conn.Insert(new JournalEntryTagEntity
						{
							Id = Guid.NewGuid().ToString("D"),
							JournalEntryId = entryId,
							TagId = tagId.ToString("D"),
						});
					}
				})
				.ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to save journal entry tags.", ex);
		}
	}
}


