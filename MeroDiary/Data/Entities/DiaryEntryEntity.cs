using SQLite;

namespace MeroDiary.Data.Entities;

[Table("DiaryEntries")]
public sealed class DiaryEntryEntity
{
	[PrimaryKey]
	public string Id { get; set; } = string.Empty;

	[Indexed]
	public DateTime EntryDateUtc { get; set; }

	[MaxLength(200)]
	public string Title { get; set; } = string.Empty;

	public string Content { get; set; } = string.Empty;

	public DateTime CreatedAtUtc { get; set; }
	public DateTime UpdatedAtUtc { get; set; }
}


