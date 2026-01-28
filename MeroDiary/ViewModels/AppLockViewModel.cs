using MeroDiary.Services.Security;
using Microsoft.Extensions.Logging;

namespace MeroDiary.ViewModels;

public sealed class AppLockViewModel : ViewModelBase
{
	private readonly IAppLockService _lock;
	private readonly ILogger<AppLockViewModel> _logger;

	private string _secret = string.Empty;
	private string _confirmSecret = string.Empty;
	private string? _message;

	public AppLockViewModel(IAppLockService @lock, ILogger<AppLockViewModel> logger)
	{
		_lock = @lock;
		_logger = logger;
		_lock.StateChanged += (_, _) =>
		{
			OnPropertyChanged(nameof(State));
			OnPropertyChanged(nameof(IsConfigured));
			OnPropertyChanged(nameof(IsLocked));
		};
	}

	public AppLockState State => _lock.State;
	public bool IsConfigured => State.IsConfigured;
	public bool IsLocked => State.IsLocked;

	public string Secret
	{
		get => _secret;
		set => SetProperty(ref _secret, value);
	}

	public string ConfirmSecret
	{
		get => _confirmSecret;
		set => SetProperty(ref _confirmSecret, value);
	}

	public string? Message
	{
		get => _message;
		private set => SetProperty(ref _message, value);
	}

	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		await _lock.InitializeAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task SetupAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			Message = null;
			var s = (Secret ?? string.Empty).Trim();
			var c = (ConfirmSecret ?? string.Empty).Trim();
			if (s != c)
				throw new InvalidOperationException("PIN/password confirmation does not match.");

			await _lock.ConfigureAsync(s, cancellationToken).ConfigureAwait(false);
			Secret = string.Empty;
			ConfirmSecret = string.Empty;
			Message = "PIN/password set. Please unlock to continue.";
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogWarning(ex, "Setup failed.");
			Message = NormalizeSecureStorageError(ex);
		}
	}

	public async Task UnlockAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			Message = null;
			var ok = await _lock.TryUnlockAsync((Secret ?? string.Empty).Trim(), cancellationToken).ConfigureAwait(false);
			Secret = string.Empty;
			if (!ok)
				Message = "Invalid PIN/password.";
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogWarning(ex, "Unlock failed.");
			Message = NormalizeSecureStorageError(ex);
		}
	}

	private static string NormalizeSecureStorageError(Exception ex)
	{
		// Common Apple platform error when keychain entitlements are missing:
		// "Error adding record: MissingEntitlement"
		var msg = ex.Message ?? "Unexpected error.";
		if (msg.Contains("MissingEntitlement", StringComparison.OrdinalIgnoreCase))
		{
			return "Secure storage is not available (MissingEntitlement). Rebuild the app with Keychain entitlements enabled, then try again.";
		}

		return msg;
	}
}


