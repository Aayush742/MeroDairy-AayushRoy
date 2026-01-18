using SQLite;

namespace MeroDiary.Data.Entities;

[Table("Moods")]
public sealed class MoodEntity
{
	[PrimaryKey]
	public string Id { get; set; } = string.Empty;

	[Indexed(Name = "IX_Moods_Name", Unique = true)]
	[MaxLength(100)]
	public string Name { get; set; } = string.Empty;

	// Stored as int (see MoodCategory enum)
	[Indexed(Name = "IX_Moods_Category")]
	public int Category { get; set; }

	public bool IsPredefined { get; set; }
}


