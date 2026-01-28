namespace MeroDiary.Data.Sqlite;

public interface IDatabaseInitializer
{
	Task InitializeAsync(CancellationToken cancellationToken = default);
}


