using SQLite;

namespace MeroDiary.Data.Sqlite;

public sealed class SqliteConnectionProvider : ISqliteConnectionProvider
{
	public SQLiteAsyncConnection Connection { get; }

	public SqliteConnectionProvider()
	{
		// Required for SQLitePCLRaw providers on some platforms (iOS/Android).
		SQLitePCL.Batteries_V2.Init();

		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "merodiary.db3");

		Connection = new SQLiteAsyncConnection(
			dbPath,
			SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
	}
}


