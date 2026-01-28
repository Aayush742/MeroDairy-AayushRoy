using Microsoft.Maui.Storage;

namespace MeroDiary.Services.Security;

/// <summary>
/// Fallback store when platform secure storage is unavailable.
/// Stores only the salted hash string (never plaintext).
/// </summary>
public sealed class PreferencesCredentialStore : ICredentialStore
{
	private const string Key = "app_lock_hash_v1_fallback";

	public string StoreName => "Preferences (fallback)";

	public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(true);
	}

	public Task<string?> GetAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var v = Preferences.Get(Key, string.Empty);
		return Task.FromResult<string?>(string.IsNullOrWhiteSpace(v) ? null : v);
	}

	public Task SetAsync(string value, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Preferences.Set(Key, value);
		return Task.CompletedTask;
	}

	public Task RemoveAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Preferences.Remove(Key);
		return Task.CompletedTask;
	}
}


