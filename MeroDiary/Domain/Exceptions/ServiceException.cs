namespace MeroDiary.Domain.Exceptions;

public sealed class ServiceException : Exception
{
	public ServiceException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}


