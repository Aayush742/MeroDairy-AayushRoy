namespace MeroDiary.Services.Security;

public sealed class PasswordHash
{
	public required int Version { get; init; }
	public required int Iterations { get; init; }
	public required byte[] Salt { get; init; }
	public required byte[] Hash { get; init; }
}


