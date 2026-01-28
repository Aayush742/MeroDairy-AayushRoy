using Microsoft.Maui.Storage;

namespace MeroDiary.Services.Theme;

public sealed class ThemeService : IThemeService
{
	private const string PreferenceKey = "merodiary.theme.mode";
	private AppThemeMode _currentTheme = AppThemeMode.Light;
	private int _initialized;

	public AppThemeMode CurrentTheme => _currentTheme;

	public event EventHandler? ThemeChanged;

	public Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (Interlocked.Exchange(ref _initialized, 1) == 1)
			return Task.CompletedTask;

		try
		{
			var raw = Preferences.Default.Get(PreferenceKey, AppThemeMode.Light.ToString());
			if (!Enum.TryParse<AppThemeMode>(raw, ignoreCase: true, out var mode))
				mode = AppThemeMode.Light;

			_currentTheme = mode;
		}
		catch
		{
			// If Preferences fails for some reason, fall back to Light.
			_currentTheme = AppThemeMode.Light;
		}

		return Task.CompletedTask;
	}

	public Task SetThemeAsync(AppThemeMode theme, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (_currentTheme == theme)
			return Task.CompletedTask;

		_currentTheme = theme;
		Preferences.Default.Set(PreferenceKey, theme.ToString());
		ThemeChanged?.Invoke(this, EventArgs.Empty);
		return Task.CompletedTask;
	}
}


