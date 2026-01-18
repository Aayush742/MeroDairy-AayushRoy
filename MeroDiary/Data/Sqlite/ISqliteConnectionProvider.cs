using SQLite;

namespace MeroDiary.Data.Sqlite;

public interface ISqliteConnectionProvider
{
	SQLiteAsyncConnection Connection { get; }
}


