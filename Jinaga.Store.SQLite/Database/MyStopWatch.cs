using System;
using System.Diagnostics;

namespace Jinaga.Store.SQLite.Database
{
    public sealed class MyStopWatch
    {
        //TODO: Remove this class here and inject it, together with a Logger, to be used during unit-testing
        private static Stopwatch stopwatch;

        private MyStopWatch() { }


        public static string Start()
        {
            if (stopwatch == null)
            {
                stopwatch = new Stopwatch();
            }
            stopwatch.Restart();
            return $"{TimeSpan.Zero:ss\\ fff} ms";
        }

        public static string Elapsed()
        {
            return $"{stopwatch.Elapsed:ss\\ fff} ms";
        }

        public static long ElapsedMilliSeconds()
        {
            return stopwatch.ElapsedMilliseconds;
        }

    }
}
