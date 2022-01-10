
using System;

using Sqlite3DatabaseHandle = SQLitePCL.sqlite3;
using Sqlite3BackupHandle = SQLitePCL.sqlite3_backup;
using Sqlite3Statement = SQLitePCL.sqlite3_stmt;
using Sqlite3 = SQLitePCL.raw;
using System.Reflection;

namespace Jinaga.Store.SQLite
{
    public static class SQLite
	{

		public enum ColType : int
		{
			Integer = 1,
			Float = 2,
			Text = 3,
			Blob = 4,
			Null = 5
		}

		public enum Result : int
		{
			OK = 0,
			Error = 1,
			Internal = 2,
			Perm = 3,
			Abort = 4,
			Busy = 5,
			Locked = 6,
			NoMem = 7,
			ReadOnly = 8,
			Interrupt = 9,
			IOError = 10,
			Corrupt = 11,
			NotFound = 12,
			Full = 13,
			CannotOpen = 14,
			LockErr = 15,
			Empty = 16,
			SchemaChngd = 17,
			TooBig = 18,
			Constraint = 19,
			Mismatch = 20,
			Misuse = 21,
			NotImplementedLFS = 22,
			AccessDenied = 23,
			Format = 24,
			Range = 25,
			NonDBFile = 26,
			Notice = 27,
			Warning = 28,
			Row = 100,
			Done = 101
		}

		public enum ExtendedResult : int
		{
			IOErrorRead = (Result.IOError | (1 << 8)),
			IOErrorShortRead = (Result.IOError | (2 << 8)),
			IOErrorWrite = (Result.IOError | (3 << 8)),
			IOErrorFsync = (Result.IOError | (4 << 8)),
			IOErrorDirFSync = (Result.IOError | (5 << 8)),
			IOErrorTruncate = (Result.IOError | (6 << 8)),
			IOErrorFStat = (Result.IOError | (7 << 8)),
			IOErrorUnlock = (Result.IOError | (8 << 8)),
			IOErrorRdlock = (Result.IOError | (9 << 8)),
			IOErrorDelete = (Result.IOError | (10 << 8)),
			IOErrorBlocked = (Result.IOError | (11 << 8)),
			IOErrorNoMem = (Result.IOError | (12 << 8)),
			IOErrorAccess = (Result.IOError | (13 << 8)),
			IOErrorCheckReservedLock = (Result.IOError | (14 << 8)),
			IOErrorLock = (Result.IOError | (15 << 8)),
			IOErrorClose = (Result.IOError | (16 << 8)),
			IOErrorDirClose = (Result.IOError | (17 << 8)),
			IOErrorSHMOpen = (Result.IOError | (18 << 8)),
			IOErrorSHMSize = (Result.IOError | (19 << 8)),
			IOErrorSHMLock = (Result.IOError | (20 << 8)),
			IOErrorSHMMap = (Result.IOError | (21 << 8)),
			IOErrorSeek = (Result.IOError | (22 << 8)),
			IOErrorDeleteNoEnt = (Result.IOError | (23 << 8)),
			IOErrorMMap = (Result.IOError | (24 << 8)),
			LockedSharedcache = (Result.Locked | (1 << 8)),			
			BusyRecovery = (Result.Busy | (1 << 8)),
			BusySnapshot = (Result.Busy | (2 << 8)),
			BusyTimeout = (Result.Busy | (3 << 8)),
			CannottOpenNoTempDir = (Result.CannotOpen | (1 << 8)),
			CannotOpenIsDir = (Result.CannotOpen | (2 << 8)),
			CannotOpenFullPath = (Result.CannotOpen | (3 << 8)),
			CorruptVTab = (Result.Corrupt | (1 << 8)),
			ReadonlyRecovery = (Result.ReadOnly | (1 << 8)),
			ReadonlyCannotLock = (Result.ReadOnly | (2 << 8)),
			ReadonlyRollback = (Result.ReadOnly | (3 << 8)),
			AbortRollback = (Result.Abort | (2 << 8)),
			ConstraintCheck = (Result.Constraint | (1 << 8)),
			ConstraintCommitHook = (Result.Constraint | (2 << 8)),
			ConstraintForeignKey = (Result.Constraint | (3 << 8)),
			ConstraintFunction = (Result.Constraint | (4 << 8)),
			ConstraintNotNull = (Result.Constraint | (5 << 8)),
			ConstraintPrimaryKey = (Result.Constraint | (6 << 8)),
			ConstraintTrigger = (Result.Constraint | (7 << 8)),
			ConstraintUnique = (Result.Constraint | (8 << 8)),
			ConstraintVTab = (Result.Constraint | (9 << 8)),
			NoticeRecoverWAL = (Result.Notice | (1 << 8)),
			NoticeRecoverRollback = (Result.Notice | (2 << 8))
		}

