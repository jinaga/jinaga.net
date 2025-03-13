﻿using Jinaga.DefaultImplementations;
using Jinaga.Http;
using Jinaga.Projections;
using Jinaga.Services;
using Jinaga.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Immutable;

namespace Jinaga.Store.SQLite
{
    public class JinagaSQLiteClientOptions : JinagaClientOptions
    {
        /// <summary>
        /// The path to the SQLite database, or null for in-memory operation.
        /// </summary>
        public string SQLitePath { get; set; }
    }

    public static class JinagaSQLiteClient
    {
        /// <summary>
        /// Creates a Jinaga client with no persistent storage or network connection.
        /// </summary>
        /// <returns>A Jinaga client</returns>
        public static JinagaClient Create()
        {
            return Create(_ => { });
        }

        /// <summary>
        /// Creates a Jinaga client using the provided configuration.
        /// </summary>
        /// <param name="configure">Lambda that sets properties on the options object.</param>
        /// <returns>A Jinaga client</returns>
        public static JinagaClient Create(Action<JinagaSQLiteClientOptions> configure)
        {
            var options = new JinagaSQLiteClientOptions();
            configure(options);
            var loggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;
            IStore store = options.SQLitePath == null
                ? (IStore)new MemoryStore()
                : new SQLiteStore(options.SQLitePath, loggerFactory);
            INetwork network = options.HttpEndpoint == null
                ? (INetwork)new LocalNetwork()
                : new HttpNetwork(options.HttpEndpoint, options.HttpAuthenticationProvider, loggerFactory, options.RetryConfiguration);
            var purgeConditions = CreatePurgeConditions(options);
            return new JinagaClient(store, network, purgeConditions, loggerFactory, options);
        }

        private static ImmutableList<Specification> CreatePurgeConditions(JinagaClientOptions options)
        {
            if (options.PurgeConditions == null)
            {
                return ImmutableList<Specification>.Empty;
            }
            else
            {
                var purgeConditions = options.PurgeConditions(PurgeConditions.Empty);
                return purgeConditions.Validate();
            }
        }
    }
}
