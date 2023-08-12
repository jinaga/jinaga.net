using Jinaga.DefaultImplementations;
using Jinaga.Http;
using Jinaga.Services;
using Jinaga.Storage;
using System;

namespace Jinaga.Store.SQLite
{
    public class JinagaSQLiteClientOptions
    {
        /// <summary>
        /// http://localhost:8080/jinaga/
        /// </summary>
        public static Uri DefaultReplicatorEndpoint = new Uri("http://localhost:8080/jinaga/");

        /// <summary>
        /// The endpoint of the Jinaga server, or null for local operation.
        /// </summary>
        public Uri HttpEndpoint { get; set; }

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
        public static Jinaga Create()
        {
            return Create(_ => { });
        }

        /// <summary>
        /// Creates a Jinaga client using the provided configuration.
        /// </summary>
        /// <param name="configure">Lambda that sets properties on the options object.</param>
        /// <returns>A Jinaga client</returns>
        public static Jinaga Create(Action<JinagaSQLiteClientOptions> configure)
        {
            var options = new JinagaSQLiteClientOptions();
            configure(options);
            IStore store = options.SQLitePath == null
                ? (IStore)new MemoryStore()
                : new SQLiteStore(options.SQLitePath);
            INetwork network = options.HttpEndpoint == null
                ? (INetwork)new LocalNetwork()
                : new HttpNetwork(options.HttpEndpoint);
            return new Jinaga(store, network);
        }
    }
}