		public enum ConfigOption : int
		{
			SingleThread = 1,
			MultiThread = 2,
			Serialized = 3
		}

		const string LibraryPath = "sqlite3";


 #region JVN

        public static void InitLib()
		{
			SQLitePCL.Batteries_V2.Init();
		}


		public static T row<T>(this SQLitePCL.sqlite3_stmt stmt) where T : new()
		{
			Type typ = typeof(T);
			var obj = new T();
			for (int i = 0; i < ColumnCount(stmt); i++)
			{
				string colname = ColumnName(stmt,i);
				var prop = typ.GetTypeInfo().GetDeclaredProperty(colname);

				if (
						(null != prop)
						&& prop.CanWrite
						)
				{
					prop.SetValue(obj, stmt.column(i, prop.PropertyType), null);
				}
				else
				{
					throw new NotSupportedException("property not found");
				}

			}
			return obj;
		}

		public static object column(this SQLitePCL.sqlite3_stmt stmt, int index, Type t)
		{
			if (typeof(String) == t)
			{
				return ColumnText(stmt, index);
			}
			else if (
					   (typeof(Int32) == t)
					|| (typeof(Boolean) == t)
					|| (typeof(Byte) == t)
					|| (typeof(UInt16) == t)
					|| (typeof(Int16) == t)
					|| (typeof(sbyte) == t)
					)
			{
				return Convert.ChangeType(ColumnInt(stmt, index), t, null);
			}
			//else if (
			//		   (typeof(double) == t)
			//		|| (typeof(float) == t)
			//		)
			//{
			//	return Convert.ChangeType(stmt.column_double(index), t, null);
			//}
			//else if (typeof(DateTime) == t)
			//{
			//	DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
			//	return origin.AddSeconds(stmt.column_int64(index));
			//}
			//else if (
			//		   (typeof(Int64) == t)
			//		|| (typeof(UInt32) == t)
			//		)
			//{
			//	return Convert.ChangeType(stmt.column_int64(index), t, null);
			//}
			//else if (typeof(System.Nullable<long>) == t)
			//{
			//	if (stmt.column_type(index) == raw.SQLITE_NULL)
			//	{
			//		return null;
			//	}
			//	else
			//	{
			//		long? x = stmt.column_int64(index);
			//		return x;
			//	}
			//}
			//else if (typeof(System.Nullable<double>) == t)
			//{
			//	if (stmt.column_type(index) == raw.SQLITE_NULL)
			//	{
			//		return null;
			//	}
			//	else
			//	{
			//		double? x = stmt.column_double(index);
			//		return x;
			//	}
			//}
			//else if (typeof(System.Nullable<int>) == t)
			//{
			//	if (stmt.column_type(index) == raw.SQLITE_NULL)
			//	{
			//		return null;
			//	}
			//	else
			//	{
			//		int? x = stmt.column_int(index);
			//		return x;
			//	}
			//}
			//else if (typeof(byte[]) == t)
			//{
			//	// TODO hmmm.  how should this function adapt to Span/Memory ?
			//	// need a way to ask for ReadOnlySpan<byte> ?
			//	if (stmt.column_type(index) == raw.SQLITE_NULL)
			//	{
			//		return null;
			//	}
			//	else
			//	{
			//		return stmt.column_blob(index).ToArray();
			//	}
			//}
			else
			{
				throw new NotSupportedException("Invalid type conversion" + t);
			}
		}

		
		#endregion


		public static Result Open (string filename, out Sqlite3DatabaseHandle db)
		{
			return (Result)Sqlite3.sqlite3_open (filename, out db);
		}

		public static Result Open (string filename, out Sqlite3DatabaseHandle db, int flags, string vfsName)
		{
			return (Result)Sqlite3.sqlite3_open_v2 (filename, out db, flags, vfsName);
		}

		public static Result Close (Sqlite3DatabaseHandle db)
		{
			return (Result)Sqlite3.sqlite3_close (db);
		}

		public static Result Close2 (Sqlite3DatabaseHandle db)
		{
			return (Result)Sqlite3.sqlite3_close_v2 (db);
		}

		public static Result BusyTimeout (Sqlite3DatabaseHandle db, int milliseconds)
		{
			return (Result)Sqlite3.sqlite3_busy_timeout (db, milliseconds);
		}

		public static int Changes (Sqlite3DatabaseHandle db)
		{
			return Sqlite3.sqlite3_changes (db);
		}

		public static Sqlite3Statement Prepare2 (Sqlite3DatabaseHandle db, string query)
		{
			Sqlite3Statement stmt = default (Sqlite3Statement);
			var r = Sqlite3.sqlite3_prepare_v2 (db, query, out stmt);

			if (r != 0) {
				//throw SQLiteException.New ((Result)r, GetErrmsg (db));
				throw SQLiteException.New((Result)r, $"Prepare2 - Result: {r.ToString()} - ExtendedErrCode: {ExtendedErrCode(db)} - ErrorMsg: {GetErrmsg(db)}" );
			}
			return stmt;
		}

