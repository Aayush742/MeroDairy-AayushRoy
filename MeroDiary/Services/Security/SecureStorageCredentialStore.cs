using Microsoft.Maui.Storage;
using Microsoft.Extensions.Logging;

namespace MeroDiary.Services.Security;

public sealed class SecureStorageCredentialStore : ICredentialStore
{
	private const string Key = "app_lock_hash_v1";
	private const string ProbeKey = "app_lock_probe";

	private readonly ILogger<SecureStorageCredentialStore> _logger;

	public SecureStorageCredentialStore(ILogger<SecureStorageCredentialStore> logger)
	{
		_logger = logger;
	}

	public string StoreName => "SecureStorage (Keychain/Keystore)";

	public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		try
		{
			// Probe needs to test WRITE: on Apple platforms SecureStorage can read but fail on write with MissingEntitlement.
			await SecureStorage.SetAsync(ProbeKey, "1").ConfigureAwait(false);
			SecureStorage.Remove(ProbeKey);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "SecureStorage is unavailable.");
			return false;
		}
	}

	public Task<string?> GetAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return SecureStorage.GetAsync(Key);
	}

	public Task SetAsync(string value, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return SecureStorage.SetAsync(Key, value);
	}

	public Task RemoveAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		SecureStorage.Remove(Key);
		return Task.CompletedTask;
	}
}


