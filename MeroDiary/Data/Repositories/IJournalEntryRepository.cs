using MeroDiary.Domain.Models;

namespace MeroDiary.Data.Repositories;

public interface IJournalEntryRepository
{
	Task<IReadOnlyList<JournalEntry>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<IReadOnlyList<JournalEntry>> GetAllByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<JournalEntrySummary>> GetSummariesPageAsync(int offset, int limit, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<JournalEntrySummary>> SearchSummariesPageAsync(JournalEntryQuery query, int offset, int limit, CancellationToken cancellationToken = default);
	Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task<JournalEntry?> GetByDateAsync(DateOnly entryDate, CancellationToken cancellationToken = default);
	Task<EntryDateRange> GetMinMaxEntryDateAsync(CancellationToken cancellationToken = default);
	Task<IReadOnlyList<DateOnly>> GetEntryDatesInRangeAsync(DateOnly startInclusive, DateOnly endInclusive, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<JournalEntry>> GetEntriesInRangeAsync(DateOnly startInclusive, DateOnly endInclusive, CancellationToken cancellationToken = default);

	Task AddAsync(JournalEntry entry, CancellationToken cancellationToken = default);
	Task UpdateAsync(JournalEntry entry, CancellationToken cancellationToken = default);
	Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

	// Analytics helper
	Task<IReadOnlyDictionary<Guid, int>> GetCountsByCategoryAsync(CancellationToken cancellationToken = default);
}


