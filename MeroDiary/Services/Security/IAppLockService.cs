namespace MeroDiary.Services.Security;

public interface IAppLockService
{
	AppLockState State { get; }
	event EventHandler? StateChanged;

	/// <summary>
	/// Loads persisted state and locks the app if credentials are configured.
	/// Call once on app startup.
	/// </summary>
	Task InitializeAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Configure a new PIN/password. Stores only a salted hash in SecureStorage.
	/// Locks the app after setup (caller may choose to unlock immediately).
	/// </summary>
	Task ConfigureAsync(string secret, CancellationToken cancellationToken = default);

	/// <summary>
	/// Attempts to unlock. Enforces retry limits + lockout.
	/// </summary>
	Task<bool> TryUnlockAsync(string secret, CancellationToken cancellationToken = default);

	/// <summary>
	/// Locks the app (e.g., on launch/resume or user action).
	/// </summary>
	Task LockAsync(CancellationToken cancellationToken = default);
}


