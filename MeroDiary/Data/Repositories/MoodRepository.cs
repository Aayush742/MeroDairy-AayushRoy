using MeroDiary.Data.Entities;
using MeroDiary.Data.Sqlite;
using MeroDiary.Domain.Enums;
using MeroDiary.Domain.Exceptions;
using MeroDiary.Domain.Models;

namespace MeroDiary.Data.Repositories;

public sealed class MoodRepository : IMoodRepository
{
	private readonly ISqliteConnectionProvider _connectionProvider;

	public MoodRepository(ISqliteConnectionProvider connectionProvider)
	{
		_connectionProvider = connectionProvider;
	}

	public async Task<IReadOnlyList<Mood>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var entities = await _connectionProvider.Connection
				.Table<MoodEntity>()
				.OrderBy(x => x.Category)
				.ThenBy(x => x.Name)
				.ToListAsync()
				.ConfigureAwait(false);

			return entities.Select(MapToDomain).ToList();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load moods.", ex);
		}
	}

	public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			var idStr = id.ToString("D");

			var entity = await _connectionProvider.Connection
				.Table<MoodEntity>()
				.Where(x => x.Id == idStr)
				.FirstOrDefaultAsync()
				.ConfigureAwait(false);

			return entity is not null;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to check mood existence.", ex);
		}
	}

	private static Mood MapToDomain(MoodEntity entity)
	{
		return new Mood
		{
			Id = Guid.Parse(entity.Id),
			Name = entity.Name,
			Category = (MoodCategory)entity.Category,
			IsPredefined = entity.IsPredefined,
		};
	}
}


