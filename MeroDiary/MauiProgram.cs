using Microsoft.Extensions.Logging;

namespace MeroDiary;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

		// Data / Services / ViewModels (MVVM-style state injected into Razor views)
		builder.Services.AddSingleton<Data.Sqlite.ISqliteConnectionProvider, Data.Sqlite.SqliteConnectionProvider>();
		builder.Services.AddSingleton<Data.Sqlite.IDatabaseInitializer, Data.Sqlite.DatabaseInitializer>();
		builder.Services.AddSingleton<Services.Markdown.IMarkdownRenderer, Services.Markdown.MarkdigMarkdownRenderer>();
		builder.Services.AddSingleton<Services.Security.IPasswordHasher, Services.Security.Pbkdf2PasswordHasher>();
		builder.Services.AddSingleton<Services.Security.SecureStorageCredentialStore>();
		builder.Services.AddSingleton<Services.Security.PreferencesCredentialStore>();
		builder.Services.AddSingleton<Services.Security.IAppLockService, Services.Security.AppLockService>();
		builder.Services.AddSingleton<Services.Theme.IThemeService, Services.Theme.ThemeService>();
		builder.Services.AddScoped<Data.Repositories.IJournalEntryRepository, Data.Repositories.JournalEntryRepository>();
		builder.Services.AddScoped<Data.Repositories.ICategoryRepository, Data.Repositories.CategoryRepository>();
		builder.Services.AddScoped<Data.Repositories.IMoodRepository, Data.Repositories.MoodRepository>();
		builder.Services.AddScoped<Data.Repositories.IJournalEntryMoodRepository, Data.Repositories.JournalEntryMoodRepository>();
		builder.Services.AddScoped<Data.Repositories.ITagRepository, Data.Repositories.TagRepository>();
		builder.Services.AddScoped<Data.Repositories.IJournalEntryTagRepository, Data.Repositories.JournalEntryTagRepository>();
		builder.Services.AddScoped<Services.IJournalEntryService, Services.JournalEntryService>();
		builder.Services.AddScoped<ViewModels.JournalEntriesViewModel>();
		builder.Services.AddScoped<ViewModels.JournalEntriesListViewModel>();
		builder.Services.AddScoped<ViewModels.EntryViewViewModel>();
		builder.Services.AddScoped<ViewModels.DashboardViewModel>();
		builder.Services.AddScoped<ViewModels.Calendar.CalendarViewModel>();
		builder.Services.AddScoped<Services.Streak.IStreakService, Services.Streak.StreakService>();
		builder.Services.AddScoped<Data.Repositories.IAnalyticsRepository, Data.Repositories.AnalyticsRepository>();
		builder.Services.AddScoped<Services.Analytics.IDashboardAnalyticsService, Services.Analytics.DashboardAnalyticsService>();
		builder.Services.AddScoped<ViewModels.AppLockViewModel>();
		builder.Services.AddScoped<ViewModels.ThemeSettingsViewModel>();

		// PDF export:
		// - QuestPDF uses native rendering and does NOT work on MacCatalyst/iOS/Android.
		// - Apple platforms use WKWebView HTML->PDF pipeline.
		// - Android currently returns a friendly "not supported" message.
#if WINDOWS
		builder.Services.AddScoped<Services.Export.IJournalPdfExportService, Services.Export.JournalPdfExportService>();
#elif IOS || MACCATALYST
		builder.Services.AddScoped<Services.Export.IJournalPdfExportService, Services.Export.JournalPdfExportWebKitService>();
#else
		builder.Services.AddScoped<Services.Export.IJournalPdfExportService, Services.Export.JournalPdfExportNotSupportedService>();
#endif
		builder.Services.AddScoped<ViewModels.PdfExportViewModel>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
