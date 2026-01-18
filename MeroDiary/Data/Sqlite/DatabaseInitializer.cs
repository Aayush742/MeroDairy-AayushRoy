using MeroDiary.Data.Entities;
using MeroDiary.Domain.Constants;
using Microsoft.Extensions.Logging;

namespace MeroDiary.Data.Sqlite;

public sealed class DatabaseInitializer : IDatabaseInitializer
{
	private readonly ISqliteConnectionProvider _connectionProvider;
	private readonly ILogger<DatabaseInitializer> _logger;
	private int _initialized;

	public DatabaseInitializer(
		ISqliteConnectionProvider connectionProvider,
		ILogger<DatabaseInitializer> logger)
	{
		_connectionProvider = connectionProvider;
		_logger = logger;
	}

	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		if (Interlocked.Exchange(ref _initialized, 1) == 1)
			return;

		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			// Enable FK constraints (connection-scoped in SQLite)
			await _connectionProvider.Connection.ExecuteAsync("PRAGMA foreign_keys=ON").ConfigureAwait(false);

			// Moods (seed predefined values)
			await _connectionProvider.Connection.CreateTableAsync<MoodEntity>().ConfigureAwait(false);
			await SeedPredefinedMoodsAsync().ConfigureAwait(false);

			// Tags (seed predefined values)
			await _connectionProvider.Connection.CreateTableAsync<TagEntity>().ConfigureAwait(false);
			await SeedPredefinedTagsAsync().ConfigureAwait(false);

			// Categories (seed predefined values)
			await _connectionProvider.Connection.CreateTableAsync<CategoryEntity>().ConfigureAwait(false);
			await SeedPredefinedCategoriesAsync().ConfigureAwait(false);

			// Journal entries
			await _connectionProvider.Connection.CreateTableAsync<JournalEntryEntity>().ConfigureAwait(false);

			// Defensive: ensure unique index exists even if attribute-based index creation changes
			// or an older DB existed before we added the uniqueness rule.
			await _connectionProvider.Connection.ExecuteAsync(
					"CREATE UNIQUE INDEX IF NOT EXISTS IX_JournalEntries_EntryDate ON JournalEntries(EntryDate)")
				.ConfigureAwait(false);

			// Migration: ensure CategoryId exists (older DBs created before categories)
			await EnsureJournalCategoryColumnAsync(defaultCategoryId: PredefinedCategories.DefaultId).ConfigureAwait(false);

			// Defensive: index for category analytics/filtering
			await _connectionProvider.Connection.ExecuteAsync(
					"CREATE INDEX IF NOT EXISTS IX_JournalEntries_CategoryId ON JournalEntries(CategoryId)")
				.ConfigureAwait(false);

			// Entry moods relationship
			await _connectionProvider.Connection.CreateTableAsync<JournalEntryMoodEntity>().ConfigureAwait(false);
			await _connectionProvider.Connection.ExecuteAsync(
					"CREATE UNIQUE INDEX IF NOT EXISTS IX_JournalEntryMoods_UQ_PrimaryPerEntry ON JournalEntryMoods(JournalEntryId, Role) WHERE Role = 1")
				.ConfigureAwait(false);
			await _connectionProvider.Connection.ExecuteAsync(
					"CREATE UNIQUE INDEX IF NOT EXISTS IX_JournalEntryMoods_UQ_SecondaryPositions ON JournalEntryMoods(JournalEntryId, Role, Position) WHERE Role = 2")
				.ConfigureAwait(false);
			await _connectionProvider.Connection.ExecuteAsync(
					"CREATE UNIQUE INDEX IF NOT EXISTS IX_JournalEntryMoods_UQ_NoDuplicateMoodPerEntry ON JournalEntryMoods(JournalEntryId, MoodId)")
				.ConfigureAwait(false);
			await _connectionProvider.Connection.ExecuteAsync(
					"CREATE INDEX IF NOT EXISTS IX_JournalEntryMoods_MoodId_EntryId ON JournalEntryMoods(MoodId, JournalEntryId)")
				.ConfigureAwait(false);

