using Microsoft.Maui.Storage;
using Microsoft.Extensions.Logging;

namespace MeroDiary.Services.Security;

public sealed class AppLockService : IAppLockService
{
	private const string FailedAttemptsKey = "app_lock_failed_attempts";
	private const string LockoutUntilUtcKey = "app_lock_lockout_until_utc";
	private const string IsLockedKey = "app_lock_is_locked";

	private const int MaxAttempts = 5;
	private static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(30);

	private readonly IPasswordHasher _hasher;
	private readonly SecureStorageCredentialStore _secureStore;
	private readonly PreferencesCredentialStore _fallbackStore;
	private readonly ILogger<AppLockService> _logger;
	private ICredentialStore _store;

	public event EventHandler? StateChanged;

	public AppLockState State { get; private set; } = new()
	{
		IsConfigured = false,
		IsLocked = true,
		RemainingAttempts = MaxAttempts,
		LockedUntilUtc = null,
		StorageError = null,
	};

	public AppLockService(
		IPasswordHasher hasher,
		SecureStorageCredentialStore secureStore,
		PreferencesCredentialStore fallbackStore,
		ILogger<AppLockService> logger)
	{
		_hasher = hasher;
		_secureStore = secureStore;
		_fallbackStore = fallbackStore;
		_store = fallbackStore;
		_logger = logger;
	}

	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		_store = await _secureStore.IsAvailableAsync(cancellationToken).ConfigureAwait(false)
			? _secureStore
			: _fallbackStore;

		var serialized = await _store.GetAsync(cancellationToken).ConfigureAwait(false);
		var isConfigured = !string.IsNullOrWhiteSpace(serialized);

		var failedAttempts = await GetIntAsync(FailedAttemptsKey).ConfigureAwait(false);
		var lockoutUntilUtc = await GetDateTimeOffsetAsync(LockoutUntilUtcKey).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		var lockedUntil = lockoutUntilUtc.HasValue && lockoutUntilUtc.Value > now ? lockoutUntilUtc : null;

		// Always lock on launch. If not configured yet, user will be prompted to set a PIN/password.
		var locked = true;
		await SetBoolAsync(IsLockedKey, locked).ConfigureAwait(false);

