using Jinaga.Observers;
using System.Threading;
using Jinaga.Test.Model;
using System.Collections.Generic;
using System.Linq;

namespace Jinaga.Test.Observers;

/// <summary>
/// What a watch owes when a level of its specification gains or loses a solution behind a
/// negation. The observer decides each candidate the inverses report against the store, so
/// what it holds after a save is what a read of the same specification holds.
///
/// Every test here states the watch's rows and the same specification's query result, because
/// agreement between the two is the property at stake. An inverse that was trusted to say
/// "add" or "remove" could disagree with the store in either direction.
/// </summary>
public class WatchThroughNegationTest
{
    /// <summary>
    /// The accounts of a tenant that have a mirror nobody has retired. The condition's own
    /// level gains and loses solutions, and the account's membership follows it.
    /// </summary>
    private static readonly Specification<Tenant, Account> AccountsWithACurrentMirror =
        Given<Tenant>.Match((tenant, facts) =>
            from account in facts.OfType<Account>()
            where account.tenant == tenant
            where facts.Any<Mirror>(mirror =>
                mirror.account == account &&
                !facts.Any<MirrorRetired>(retired => retired.mirror == mirror))
            select account
        );

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
    /// from inside the negated condition, so no inverse of the shape
    /// <c>AddSpecificationListener</c> takes can reach it.
    /// </summary>
    private static readonly Specification<User, Channel, Message> UnreadMessages =
        Given<User, Channel>.Match((user, channel, facts) =>
            from message in facts.OfType<Message>()
            where message.channel == channel
            where !facts.Any<Read>(read => read.message == message && read.user == user)
            select message
        );

    [Fact]
    public async Task Watch_RetiringTheOnlyCurrentMirror_RemovesTheAccount()
    {
        var j = JinagaTest.Create();
        var tenant = await j.Fact(new Tenant("contoso"));
        var account = await j.Fact(new Account(tenant, "alice"));
        var mirror = await j.Fact(new Mirror(account, "east"));

        var observed = new ObservedAccounts();
        var watch = observed.Watch(j, tenant);
        await watch.Loaded;
        observed.Accounts.Should().ContainSingle();

        await j.Fact(new MirrorRetired(mirror));

        observed.Accounts.Should().BeEmpty();
        observed.RemovalCalls.Should().Be(1);
        (await j.Query(AccountsWithACurrentMirror, tenant)).Should().BeEmpty();

        watch.Stop();
    }

    [Fact]
    public async Task Watch_RetiringOneOfTwoCurrentMirrors_KeepsTheAccountAndDoesNotThrow()
    {
        var j = JinagaTest.Create();
        var tenant = await j.Fact(new Tenant("contoso"));
        var account = await j.Fact(new Account(tenant, "alice"));
        var east = await j.Fact(new Mirror(account, "east"));
        await j.Fact(new Mirror(account, "west"));

        var observed = new ObservedAccounts();
        var watch = observed.Watch(j, tenant);
        await watch.Loaded;

        // The remove inverse of the retirement binds the enclosing condition's own label a
        // second time, which is what made this save throw rather than notify.
        var save = async () => await j.Fact(new MirrorRetired(east));

        await save.Should().NotThrowAsync();
        observed.Accounts.Should().ContainSingle()
            .Which.identifier.Should().Be("alice");
        observed.RemovalCalls.Should().Be(0);
        (await j.Query(AccountsWithACurrentMirror, tenant)).Should().ContainSingle();

        watch.Stop();
    }

    [Fact]
    public async Task Watch_OneCompletionFalsifyingTwoSiblingConditions_RemovesTheChoreOnce()
    {
        var j = JinagaTest.Create();
        var tenant = await j.Fact(new Tenant("contoso"));
        var chore = await j.Fact(new Chore(tenant, "sweep"));

        var observed = new ObservedChores();
        var watch = observed.Watch(j, ChoresNotDone, tenant);
        await watch.Loaded;
        observed.AddedCalls.Should().Be(1);

        await j.Fact(new ChoreDone(chore, chore));

        observed.Chores.Should().BeEmpty();
        observed.AddedCalls.Should().Be(1);
        observed.RemovalCalls.Should().Be(1);
        (await j.Query(ChoresNotDone, tenant)).Should().BeEmpty();

        watch.Stop();
    }

    [Fact]
    public async Task Watch_TheInnermostConditionBecomingTrue_AddsTheChore()
    {
        var j = JinagaTest.Create();
        var tenant = await j.Fact(new Tenant("contoso"));
        var chore = await j.Fact(new Chore(tenant, "sweep"));
        var block = await j.Fact(new Block(chore));
        var reversal = await j.Fact(new Override(block));

        var observed = new ObservedChores();
        var watch = observed.Watch(j, ChoresNotBlocked, tenant);
        await watch.Loaded;
        // The override has no reason yet, so it does not count and the block still holds.
        observed.Chores.Should().BeEmpty();

        await j.Fact(new Reason(reversal));

        observed.Chores.Should().ContainSingle()
            .Which.description.Should().Be("sweep");
        observed.AddedCalls.Should().Be(1);
        (await j.Query(ChoresNotBlocked, tenant)).Should().ContainSingle();

        watch.Stop();
    }

    [Fact]
    public async Task Watch_WithAGivenJoinedOnlyInsideTheCondition_LoadsAndRemovesWhatIsRead()
    {
        var j = JinagaTest.Create();
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

        // On the inverter this faulted with "Malformed specification.", because the given the
        // condition joins cannot be reached from the saved fact.
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

        watch.Stop();
    }

    [Fact]
    public async Task Watch_RetiringAMirrorOfAChildCollection_RemovesTheChild()
    {
        var j = JinagaTest.Create();
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

        watch.Stop();
    }

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

    /// <summary>The accounts one watch holds, and the removals it has been told about.</summary>
    private class ObservedAccounts
    {
        private readonly List<Account> accounts = new List<Account>();
        private int addedCalls;
        private int removalCalls;

        public int AddedCalls => addedCalls;
        public int RemovalCalls => removalCalls;

        public IEnumerable<Account> Accounts
        {
            get
            {
                lock (accounts)
                {
                    return accounts.ToArray();
                }
            }
        }

        public IObserver Watch(JinagaClient j, Tenant tenant)
        {
            return j.Watch(AccountsWithACurrentMirror, tenant, account =>
            {
                Interlocked.Increment(ref addedCalls);
                lock (accounts)
                {
                    accounts.Add(account);
                }
                return () =>
                {
                    Interlocked.Increment(ref removalCalls);
                    lock (accounts)
                    {
                        accounts.Remove(account);
                    }
                };
            });
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
}
