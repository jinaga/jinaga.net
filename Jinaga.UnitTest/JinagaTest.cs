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
            var testOptions = new JinagaTestOptions();
            configure(testOptions);
            var loggerFactory = NullLoggerFactory.Instance;
            var network = new SimulatedNetwork(
                testOptions.User == null ? null : testOptions.User.publicKey);
            var clientOptions = new JinagaClientOptions();
            var client = new JinagaClient(new MemoryStore(), network, ImmutableList<Specification>.Empty, loggerFactory, clientOptions);
            return client;
        }
    }
}
