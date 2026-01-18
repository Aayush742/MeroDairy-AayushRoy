using SQLite;

namespace MeroDiary.Data.Entities;

[Table("Tags")]
public sealed class TagEntity
{
	[PrimaryKey]
	public string Id { get; set; } = string.Empty;

	[MaxLength(100)]
	public string Name { get; set; } = string.Empty;

	// Case-insensitive uniqueness: store normalized lower-invariant form.
	[Indexed(Name = "IX_Tags_NormalizedName", Unique = true)]
	[MaxLength(100)]
	public string NormalizedName { get; set; } = string.Empty;

	public bool IsPredefined { get; set; }

	public DateTime CreatedAtUtc { get; set; }
	public DateTime UpdatedAtUtc { get; set; }
}


