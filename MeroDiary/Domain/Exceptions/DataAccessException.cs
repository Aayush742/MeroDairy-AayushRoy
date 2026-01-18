namespace MeroDiary.Domain.Exceptions;

public sealed class DataAccessException : Exception
{
	public DataAccessException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}


