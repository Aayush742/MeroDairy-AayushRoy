using MeroDiary.Services.Theme;
using Microsoft.Extensions.Logging;

namespace MeroDiary.ViewModels;

public sealed class ThemeSettingsViewModel : ViewModelBase
{
	private readonly IThemeService _themeService;
	private readonly ILogger<ThemeSettingsViewModel> _logger;

	private bool _isBusy;
	private string? _errorMessage;
	private AppThemeMode _selectedTheme;

	public ThemeSettingsViewModel(IThemeService themeService, ILogger<ThemeSettingsViewModel> logger)
	{
		_themeService = themeService;
		_logger = logger;
		_selectedTheme = themeService.CurrentTheme;
	}

	public bool IsBusy
	{
		get => _isBusy;
		private set => SetProperty(ref _isBusy, value);
	}

	public string? ErrorMessage
	{
		get => _errorMessage;
		private set => SetProperty(ref _errorMessage, value);
	}

	public AppThemeMode SelectedTheme
	{
		get => _selectedTheme;
		set => SetProperty(ref _selectedTheme, value);
	}

	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		await _themeService.InitializeAsync(cancellationToken).ConfigureAwait(false);
		SelectedTheme = _themeService.CurrentTheme;
	}

	public async Task ApplyAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			ErrorMessage = null;
			IsBusy = true;
			await _themeService.SetThemeAsync(SelectedTheme, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Failed to apply theme.");
			ErrorMessage = ex.GetBaseException().Message;
		}
		finally
		{
			IsBusy = false;
		}
	}
}


