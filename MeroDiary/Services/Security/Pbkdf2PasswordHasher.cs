using System.Security.Cryptography;

namespace MeroDiary.Services.Security;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
	private const int CurrentVersion = 1;
	private const int SaltSizeBytes = 16;
	private const int HashSizeBytes = 32; // 256-bit
	private const int DefaultIterations = 200_000;

	public PasswordHash Hash(string secret)
	{
		if (string.IsNullOrEmpty(secret))
			throw new ArgumentException("Secret is required.", nameof(secret));

		var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
		var hash = Rfc2898DeriveBytes.Pbkdf2(
			password: secret,
			salt: salt,
			iterations: DefaultIterations,
			hashAlgorithm: HashAlgorithmName.SHA256,
			outputLength: HashSizeBytes);

		return new PasswordHash
		{
			Version = CurrentVersion,
			Iterations = DefaultIterations,
			Salt = salt,
			Hash = hash,
		};
	}

	public bool Verify(string secret, PasswordHash stored)
	{
		if (string.IsNullOrEmpty(secret))
			return false;
		if (stored.Salt.Length == 0 || stored.Hash.Length == 0 || stored.Iterations <= 0)
			return false;

		var computed = Rfc2898DeriveBytes.Pbkdf2(
			password: secret,
			salt: stored.Salt,
			iterations: stored.Iterations,
			hashAlgorithm: HashAlgorithmName.SHA256,
			outputLength: stored.Hash.Length);

		return CryptographicOperations.FixedTimeEquals(computed, stored.Hash);
	}

	// Format: v1|iterations|saltB64|hashB64
	public string Serialize(PasswordHash hash)
	{
		return $"v{hash.Version}|{hash.Iterations}|{Convert.ToBase64String(hash.Salt)}|{Convert.ToBase64String(hash.Hash)}";
	}

	public PasswordHash Deserialize(string data)
	{
		if (string.IsNullOrWhiteSpace(data))
			throw new FormatException("Missing hash data.");

		var parts = data.Split('|');
		if (parts.Length != 4)
			throw new FormatException("Invalid hash format.");

		var verPart = parts[0];
		if (!verPart.StartsWith("v", StringComparison.OrdinalIgnoreCase) || !int.TryParse(verPart[1..], out var version))
			throw new FormatException("Invalid hash version.");

		if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
			throw new FormatException("Invalid iteration count.");

		var salt = Convert.FromBase64String(parts[2]);
		var hashBytes = Convert.FromBase64String(parts[3]);

		return new PasswordHash
		{
			Version = version,
			Iterations = iterations,
			Salt = salt,
			Hash = hashBytes,
		};
	}
}


