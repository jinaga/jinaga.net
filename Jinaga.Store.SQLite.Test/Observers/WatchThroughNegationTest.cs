using Jinaga.Observers;
using Jinaga.Store.SQLite.Test.Fakes;
using Jinaga.Store.SQLite.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Jinaga.Store.SQLite.Test.Observers;

/// <summary>
/// A watch whose membership turns on a negation, against the SQLite store.
///
/// Deciding a candidate reads the specification with every unknown of the row pinned, which
/// is a shape the SQLite store had no reason to build before. Its own evaluator renders a
/// specification into SQL rather than walking it, so the shape has to be exercised here and
/// not only against the in-memory store.
/// </summary>
public class WatchThroughNegationTest
{
    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "WatchThroughNegationTest.db");

    private readonly ITestOutputHelper output;

    public WatchThroughNegationTest(ITestOutputHelper output)
    {
        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);

        this.output = output;
    }

    /// <summary>
    /// The offices of a company that have a manager nobody has terminated. The condition's
    /// own level gains and loses solutions, and the office's membership follows it.
    /// </summary>
    private static readonly Specification<Company, Office> OfficesWithACurrentManager =
        Given<Company>.Match((company, facts) =>
            from office in facts.OfType<Office>()
            where office.company == company
            where facts.Any<Manager>(manager =>
                manager.office == office &&
                !facts.Any<ManagerTerminated>(terminated => terminated.Manager == manager))
            select office
        );

    /// <summary>
    /// The open offices of a company in one city. Its one unknown is correlated with two
    /// known facts, each by walking forward from the office, so deciding a candidate has a
    /// path condition to check that neither the pin nor the anchor accounts for.
    /// </summary>
    private static readonly Specification<Company, City, Office> OpenOfficesInCity =
        Given<Company, City>.Match((company, city, facts) =>
            from office in facts.OfType<Office>()
            where office.company == company
            where office.city == city
            where !facts.Any<OfficeClosure>(closure => closure.office == office)
            select office
        );

    [Fact]
    public async Task Watch_TerminatingTheOnlyCurrentManager_RemovesTheOffice()
    {
        var j = GivenJinagaClient();
        var company = await j.Fact(new Company("contoso"));
        var dallas = await j.Fact(new City("Dallas"));
        var office = await j.Fact(new Office(company, dallas));
        var manager = await j.Fact(new Manager(office, 1));

        var offices = new List<Office>();
        var watch = j.Watch(OfficesWithACurrentManager, company, (Office added) =>
        {
            lock (offices)
            {
                offices.Add(added);
            }
            return () =>
            {
                lock (offices)
                {
                    offices.Remove(added);
                }
            };
        });

        try
        {
            await watch.Loaded;
            lock (offices)
            {
                offices.Should().ContainSingle();
            }

            await j.Fact(new ManagerTerminated(manager, DateTime.UtcNow));

            lock (offices)
            {
                offices.Should().BeEmpty();
            }
            (await j.Query(OfficesWithACurrentManager, company)).Should().BeEmpty();
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task Watch_TerminatingOneOfTwoCurrentManagers_KeepsTheOffice()
    {
        var j = GivenJinagaClient();
        var company = await j.Fact(new Company("contoso"));
        var dallas = await j.Fact(new City("Dallas"));
        var office = await j.Fact(new Office(company, dallas));
        var first = await j.Fact(new Manager(office, 1));
        await j.Fact(new Manager(office, 2));

        var offices = new List<Office>();
        var watch = j.Watch(OfficesWithACurrentManager, company, (Office added) =>
        {
            lock (offices)
            {
                offices.Add(added);
            }
            return () =>
            {
                lock (offices)
                {
                    offices.Remove(added);
                }
            };
        });

        try
        {
            await watch.Loaded;

            var save = async () => await j.Fact(new ManagerTerminated(first, DateTime.UtcNow));

            await save.Should().NotThrowAsync();
            lock (offices)
            {
                offices.Should().ContainSingle();
            }
            (await j.Query(OfficesWithACurrentManager, company)).Should().ContainSingle();
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task Watch_AMatchCorrelatedWithTwoKnownFacts_HoldsOnlyItsOwnRowsAndRemovesThem()
    {
        var j = GivenJinagaClient();
        var company = await j.Fact(new Company("contoso"));
        var dallas = await j.Fact(new City("Dallas"));
        var austin = await j.Fact(new City("Austin"));
        var dallasOffice = await j.Fact(new Office(company, dallas));
        await j.Fact(new Office(company, austin));

        var offices = new List<Office>();
        var removals = 0;
        var watch = j.Watch(OpenOfficesInCity, company, dallas, (Office added) =>
        {
            lock (offices)
            {
                offices.Add(added);
            }
            return () =>
            {
                lock (offices)
                {
                    offices.Remove(added);
                }
                Interlocked.Increment(ref removals);
            };
        });

        try
        {
            await watch.Loaded;
            lock (offices)
            {
                offices.Should().ContainSingle().Which.city.Should().Be(dallas);
            }

            // The Austin office is a candidate of this watch's own inverses -- one fact of the
            // right type in the right company -- and only the city tells the two apart.
            await j.Fact(new OfficeClosure(
                await j.Fact(new Office(company, austin)), DateTime.UtcNow));
            lock (offices)
            {
                offices.Should().ContainSingle();
            }
            removals.Should().Be(0);

            await j.Fact(new OfficeClosure(dallasOffice, DateTime.UtcNow));

            lock (offices)
            {
                offices.Should().BeEmpty();
            }
            removals.Should().Be(1);
            (await j.Query(OpenOfficesInCity, company, dallas)).Should().BeEmpty();
        }
        finally
        {
            watch.Stop();
        }
    }

    /// <summary>
    /// The chores of a tenant that no completion names, in either of a completion's two
    /// roles. The two conditions are siblings, so one fact can falsify both.
    /// </summary>
    private static readonly Specification<Tenant, Chore> ChoresNotDone =
        Given<Tenant>.Match((tenant, facts) =>
            from chore in facts.OfType<Chore>()
            where chore.tenant == tenant
            where !facts.Any<ChoreDone>(done => done.chore == chore)
            where !facts.Any<ChoreDone>(done => done.witness == chore)
            select chore
        );

    /// <summary>
    /// The chores of a tenant that no block holds up, where a block is held up in turn by an
    /// override, and an override counts only once a reason exists for it. Three levels of
    /// condition, so the innermost one becoming true adds the row.
    /// </summary>
    private static readonly Specification<Tenant, Chore> ChoresNotBlocked =
        Given<Tenant>.Match((tenant, facts) =>
            from chore in facts.OfType<Chore>()
            where chore.tenant == tenant
            where !facts.Any<Block>(block =>
                block.chore == chore &&
                !facts.Any<Override>(reversal =>
                    reversal.block == block &&
                    facts.Any<Reason>(reason => reason.reversal == reversal)))
            select chore
        );

    /// <summary>
    /// The messages of a channel that a user has not read. Its first given is joined only
    /// from inside the negated condition, so deciding a candidate pins an unknown whose
    /// conditions reach a given the outer query never joins.
    /// </summary>
    private static readonly Specification<User, Channel, Message> UnreadMessages =
        Given<User, Channel>.Match((user, channel, facts) =>
            from message in facts.OfType<Message>()
            where message.channel == channel
            where !facts.Any<Read>(read => read.message == message && read.user == user)
            select message
        );

    /// <summary>
    /// The accounts of a tenant, each with the mirrors nobody has retired. The collection is
    /// a specification of its own, decided by a read with the parent and the child both
    /// pinned.
    /// </summary>
    private static readonly Specification<Tenant, AccountWithMirrors> AccountsWithTheirCurrentMirrors =
        Given<Tenant>.Match((tenant, facts) =>
            from account in facts.OfType<Account>()
            where account.tenant == tenant
            select new AccountWithMirrors(
                account,
                facts.Observable(
                    from mirror in facts.OfType<Mirror>()
                    where mirror.account == account &&
                        !facts.Any<MirrorRetired>(retired => retired.mirror == mirror)
                    select mirror)
            )
        );

    private record AccountWithMirrors(Account Account, IObservableCollection<Mirror> Mirrors);

    [Fact]
    public async Task Watch_OneCompletionFalsifyingTwoSiblingConditions_RemovesTheChoreOnce()
    {
        var j = GivenJinagaClient();
        var tenant = await j.Fact(new Tenant("contoso"));
        var chore = await j.Fact(new Chore(tenant, "sweep"));

        var observed = new ObservedChores();
        var watch = observed.Watch(j, ChoresNotDone, tenant);

        try
        {
            await watch.Loaded;
            observed.AddedCalls.Should().Be(1);

            await j.Fact(new ChoreDone(chore, chore));

            observed.Chores.Should().BeEmpty();
            observed.AddedCalls.Should().Be(1);
            observed.RemovalCalls.Should().Be(1);
            (await j.Query(ChoresNotDone, tenant)).Should().BeEmpty();
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task Watch_TheInnermostConditionBecomingTrue_AddsTheChore()
    {
        var j = GivenJinagaClient();
        var tenant = await j.Fact(new Tenant("contoso"));
        await j.Fact(new Chore(tenant, "sweep"));
        var block = await j.Fact(new Block(new Chore(tenant, "sweep")));
        var reversal = await j.Fact(new Override(block));

        var observed = new ObservedChores();
        var watch = observed.Watch(j, ChoresNotBlocked, tenant);

        try
        {
            await watch.Loaded;
            // The override has no reason yet, so it does not count and the block still holds.
            observed.Chores.Should().BeEmpty();

            await j.Fact(new Reason(reversal));

            observed.Chores.Should().ContainSingle()
                .Which.description.Should().Be("sweep");
            observed.AddedCalls.Should().Be(1);
            (await j.Query(ChoresNotBlocked, tenant)).Should().ContainSingle();
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task Watch_WithAGivenJoinedOnlyInsideTheCondition_LoadsAndRemovesWhatIsRead()
    {
        var j = GivenJinagaClient();
        var user = await j.Fact(new User("---FAKE READER---"));
        var channel = await j.Fact(new Channel("general"));
        var message = await j.Fact(new Message(channel, "Where is everybody?"));

        var messages = new List<Message>();
        var removals = 0;
        var watch = j.Watch(UnreadMessages, user, channel, projection =>
        {
            lock (messages)
            {
                messages.Add(projection);
            }
            return () =>
            {
                lock (messages)
                {
                    messages.Remove(projection);
                }
                Interlocked.Increment(ref removals);
            };
        });

        try
        {
            await watch.Loaded;
            lock (messages)
            {
                messages.Should().ContainSingle();
            }

            await j.Fact(new Read(message, user));

            lock (messages)
            {
                messages.Should().BeEmpty();
            }
            removals.Should().Be(1);
            (await j.Query(UnreadMessages, user, channel)).Should().BeEmpty();
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task Watch_RetiringAMirrorOfAChildCollection_RemovesTheChild()
    {
        var j = GivenJinagaClient();
        var tenant = await j.Fact(new Tenant("contoso"));
        var account = await j.Fact(new Account(tenant, "alice"));
        var mirror = await j.Fact(new Mirror(account, "east"));

        var hosts = new List<string>();
        var removals = 0;
        var watch = j.Watch(AccountsWithTheirCurrentMirrors, tenant, projection =>
        {
            projection.Mirrors.OnAdded(child =>
            {
                lock (hosts)
                {
                    hosts.Add(child.host);
                }
                return () =>
                {
                    lock (hosts)
                    {
                        hosts.Remove(child.host);
                    }
                    Interlocked.Increment(ref removals);
                };
            });
        });

        try
        {
            await watch.Loaded;
            lock (hosts)
            {
                hosts.Should().ContainSingle().Which.Should().Be("east");
            }

            await j.Fact(new MirrorRetired(mirror));

            lock (hosts)
            {
                hosts.Should().BeEmpty();
            }
            removals.Should().Be(1);
        }
        finally
        {
            watch.Stop();
        }
    }

    /// <summary>The chores one watch holds, for a specification the test chooses.</summary>
    private class ObservedChores
    {
        private readonly List<Chore> chores = new List<Chore>();
        private int addedCalls;
        private int removalCalls;

        public int AddedCalls => addedCalls;
        public int RemovalCalls => removalCalls;

        public IEnumerable<Chore> Chores
        {
            get
            {
                lock (chores)
                {
                    return chores.ToArray();
                }
            }
        }

        public IObserver Watch(JinagaClient j, Specification<Tenant, Chore> specification, Tenant tenant)
        {
            return j.Watch(specification, tenant, chore =>
            {
                Interlocked.Increment(ref addedCalls);
                lock (chores)
                {
                    chores.Add(chore);
                }
                return () =>
                {
                    Interlocked.Increment(ref removalCalls);
                    lock (chores)
                    {
                        chores.Remove(chore);
                    }
                };
            });
        }
    }

    private JinagaClient GivenJinagaClient()
    {
        var options = new JinagaClientOptions();
        return new JinagaClient(
            new SQLiteStore(SQLitePath, NullLoggerFactory.Instance),
            new FakeNetwork(output),
            [],
            NullLoggerFactory.Instance,
            options);
    }
}
