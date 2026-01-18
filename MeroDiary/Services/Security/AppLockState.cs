namespace MeroDiary.Services.Security;

public sealed class AppLockState
{
	public required bool IsConfigured { get; init; }
	public required bool IsLocked { get; init; }
	public required int RemainingAttempts { get; init; }
	public required DateTimeOffset? LockedUntilUtc { get; init; }
	public string? StorageError { get; init; }
}


