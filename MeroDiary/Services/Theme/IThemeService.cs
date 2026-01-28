namespace MeroDiary.Services.Theme;

public interface IThemeService
{
	AppThemeMode CurrentTheme { get; }
	event EventHandler? ThemeChanged;

	Task InitializeAsync(CancellationToken cancellationToken = default);
	Task SetThemeAsync(AppThemeMode theme, CancellationToken cancellationToken = default);
}


