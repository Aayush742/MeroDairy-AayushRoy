using MeroDiary.Data.Entities;
using MeroDiary.Data.Sqlite;
using MeroDiary.Domain.Exceptions;
using MeroDiary.Domain.Models;
using SQLite;

namespace MeroDiary.Data.Repositories;

public sealed class DiaryEntryRepository : IDiaryEntryRepository
{
	private readonly ISqliteConnectionProvider _connectionProvider;

	public DiaryEntryRepository(ISqliteConnectionProvider connectionProvider)
	{
		_connectionProvider = connectionProvider;
	}

	public async Task<IReadOnlyList<DiaryEntry>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var entities = await _connectionProvider.Connection
				.Table<DiaryEntryEntity>()
				.OrderByDescending(x => x.EntryDateUtc)
				.ToListAsync()
				.ConfigureAwait(false);

			return entities.Select(MapToDomain).ToList();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load diary entries.", ex);
		}
	}

	public async Task<DiaryEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var idStr = id.ToString("D");
			var entity = await _connectionProvider.Connection
				.Table<DiaryEntryEntity>()
				.Where(x => x.Id == idStr)
				.FirstOrDefaultAsync()
				.ConfigureAwait(false);

			return entity is null ? null : MapToDomain(entity);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load diary entry.", ex);
		}
	}

	public async Task AddAsync(DiaryEntry entry, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			var entity = MapToEntity(entry);
			await _connectionProvider.Connection.InsertAsync(entity).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to add diary entry.", ex);
		}
	}

	public async Task UpdateAsync(DiaryEntry entry, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			var entity = MapToEntity(entry);
			await _connectionProvider.Connection.UpdateAsync(entity).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to update diary entry.", ex);
		}
	}

	public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			await _connectionProvider.Connection.DeleteAsync<DiaryEntryEntity>(id.ToString("D")).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to delete diary entry.", ex);
		}
	}

	private static DiaryEntry MapToDomain(DiaryEntryEntity entity)
	{
		return new DiaryEntry
		{
			Id = Guid.Parse(entity.Id),
			EntryDate = new DateTimeOffset(DateTime.SpecifyKind(entity.EntryDateUtc, DateTimeKind.Utc)),
			Title = entity.Title,
			Content = entity.Content,
			CreatedAtUtc = new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAtUtc, DateTimeKind.Utc)),
			UpdatedAtUtc = new DateTimeOffset(DateTime.SpecifyKind(entity.UpdatedAtUtc, DateTimeKind.Utc)),
		};
	}

	private static DiaryEntryEntity MapToEntity(DiaryEntry entry)
	{
		return new DiaryEntryEntity
		{
			Id = entry.Id.ToString("D"),
			EntryDateUtc = entry.EntryDate.UtcDateTime,
			Title = entry.Title,
			Content = entry.Content,
			CreatedAtUtc = entry.CreatedAtUtc.UtcDateTime,
			UpdatedAtUtc = entry.UpdatedAtUtc.UtcDateTime,
		};
	}
}


