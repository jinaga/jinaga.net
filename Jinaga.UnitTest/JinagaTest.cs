#nullable enable

using Jinaga.Authorization;
using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Serialization;
using Jinaga.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Jinaga.UnitTest
{
    public class JinagaTestOptions
    {
        public User? User { get; set; }

        /// <summary>
        /// Authorization rules to enforce locally.
        ///
        /// Without this the test client accepts every fact, so a rule can only be inspected as
        /// text and never observed refusing anything. With it, <c>await j.Fact(...)</c> throws
        /// <see cref="AuthorizationException"/> when the rules do not permit <see cref="User"/>
        /// to author the fact — the same decision a replicator makes.
        ///
        /// Rules that name a user are evaluated against <see cref="User"/>, so setting rules
        /// without a user asserts that nobody may author anything.
        /// </summary>
        public Func<AuthorizationRules, AuthorizationRules>? Authorization { get; set; }

        /// <summary>
        /// Facts that already exist, saved without being authorized.
        ///
        /// A test client has its own store, so facts another client created are not there —
        /// and a rule that asks whether something exists would find nothing. Seeding them here
        /// says "this had already happened", which is what a real client would have learned by
        /// syncing rather than by authoring.
        ///
        /// These bypass authorization deliberately. They are premises, not actions: a test
        /// about what a guest blogger may do should not first have to prove the invitation was
        /// legitimate.
        /// </summary>
        public ImmutableList<object> InitialState { get; set; } = ImmutableList<object>.Empty;
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
            var store = new MemoryStore();

            if (!testOptions.InitialState.IsEmpty)
            {
                // Saved straight to the store, as though synced: neither queued for the network
                // nor subject to the policy.
                var collector = new Collector(SerializerCache.Empty, new ConditionalWeakTable<object, FactGraph>());
                foreach (var fact in testOptions.InitialState)
                {
                    collector.Serialize(fact);
                }
                store.Save(collector.Graph, false, CancellationToken.None).GetAwaiter().GetResult();
            }

            if (testOptions.Authorization == null)
            {
                return new JinagaClient(store, network, ImmutableList<Specification>.Empty, loggerFactory, clientOptions);
            }

            var rules = AuthorizationRules.Build(testOptions.Authorization);
            var engine = new AuthorizationEngine(rules, store);

            return new JinagaClient(
                store,
                network,
                ImmutableList<Specification>.Empty,
                loggerFactory,
                clientOptions,
                engine,
                testOptions.User);
        }
    }
}
