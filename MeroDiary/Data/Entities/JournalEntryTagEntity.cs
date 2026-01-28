using SQLite;

namespace MeroDiary.Data.Entities;

[Table("JournalEntryTags")]
public sealed class JournalEntryTagEntity
{
	[PrimaryKey]
	public string Id { get; set; } = string.Empty;

	[Indexed(Name = "IX_JournalEntryTags_EntryId")]
	public string JournalEntryId { get; set; } = string.Empty;

	[Indexed(Name = "IX_JournalEntryTags_TagId")]
	public string TagId { get; set; } = string.Empty;
}


