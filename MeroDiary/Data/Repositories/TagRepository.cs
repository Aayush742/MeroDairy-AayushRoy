using MeroDiary.Data.Entities;
using MeroDiary.Data.Sqlite;
using MeroDiary.Domain.Exceptions;
using MeroDiary.Domain.Models;
using SQLite;

namespace MeroDiary.Data.Repositories;

public sealed class TagRepository : ITagRepository
{
	private readonly ISqliteConnectionProvider _connectionProvider;

	public TagRepository(ISqliteConnectionProvider connectionProvider)
	{
		_connectionProvider = connectionProvider;
	}

	public async Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var entities = await _connectionProvider.Connection
				.Table<TagEntity>()
				.OrderBy(x => x.Name)
				.ToListAsync()
				.ConfigureAwait(false);

			return entities.Select(MapToDomain).ToList();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load tags.", ex);
		}
	}

	public async Task<Tag?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			normalizedName = (normalizedName ?? string.Empty).Trim().ToLowerInvariant();

			var entity = await _connectionProvider.Connection
				.Table<TagEntity>()
				.Where(x => x.NormalizedName == normalizedName)
				.FirstOrDefaultAsync()
				.ConfigureAwait(false);

			return entity is null ? null : MapToDomain(entity);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load tag by name.", ex);
		}
	}

	public async Task<Tag?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			var idStr = id.ToString("D");
			var entity = await _connectionProvider.Connection
				.Table<TagEntity>()
				.Where(x => x.Id == idStr)
				.FirstOrDefaultAsync()
				.ConfigureAwait(false);

			return entity is null ? null : MapToDomain(entity);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load tag.", ex);
		}
	}

	public async Task<IReadOnlyList<Tag>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			var idList = ids?.Distinct().ToList() ?? new List<Guid>();
			if (idList.Count == 0)
				return Array.Empty<Tag>();

			var idStrings = idList.Select(x => x.ToString("D")).ToList();
			var placeholders = string.Join(",", idStrings.Select(_ => "?"));

			var rows = await _connectionProvider.Connection
				.QueryAsync<TagEntity>($"SELECT * FROM Tags WHERE Id IN ({placeholders})", idStrings.Cast<object>().ToArray())
				.ConfigureAwait(false);

			return rows.Select(MapToDomain).ToList();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to load tags by ids.", ex);
		}
	}

	public async Task<Tag> GetOrCreateAsync(string name, bool isPredefined, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			name = NormalizeName(name);
			var normalized = NormalizeNormalized(name);

			var existing = await GetByNormalizedNameAsync(normalized, cancellationToken).ConfigureAwait(false);
			if (existing is not null)
				return existing;

			var now = DateTimeOffset.UtcNow;
			var tag = new Tag
			{
				Id = Guid.NewGuid(),
				Name = name,
				IsPredefined = isPredefined,
				CreatedAt = now,
				UpdatedAt = now,
			};

			try
			{
				await _connectionProvider.Connection.InsertAsync(MapToEntity(tag)).ConfigureAwait(false);
				return tag;
			}
			catch (SQLiteException ex) when (ex.Result == SQLite3.Result.Constraint)
			{
				// Another writer inserted same tag concurrently; fetch it.
				var fetched = await GetByNormalizedNameAsync(normalized, cancellationToken).ConfigureAwait(false);
				if (fetched is not null)
					return fetched;
				throw;
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new DataAccessException("Failed to create tag.", ex);
		}
	}

	private static string NormalizeName(string name)
	{
		name = (name ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("Tag name is required.", nameof(name));

		if (name.Length > 100)
			name = name[..100];

		return name;
	}

	private static string NormalizeNormalized(string name)
	{
		return (name ?? string.Empty).Trim().ToLowerInvariant();
	}

	private static Tag MapToDomain(TagEntity entity)
	{
		return new Tag
		{
			Id = Guid.Parse(entity.Id),
			Name = entity.Name,
			IsPredefined = entity.IsPredefined,
			CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAtUtc, DateTimeKind.Utc)),
			UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(entity.UpdatedAtUtc, DateTimeKind.Utc)),
		};
	}

	private static TagEntity MapToEntity(Tag tag)
	{
		return new TagEntity
		{
			Id = tag.Id.ToString("D"),
			Name = tag.Name,
			NormalizedName = NormalizeNormalized(tag.Name),
			IsPredefined = tag.IsPredefined,
			CreatedAtUtc = tag.CreatedAt.UtcDateTime,
			UpdatedAtUtc = tag.UpdatedAt.UtcDateTime,
		};
	}
}


