using MeroDiary.Domain.Models;

namespace MeroDiary.Services;

public interface IJournalEntryService
{
	Task<IReadOnlyList<JournalEntry>> GetAllAsync(Guid? categoryId = null, CancellationToken cancellationToken = default);
	Task<JournalEntry?> GetByDateAsync(DateOnly entryDate, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<DateOnly>> GetEntryDatesInRangeAsync(DateOnly startInclusive, DateOnly endInclusive, CancellationToken cancellationToken = default);
	Task<MoodSelection?> GetMoodSelectionAsync(Guid journalEntryId, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<Guid>> GetTagIdsAsync(Guid journalEntryId, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<JournalEntryListItem>> GetListPageAsync(int offset, int limit, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<JournalEntryListItem>> SearchListPageAsync(JournalEntryQuery query, int offset, int limit, CancellationToken cancellationToken = default);
	Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

	// Enforces: one journal entry per day. CreatedAt/UpdatedAt are system-generated.
	Task<JournalEntry> CreateAsync(DateOnly entryDate, Guid categoryId, MoodSelection moodSelection, IReadOnlyList<Guid> tagIds, string title, string content, CancellationToken cancellationToken = default);

	// Update an existing entry (any date). CreatedAt is preserved; UpdatedAt is system-generated.
	Task<JournalEntry> UpdateAsync(Guid id, Guid categoryId, MoodSelection moodSelection, IReadOnlyList<Guid> tagIds, string title, string content, CancellationToken cancellationToken = default);

	// Delete an existing entry (any date).
	Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}


