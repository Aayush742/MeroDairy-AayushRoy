using SQLite;

namespace MeroDiary.Data.Entities;

[Table("JournalEntries")]
public sealed class JournalEntryEntity
{
	[PrimaryKey]
	public string Id { get; set; } = string.Empty;

	// Local calendar date: "yyyy-MM-dd". Unique => one entry per day.
	[Indexed(Name = "IX_JournalEntries_EntryDate", Unique = true)]
	public string EntryDate { get; set; } = string.Empty;

	[Indexed(Name = "IX_JournalEntries_CategoryId")]
	public string CategoryId { get; set; } = string.Empty;

	[MaxLength(200)]
	public string Title { get; set; } = string.Empty;

	// Markdown
	public string Content { get; set; } = string.Empty;

	public DateTime CreatedAtUtc { get; set; }
	public DateTime UpdatedAtUtc { get; set; }
}


