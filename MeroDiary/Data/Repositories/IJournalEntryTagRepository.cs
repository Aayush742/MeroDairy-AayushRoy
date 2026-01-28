using MeroDiary.Domain.Models;

namespace MeroDiary.Data.Repositories;

public interface IJournalEntryTagRepository
{
	Task<IReadOnlyList<Guid>> GetTagIdsAsync(Guid journalEntryId, CancellationToken cancellationToken = default);
	Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetTagIdsByEntryAsync(IEnumerable<Guid> journalEntryIds, CancellationToken cancellationToken = default);
	Task ReplaceTagsAsync(Guid journalEntryId, IEnumerable<Guid> tagIds, CancellationToken cancellationToken = default);
}


