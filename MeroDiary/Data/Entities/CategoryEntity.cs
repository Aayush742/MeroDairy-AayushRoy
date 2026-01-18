using SQLite;

namespace MeroDiary.Data.Entities;

[Table("Categories")]
public sealed class CategoryEntity
{
	[PrimaryKey]
	public string Id { get; set; } = string.Empty;

	[Indexed(Name = "IX_Categories_Name", Unique = true)]
	[MaxLength(100)]
	public string Name { get; set; } = string.Empty;

	public bool IsPredefined { get; set; }

	public DateTime CreatedAtUtc { get; set; }
	public DateTime UpdatedAtUtc { get; set; }
}


