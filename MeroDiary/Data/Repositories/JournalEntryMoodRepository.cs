using MeroDiary.Data.Entities;
using MeroDiary.Data.Sqlite;
using MeroDiary.Domain.Enums;
using MeroDiary.Domain.Exceptions;
using MeroDiary.Domain.Models;

namespace MeroDiary.Data.Repositories;

public sealed class JournalEntryMoodRepository : IJournalEntryMoodRepository
{
	private readonly ISqliteConnectionProvider _connectionProvider;

	public JournalEntryMoodRepository(ISqliteConnectionProvider connectionProvider)
	{
		_connectionProvider = connectionProvider;
	}

	public async Task<MoodSelection?> GetSelectionAsync(Guid journalEntryId, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			var entryId = journalEntryId.ToString("D");

			var rows = await _connectionProvider.Connection
				.Table<JournalEntryMoodEntity>()
				.Where(x => x.JournalEntryId == entryId)
				.ToListAsync()
				.ConfigureAwait(false);

			if (rows.Count == 0)
				return null;

			var primary = rows.FirstOrDefault(r => r.Role == (int)MoodRole.Primary);
			if (primary is null || !Guid.TryParse(primary.MoodId, out var primaryId))
				return null;

			var secondaryIds = rows
				.Where(r => r.Role == (int)MoodRole.Secondary)
				.OrderBy(r => r.Position)
				.Select(r => Guid.TryParse(r.MoodId, out var id) ? id : Guid.Empty)
				.Where(id => id != Guid.Empty)
				.Take(2)
				.ToList();

			return new MoodSelection
			{
				PrimaryMoodId = primaryId,
				SecondaryMoodIds = secondaryIds,
			};
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load mood selection.", ex);
		}
	}

	private sealed class EntryMoodRow
	{
		// ReSharper disable once InconsistentNaming
		public string JournalEntryId { get; set; } = string.Empty;
		// ReSharper disable once InconsistentNaming
		public string MoodId { get; set; } = string.Empty;
	}

	public async Task<IReadOnlyDictionary<Guid, Guid>> GetPrimaryMoodIdsByEntryAsync(
		IEnumerable<Guid> journalEntryIds,
		CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var ids = journalEntryIds?.Distinct().ToList() ?? new List<Guid>();
			if (ids.Count == 0)
				return new Dictionary<Guid, Guid>();

			var idStrings = ids.Select(x => x.ToString("D")).ToList();
			var placeholders = string.Join(",", idStrings.Select(_ => "?"));

			var rows = await _connectionProvider.Connection
				.QueryAsync<EntryMoodRow>(
					$"SELECT JournalEntryId, MoodId FROM JournalEntryMoods WHERE Role = {(int)MoodRole.Primary} AND JournalEntryId IN ({placeholders})",
					idStrings.Cast<object>().ToArray())
				.ConfigureAwait(false);

			return rows
				.Where(r => Guid.TryParse(r.JournalEntryId, out _) && Guid.TryParse(r.MoodId, out _))
				.ToDictionary(r => Guid.Parse(r.JournalEntryId), r => Guid.Parse(r.MoodId));
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load primary moods for entries.", ex);
		}
	}

	public async Task<IReadOnlyDictionary<Guid, MoodSelection>> GetSelectionsByEntryAsync(
		IEnumerable<Guid> journalEntryIds,
		CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var ids = journalEntryIds?.Distinct().ToList() ?? new List<Guid>();
			if (ids.Count == 0)
				return new Dictionary<Guid, MoodSelection>();

			var idStrings = ids.Select(x => x.ToString("D")).ToList();
			var placeholders = string.Join(",", idStrings.Select(_ => "?"));

			var rows = await _connectionProvider.Connection
				.QueryAsync<JournalEntryMoodEntity>(
					$"SELECT * FROM JournalEntryMoods WHERE JournalEntryId IN ({placeholders})",
					idStrings.Cast<object>().ToArray())
				.ConfigureAwait(false);

			return rows
				.Where(r => Guid.TryParse(r.JournalEntryId, out _) && Guid.TryParse(r.MoodId, out _))
				.GroupBy(r => Guid.Parse(r.JournalEntryId))
				.ToDictionary(
					g => g.Key,
					g =>
					{
						var primary = g.FirstOrDefault(x => x.Role == (int)MoodRole.Primary);
						var primaryId = primary is null ? Guid.Empty : Guid.Parse(primary.MoodId);
						var secondaries = g
							.Where(x => x.Role == (int)MoodRole.Secondary)
							.OrderBy(x => x.Position)
							.Select(x => Guid.Parse(x.MoodId))
							.Take(2)
							.ToList();

						return new MoodSelection
						{
							PrimaryMoodId = primaryId,
							SecondaryMoodIds = secondaries,
						};
					});
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load mood selections for entries.", ex);
		}
	}

	public async Task ReplaceSelectionAsync(Guid journalEntryId, MoodSelection selection, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			var entryId = journalEntryId.ToString("D");

			// sqlite-net's transaction helper is sync inside, but it ensures atomicity.
			await _connectionProvider.Connection.RunInTransactionAsync(conn =>
				{
					// delete previous selection
					conn.Execute("DELETE FROM JournalEntryMoods WHERE JournalEntryId = ?", entryId);

					// insert primary
					conn.Insert(new JournalEntryMoodEntity
					{
						Id = Guid.NewGuid().ToString("D"),
						JournalEntryId = entryId,
						MoodId = selection.PrimaryMoodId.ToString("D"),
						Role = (int)MoodRole.Primary,
						Position = 0,
					});

					// insert secondaries (0..2)
					var secondaries = selection.SecondaryMoodIds?.Where(x => x != Guid.Empty).Distinct().Take(2).ToList()
						?? new List<Guid>();

					for (var i = 0; i < secondaries.Count; i++)
					{
						conn.Insert(new JournalEntryMoodEntity
						{
							Id = Guid.NewGuid().ToString("D"),
							JournalEntryId = entryId,
							MoodId = secondaries[i].ToString("D"),
							Role = (int)MoodRole.Secondary,
							Position = i + 1, // 1 or 2
						});
					}
				})
				.ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to save mood selection.", ex);
		}
	}

	public async Task DeleteSelectionAsync(Guid journalEntryId, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			var entryId = journalEntryId.ToString("D");
			await _connectionProvider.Connection
				.ExecuteAsync("DELETE FROM JournalEntryMoods WHERE JournalEntryId = ?", entryId)
				.ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to delete mood selection.", ex);
		}
	}
}


