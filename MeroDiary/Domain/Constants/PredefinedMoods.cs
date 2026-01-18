using MeroDiary.Domain.Enums;

namespace MeroDiary.Domain.Constants;

/// <summary>
/// Predefined mood list (coursework): update names/ids here if your coursework list differs.
/// Stable IDs are used for seeding and analytics.
/// </summary>
public static class PredefinedMoods
{
	public static readonly IReadOnlyList<(Guid Id, string Name, MoodCategory Category)> All = new List<(Guid, string, MoodCategory)>
	{
		// Positive
		(new Guid("a1111111-1111-1111-1111-111111111111"), "Happy", MoodCategory.Positive),
		(new Guid("a2222222-2222-2222-2222-222222222222"), "Excited", MoodCategory.Positive),
		(new Guid("a3333333-3333-3333-3333-333333333333"), "Grateful", MoodCategory.Positive),
		(new Guid("a4444444-4444-4444-4444-444444444444"), "Calm", MoodCategory.Positive),

		// Neutral
		(new Guid("b1111111-1111-1111-1111-111111111111"), "Okay", MoodCategory.Neutral),
		(new Guid("b2222222-2222-2222-2222-222222222222"), "Tired", MoodCategory.Neutral),
		(new Guid("b3333333-3333-3333-3333-333333333333"), "Bored", MoodCategory.Neutral),

		// Negative
		(new Guid("c1111111-1111-1111-1111-111111111111"), "Sad", MoodCategory.Negative),
		(new Guid("c2222222-2222-2222-2222-222222222222"), "Anxious", MoodCategory.Negative),
		(new Guid("c3333333-3333-3333-3333-333333333333"), "Stressed", MoodCategory.Negative),
		(new Guid("c4444444-4444-4444-4444-444444444444"), "Angry", MoodCategory.Negative),
	};

	public static Guid DefaultPrimaryId => new Guid("b1111111-1111-1111-1111-111111111111"); // Okay
}