			// Entry tags relationship
			await _connectionProvider.Connection.CreateTableAsync<JournalEntryTagEntity>().ConfigureAwait(false);
			await _connectionProvider.Connection.ExecuteAsync(
					"CREATE UNIQUE INDEX IF NOT EXISTS IX_JournalEntryTags_UQ ON JournalEntryTags(JournalEntryId, TagId)")
				.ConfigureAwait(false);
			await _connectionProvider.Connection.ExecuteAsync(
					"CREATE INDEX IF NOT EXISTS IX_JournalEntryTags_TagId_EntryId ON JournalEntryTags(TagId, JournalEntryId)")
				.ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			// Allow retry if initialization failed.
			Interlocked.Exchange(ref _initialized, 0);
			_logger.LogError(ex, "Failed to initialize local database.");
			throw;
		}
	}

	private async Task SeedPredefinedCategoriesAsync()
	{
		var now = DateTime.UtcNow;

		foreach (var (id, name) in PredefinedCategories.All)
		{
			var idStr = id.ToString("D");
			var existing = await _connectionProvider.Connection
				.Table<CategoryEntity>()
				.Where(x => x.Id == idStr)
				.FirstOrDefaultAsync()
				.ConfigureAwait(false);

			if (existing is not null)
				continue;

			await _connectionProvider.Connection.InsertAsync(new CategoryEntity
				{
					Id = idStr,
					Name = name,
					IsPredefined = true,
					CreatedAtUtc = now,
					UpdatedAtUtc = now,
				})
				.ConfigureAwait(false);
		}
	}

	private async Task SeedPredefinedMoodsAsync()
	{
		foreach (var (id, name, category) in PredefinedMoods.All)
		{
			var idStr = id.ToString("D");
			var existing = await _connectionProvider.Connection
				.Table<MoodEntity>()
				.Where(x => x.Id == idStr)
				.FirstOrDefaultAsync()
				.ConfigureAwait(false);

			if (existing is not null)
				continue;

			await _connectionProvider.Connection.InsertAsync(new MoodEntity
				{
					Id = idStr,
					Name = name,
					Category = (int)category,
					IsPredefined = true,
				})
				.ConfigureAwait(false);
		}
	}

	private async Task SeedPredefinedTagsAsync()
	{
		var now = DateTime.UtcNow;

		foreach (var (id, name) in PredefinedTags.All)
		{
			var idStr = id.ToString("D");
			var existing = await _connectionProvider.Connection
				.Table<TagEntity>()
				.Where(x => x.Id == idStr)
				.FirstOrDefaultAsync()
				.ConfigureAwait(false);

			if (existing is not null)
				continue;

			await _connectionProvider.Connection.InsertAsync(new TagEntity
				{
					Id = idStr,
					Name = name,
					NormalizedName = NormalizeTagName(name),
					IsPredefined = true,
					CreatedAtUtc = now,
					UpdatedAtUtc = now,
				})
				.ConfigureAwait(false);
		}
	}

	private static string NormalizeTagName(string name)
	{
		return (name ?? string.Empty).Trim().ToLowerInvariant();
	}

	private sealed class SqliteTableInfoRow
	{
		// ReSharper disable once InconsistentNaming
		public string name { get; set; } = string.Empty;
	}

	private async Task EnsureJournalCategoryColumnAsync(Guid defaultCategoryId)
	{
		var cols = await _connectionProvider.Connection
			.QueryAsync<SqliteTableInfoRow>("PRAGMA table_info('JournalEntries')")
			.ConfigureAwait(false);

		var hasCategoryId = cols.Any(c => string.Equals(c.name, "CategoryId", StringComparison.OrdinalIgnoreCase));
		if (hasCategoryId)
			return;

		var defaultId = defaultCategoryId.ToString("D");
		// SQLite allows adding a NOT NULL column only with a DEFAULT. We backfill existing rows to the default category.
		await _connectionProvider.Connection.ExecuteAsync(
				$"ALTER TABLE JournalEntries ADD COLUMN CategoryId TEXT NOT NULL DEFAULT '{defaultId}'")
			.ConfigureAwait(false);
	}
}


