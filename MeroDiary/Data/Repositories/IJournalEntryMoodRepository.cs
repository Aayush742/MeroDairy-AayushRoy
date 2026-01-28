using MeroDiary.Domain.Models;

namespace MeroDiary.Data.Repositories;

public interface IJournalEntryMoodRepository
{
	Task<MoodSelection?> GetSelectionAsync(Guid journalEntryId, CancellationToken cancellationToken = default);
	Task<IReadOnlyDictionary<Guid, Guid>> GetPrimaryMoodIdsByEntryAsync(IEnumerable<Guid> journalEntryIds, CancellationToken cancellationToken = default);
	Task<IReadOnlyDictionary<Guid, MoodSelection>> GetSelectionsByEntryAsync(IEnumerable<Guid> journalEntryIds, CancellationToken cancellationToken = default);
	Task ReplaceSelectionAsync(Guid journalEntryId, MoodSelection selection, CancellationToken cancellationToken = default);
	Task DeleteSelectionAsync(Guid journalEntryId, CancellationToken cancellationToken = default);
}


