namespace MeroDiary.Services.Security;

public interface IPasswordHasher
{
	PasswordHash Hash(string secret);
	bool Verify(string secret, PasswordHash stored);
	string Serialize(PasswordHash hash);
	PasswordHash Deserialize(string data);
}