		State = new AppLockState
		{
			IsConfigured = isConfigured,
			IsLocked = locked,
			RemainingAttempts = ComputeRemainingAttempts(failedAttempts, lockedUntil),
			LockedUntilUtc = lockedUntil,
			StorageError = _store == _fallbackStore
				? "Secure storage is unavailable on this runtime; using fallback storage for the PIN hash."
				: null,
		};
		OnStateChanged();
	}

	public async Task ConfigureAsync(string secret, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		ValidateSecret(secret);

		var hash = _hasher.Hash(secret);
		var serialized = _hasher.Serialize(hash);

		try
		{
			await _store.SetAsync(serialized, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			// If SecureStorage failed (e.g., MissingEntitlement), transparently fall back so the app remains usable.
			_logger.LogWarning(ex, "Primary credential store failed. Falling back to Preferences.");
			_store = _fallbackStore;
			await _store.SetAsync(serialized, cancellationToken).ConfigureAwait(false);
		}

		await SetIntAsync(FailedAttemptsKey, 0).ConfigureAwait(false);
		await RemoveAsync(LockoutUntilUtcKey).ConfigureAwait(false);

		// Lock after setup (consistent "lock on launch" behavior).
		await LockAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task<bool> TryUnlockAsync(string secret, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var serialized = await _store.GetAsync(cancellationToken).ConfigureAwait(false);
		if (string.IsNullOrWhiteSpace(serialized))
		{
			// Not configured yet.
			State = new AppLockState
			{
				IsConfigured = false,
				IsLocked = true,
				RemainingAttempts = MaxAttempts,
				LockedUntilUtc = null,
				StorageError = _store == _fallbackStore
					? "Secure storage is unavailable on this runtime; using fallback storage for the PIN hash."
					: null,
			};
			OnStateChanged();
			return false;
		}

		var now = DateTimeOffset.UtcNow;
		var lockedUntil = await GetDateTimeOffsetAsync(LockoutUntilUtcKey).ConfigureAwait(false);
		if (lockedUntil.HasValue && lockedUntil.Value > now)
		{
			State = new AppLockState
			{
				IsConfigured = true,
				IsLocked = true,
				RemainingAttempts = 0,
				LockedUntilUtc = lockedUntil,
				StorageError = _store == _fallbackStore
					? "Secure storage is unavailable on this runtime; using fallback storage for the PIN hash."
					: null,
			};
			OnStateChanged();
			return false;
		}

		PasswordHash stored;
		try
		{
			stored = _hasher.Deserialize(serialized);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to parse stored credential hash.");
			// Fail closed: force lock.
			await LockAsync(cancellationToken).ConfigureAwait(false);
			return false;
		}

		if (_hasher.Verify(secret, stored))
		{
			await SetIntAsync(FailedAttemptsKey, 0).ConfigureAwait(false);
			await RemoveAsync(LockoutUntilUtcKey).ConfigureAwait(false);
			await SetBoolAsync(IsLockedKey, false).ConfigureAwait(false);

			State = new AppLockState
			{
				IsConfigured = true,
				IsLocked = false,
				RemainingAttempts = MaxAttempts,
				LockedUntilUtc = null,
				StorageError = _store == _fallbackStore
					? "Secure storage is unavailable on this runtime; using fallback storage for the PIN hash."
					: null,
			};
			OnStateChanged();
			return true;
		}

		// Failed attempt
		var failedAttempts = await GetIntAsync(FailedAttemptsKey).ConfigureAwait(false);
		failedAttempts++;
		await SetIntAsync(FailedAttemptsKey, failedAttempts).ConfigureAwait(false);

		DateTimeOffset? newLockoutUntil = null;
		if (failedAttempts >= MaxAttempts)
		{
			newLockoutUntil = now.Add(LockoutDuration);
			await SetDateTimeOffsetAsync(LockoutUntilUtcKey, newLockoutUntil.Value).ConfigureAwait(false);
			await SetIntAsync(FailedAttemptsKey, 0).ConfigureAwait(false); // reset for next window
		}

		State = new AppLockState
		{
			IsConfigured = true,
			IsLocked = true,
			RemainingAttempts = ComputeRemainingAttempts(failedAttempts, newLockoutUntil),
			LockedUntilUtc = newLockoutUntil,
			StorageError = _store == _fallbackStore
				? "Secure storage is unavailable on this runtime; using fallback storage for the PIN hash."
				: null,
		};
		OnStateChanged();
		return false;
	}

	public async Task LockAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var serialized = await _store.GetAsync(cancellationToken).ConfigureAwait(false);
		var isConfigured = !string.IsNullOrWhiteSpace(serialized);

		await SetBoolAsync(IsLockedKey, true).ConfigureAwait(false);

		var failedAttempts = await GetIntAsync(FailedAttemptsKey).ConfigureAwait(false);
		var lockedUntil = await GetDateTimeOffsetAsync(LockoutUntilUtcKey).ConfigureAwait(false);

		State = new AppLockState
		{
			IsConfigured = isConfigured,
			IsLocked = true,
			RemainingAttempts = ComputeRemainingAttempts(failedAttempts, lockedUntil),
			LockedUntilUtc = lockedUntil,
			StorageError = _store == _fallbackStore
				? "Secure storage is unavailable on this runtime; using fallback storage for the PIN hash."
				: null,
		};
		OnStateChanged();
	}

	private static int ComputeRemainingAttempts(int failedAttempts, DateTimeOffset? lockoutUntilUtc)
	{
		if (lockoutUntilUtc.HasValue && lockoutUntilUtc.Value > DateTimeOffset.UtcNow)
			return 0;

		var remaining = MaxAttempts - Math.Clamp(failedAttempts, 0, MaxAttempts);
		return remaining;
	}

	private static void ValidateSecret(string secret)
	{
		// PIN-only policy: exactly 4 digits.
		secret = secret?.Trim() ?? string.Empty;
		if (secret.Length != 4 || secret.Any(c => c < '0' || c > '9'))
			throw new ArgumentException("PIN must be exactly 4 digits.", nameof(secret));
	}

	private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

	// Secure storage errors are handled by selecting the fallback store.

	// We use Preferences for non-sensitive counters/timers. Credential hash is stored in SecureStorage.
	private static Task<int> GetIntAsync(string key)
		=> Task.FromResult(Preferences.Get(key, 0));

	private static Task SetIntAsync(string key, int value)
	{
		Preferences.Set(key, value);
		return Task.CompletedTask;
	}

	private static Task SetBoolAsync(string key, bool value)
	{
		Preferences.Set(key, value);
		return Task.CompletedTask;
	}

	private static Task<DateTimeOffset?> GetDateTimeOffsetAsync(string key)
	{
		var s = Preferences.Get(key, string.Empty);
		if (DateTimeOffset.TryParse(s, out var dto))
			return Task.FromResult<DateTimeOffset?>(dto);
		return Task.FromResult<DateTimeOffset?>(null);
	}

	private static Task SetDateTimeOffsetAsync(string key, DateTimeOffset value)
	{
		Preferences.Set(key, value.ToString("O"));
		return Task.CompletedTask;
	}

	private static Task RemoveAsync(string key)
	{
		Preferences.Remove(key);
		return Task.CompletedTask;
	}
}


