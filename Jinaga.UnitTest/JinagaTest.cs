using Jinaga.Projections;
using Jinaga.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Immutable;

namespace Jinaga.UnitTest
{
    public class JinagaTestOptions
    {
        public User User { get; set; }
    }

    public class JinagaTest
    {
        public static JinagaClient Create()
        {
            return Create(_ => { });
        }

        public static JinagaClient Create(Action<JinagaTestOptions> configure)
        {
            var options = new JinagaTestOptions();
            configure(options);
            var loggerFactory = NullLoggerFactory.Instance;
            var network = new SimulatedNetwork(
                options.User == null ? null : options.User.publicKey);
            var client = new JinagaClient(new MemoryStore(), network, ImmutableList<Specification>.Empty, loggerFactory);
            return client;
        }
    }
}