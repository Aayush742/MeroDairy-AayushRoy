using MeroDiary.Domain.Models;

namespace MeroDiary.Data.Repositories;

public interface IDiaryEntryRepository
{
	Task<IReadOnlyList<DiaryEntry>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<DiaryEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task AddAsync(DiaryEntry entry, CancellationToken cancellationToken = default);
	Task UpdateAsync(DiaryEntry entry, CancellationToken cancellationToken = default);
	Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}


