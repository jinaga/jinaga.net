#nullable enable

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Jinaga.Test.Fakes
{
    /// <summary>
    /// One entry a logger was asked to write.
    /// </summary>
    public record LogEntry(LogLevel Level, string Message, Exception? Exception);

    /// <summary>
    /// A logger factory that keeps what was logged, so a test can assert that a
    /// condition reached the client's <see cref="ILoggerFactory"/> rather than
    /// only that it was swallowed.
    /// </summary>
    public class RecordingLoggerFactory : ILoggerFactory
    {
        private readonly List<LogEntry> entries = new List<LogEntry>();

        public ImmutableList<LogEntry> Entries
        {
            get
            {
                lock (entries)
                {
                    return entries.ToImmutableList();
                }
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new RecordingLogger(this);
        }

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        private void Record(LogEntry entry)
        {
            lock (entries)
            {
                entries.Add(entry);
            }
        }

        private class RecordingLogger : ILogger
        {
            private readonly RecordingLoggerFactory factory;

            public RecordingLogger(RecordingLoggerFactory factory)
            {
                this.factory = factory;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return new NullScope();
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                factory.Record(new LogEntry(logLevel, formatter(state, exception), exception));
            }

            private class NullScope : IDisposable
            {
                public void Dispose()
                {
                }
            }
        }
    }
}
