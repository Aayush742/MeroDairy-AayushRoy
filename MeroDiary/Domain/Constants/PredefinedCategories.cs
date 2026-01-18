namespace MeroDiary.Domain.Constants;

public static class PredefinedCategories
{
	public static readonly IReadOnlyList<(Guid Id, string Name)> All = new List<(Guid, string)>
	{
		(new Guid("11111111-1111-1111-1111-111111111111"), "Work"),
		(new Guid("22222222-2222-2222-2222-222222222222"), "Health"),
		(new Guid("33333333-3333-3333-3333-333333333333"), "Travel"),
		(new Guid("44444444-4444-4444-4444-444444444444"), "Personal Growth"),
		(new Guid("55555555-5555-5555-5555-555555555555"), "Reflection"),
	};

	public static Guid DefaultId => new Guid("55555555-5555-5555-5555-555555555555"); // Reflection
}


