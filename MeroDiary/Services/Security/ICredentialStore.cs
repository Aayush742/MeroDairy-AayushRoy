namespace MeroDiary.Services.Security;

public interface ICredentialStore
{
	/// <summary>
	/// True if the underlying store is available on this device/runtime.
	/// </summary>
	Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

	Task<string?> GetAsync(CancellationToken cancellationToken = default);
	Task SetAsync(string value, CancellationToken cancellationToken = default);
	Task RemoveAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Name of the backing store (for diagnostics/UI).
	/// </summary>
	string StoreName { get; }
}


