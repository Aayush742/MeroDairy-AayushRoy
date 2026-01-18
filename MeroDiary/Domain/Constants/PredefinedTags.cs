namespace MeroDiary.Domain.Constants;

public static class PredefinedTags
{
	// Update these to match your coursework’s predefined list if you have one.
	public static readonly IReadOnlyList<(Guid Id, string Name)> All = new List<(Guid, string)>
	{
		(new Guid("66666666-6666-6666-6666-666666666666"), "Routine"),
		(new Guid("77777777-7777-7777-7777-777777777777"), "Milestone"),
		(new Guid("88888888-8888-8888-8888-888888888888"), "Insight"),
		(new Guid("99999999-9999-9999-9999-999999999999"), "Goal"),
	};
}


