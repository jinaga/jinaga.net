using System;


namespace Jinaga.Store.SQLite
{
    public class SQLiteException : Exception
	{
		public SQLite.Result Result { get; private set; }

		protected SQLiteException(SQLite.Result r, string message) : base(message)
		{
			Result = r;
		}

		public static SQLiteException New(SQLite.Result r, string message)
		{
			return new SQLiteException(r, message);
		}
	}
}
