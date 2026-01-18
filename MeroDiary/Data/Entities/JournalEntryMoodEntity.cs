using SQLite;

namespace MeroDiary.Data.Entities;

[Table("JournalEntryMoods")]
public sealed class JournalEntryMoodEntity
{
	// Composite key isn't directly supported by sqlite-net attributes,
	// so we store an explicit Id and enforce uniqueness with indexes.
	[PrimaryKey]
	public string Id { get; set; } = string.Empty;

	[Indexed(Name = "IX_JournalEntryMoods_EntryId")]
	public string JournalEntryId { get; set; } = string.Empty;

	[Indexed(Name = "IX_JournalEntryMoods_MoodId")]
	public string MoodId { get; set; } = string.Empty;

	// 1=Primary, 2=Secondary
	[Indexed(Name = "IX_JournalEntryMoods_Role")]
	public int Role { get; set; }

	// For secondaries: 1 or 2. For primary: 0.
	public int Position { get; set; }
}


