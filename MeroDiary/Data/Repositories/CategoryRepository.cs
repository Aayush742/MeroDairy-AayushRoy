using MeroDiary.Data.Entities;
using MeroDiary.Data.Sqlite;
using MeroDiary.Domain.Exceptions;
using MeroDiary.Domain.Models;

namespace MeroDiary.Data.Repositories;

public sealed class CategoryRepository : ICategoryRepository
{
	private readonly ISqliteConnectionProvider _connectionProvider;

	public CategoryRepository(ISqliteConnectionProvider connectionProvider)
	{
		_connectionProvider = connectionProvider;
	}

	public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var entities = await _connectionProvider.Connection
				.Table<CategoryEntity>()
				.OrderBy(x => x.Name)
				.ToListAsync()
				.ConfigureAwait(false);

			return entities.Select(MapToDomain).ToList();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load categories.", ex);
		}
	}

	public async Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var idStr = id.ToString("D");
			var entity = await _connectionProvider.Connection
				.Table<CategoryEntity>()
				.Where(x => x.Id == idStr)
				.FirstOrDefaultAsync()
				.ConfigureAwait(false);

			return entity is null ? null : MapToDomain(entity);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load category.", ex);
		}
	}

	public async Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			name = (name ?? string.Empty).Trim();

			var entity = await _connectionProvider.Connection
				.Table<CategoryEntity>()
				.Where(x => x.Name == name)
				.FirstOrDefaultAsync()
				.ConfigureAwait(false);

			return entity is null ? null : MapToDomain(entity);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load category by name.", ex);
		}
	}

	public async Task<IReadOnlyList<Category>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var idList = ids?.Distinct().ToList() ?? new List<Guid>();
			if (idList.Count == 0)
				return Array.Empty<Category>();

			var idStrings = idList.Select(x => x.ToString("D")).ToList();
			var placeholders = string.Join(",", idStrings.Select(_ => "?"));

			var rows = await _connectionProvider.Connection
				.QueryAsync<CategoryEntity>($"SELECT * FROM Categories WHERE Id IN ({placeholders})", idStrings.Cast<object>().ToArray())
				.ConfigureAwait(false);

			return rows.Select(MapToDomain).ToList();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load categories by ids.", ex);
		}
	}

	public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			await _connectionProvider.Connection.InsertAsync(MapToEntity(category)).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to add category.", ex);
		}
	}

	public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			await _connectionProvider.Connection.DeleteAsync<CategoryEntity>(id.ToString("D")).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to delete category.", ex);
		}
	}

	private sealed class CategoryCountRow
	{
		// ReSharper disable once InconsistentNaming
		public string CategoryId { get; set; } = string.Empty;

		// ReSharper disable once InconsistentNaming
		public int Count { get; set; }
	}

	public async Task<IReadOnlyDictionary<Guid, int>> GetEntryCountsByCategoryAsync(CancellationToken cancellationToken = default)
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
			throw new DataAccessException("Failed to load category analytics.", ex);
		}
	}

	private static Category MapToDomain(CategoryEntity entity)
	{
		return new Category
		{
			Id = Guid.Parse(entity.Id),
			Name = entity.Name,
			IsPredefined = entity.IsPredefined,
			CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAtUtc, DateTimeKind.Utc)),
			UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(entity.UpdatedAtUtc, DateTimeKind.Utc)),
		};
	}

	private static CategoryEntity MapToEntity(Category category)
	{
		return new CategoryEntity
		{
			Id = category.Id.ToString("D"),
			Name = category.Name,
			IsPredefined = category.IsPredefined,
			CreatedAtUtc = category.CreatedAt.UtcDateTime,
			UpdatedAtUtc = category.UpdatedAt.UtcDateTime,
		};
	}
}