		public static Result Step (Sqlite3Statement stmt)
		{
			return (Result)Sqlite3.sqlite3_step (stmt);
		}

		public static Result Reset (Sqlite3Statement stmt)
		{
			return (Result)Sqlite3.sqlite3_reset (stmt);
		}

		public static Result Finalize (Sqlite3Statement stmt)
		{
			return (Result)Sqlite3.sqlite3_finalize (stmt);
		}

		public static long LastInsertRowid (Sqlite3DatabaseHandle db)
		{
			return Sqlite3.sqlite3_last_insert_rowid (db);
		}

		public static string GetErrmsg (Sqlite3DatabaseHandle db)
		{
			return Sqlite3.sqlite3_errmsg (db).utf8_to_string ();
		}

		public static int BindParameterIndex (Sqlite3Statement stmt, string name)
		{
			return Sqlite3.sqlite3_bind_parameter_index (stmt, name);
		}

		public static int BindNull (Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_bind_null (stmt, index);
		}

		public static int BindInt (Sqlite3Statement stmt, int index, int val)
		{
			return Sqlite3.sqlite3_bind_int (stmt, index, val);
		}

		public static int BindInt64 (Sqlite3Statement stmt, int index, long val)
		{
			return Sqlite3.sqlite3_bind_int64 (stmt, index, val);
		}

		public static int BindDouble (Sqlite3Statement stmt, int index, double val)
		{
			return Sqlite3.sqlite3_bind_double (stmt, index, val);
		}

		public static int BindText (Sqlite3Statement stmt, int index, string val, int n, IntPtr free)
		{
			return Sqlite3.sqlite3_bind_text (stmt, index, val);
		}

		public static int BindBlob (Sqlite3Statement stmt, int index, byte[] val, int n, IntPtr free)
		{
			return Sqlite3.sqlite3_bind_blob (stmt, index, val);
		}

		public static int ColumnCount (Sqlite3Statement stmt)
		{
			return Sqlite3.sqlite3_column_count (stmt);
		}

		public static string ColumnName (Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_column_name (stmt, index).utf8_to_string ();
		}

		public static string ColumnName16 (Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_column_name (stmt, index).utf8_to_string ();
		}

		public static ColType ColumnType (Sqlite3Statement stmt, int index)
		{
			return (ColType)Sqlite3.sqlite3_column_type (stmt, index);
		}

		public static int ColumnInt (Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_column_int (stmt, index);
		}

		public static long ColumnInt64 (Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_column_int64 (stmt, index);
		}

		public static double ColumnDouble (Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_column_double (stmt, index);
		}

		public static string ColumnText (Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_column_text (stmt, index).utf8_to_string ();
		}

		public static string ColumnText16 (Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_column_text (stmt, index).utf8_to_string ();
		}

		public static byte[] ColumnBlob (Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_column_blob (stmt, index).ToArray ();
		}

		public static int ColumnBytes (Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_column_bytes (stmt, index);
		}

		public static string ColumnString (Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_column_text (stmt, index).utf8_to_string ();
		}

		public static byte[] ColumnByteArray (Sqlite3Statement stmt, int index)
		{
			int length = ColumnBytes (stmt, index);
			if (length > 0) {
				return ColumnBlob (stmt, index);
			}
			return new byte[0];
		}

		public static Result EnableLoadExtension (Sqlite3DatabaseHandle db, int onoff)
		{
			return (Result)Sqlite3.sqlite3_enable_load_extension (db, onoff);
		}

		public static int LibVersionNumber ()
		{
			return Sqlite3.sqlite3_libversion_number ();
		}

		public static Result GetResult (Sqlite3DatabaseHandle db)
		{
			return (Result)Sqlite3.sqlite3_errcode (db);
		}

		public static ExtendedResult ExtendedErrCode (Sqlite3DatabaseHandle db)
		{
			return (ExtendedResult)Sqlite3.sqlite3_extended_errcode (db);
		}

		public static Sqlite3BackupHandle BackupInit (Sqlite3DatabaseHandle destDb, string destName, Sqlite3DatabaseHandle sourceDb, string sourceName)
		{
			return Sqlite3.sqlite3_backup_init (destDb, destName, sourceDb, sourceName);
		}

		public static Result BackupStep (Sqlite3BackupHandle backup, int numPages)
		{
			return (Result)Sqlite3.sqlite3_backup_step (backup, numPages);
		}

		public static Result BackupFinish (Sqlite3BackupHandle backup)
		{
			return (Result)Sqlite3.sqlite3_backup_finish (backup);
		}
	
	}
}
