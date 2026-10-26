using Jinaga.Observers;
using Jinaga.Test.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Jinaga.Test.Observers;

/// <summary>
/// What a watch delivers while one of its added handlers has not returned. A handler is the
/// caller's code, so it may take as long as it likes, and nothing the observer owes another
/// row may wait on it.
///
/// Two intervals matter. One row's handler must not hold up another row's delivery, at the
/// initial read or after it. And a row nested within a row whose handler is running is
/// pending delivery out of a projection computed before that handler started, so a removal
/// decided in between has to withdraw that delivery rather than be dropped.
/// </summary>
public class WatchDeliveryRaceTest
{
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
    /// The activated accounts of a tenant, each with the mirrors nobody has retired without
    /// restoring. The account's own membership turns on its activation rather than on its
    /// mirrors, so a parent can be added by a listener while its children already exist.
    /// </summary>
    private static readonly Specification<Tenant, ActivatedAccount> ActivatedAccountsWithTheirCurrentMirrors =
        Given<Tenant>.Match((tenant, facts) =>
            from account in facts.OfType<Account>()
            where account.tenant == tenant
            where facts.Any<Activated>(activated => activated.account == account)
            select new ActivatedAccount(
                account,
                facts.Observable(
                    from mirror in facts.OfType<Mirror>()
                    where mirror.account == account
                    where !facts.Any<MirrorRetired>(retired =>
                        retired.mirror == mirror &&
                        !facts.Any<MirrorRestored>(restored => restored.retired == retired))
                    select mirror))
        );

    private static readonly Specification<Account, Mirror> CurrentMirrorsOfAccount =
        Given<Account>.Match((account, facts) =>
            from mirror in facts.OfType<Mirror>()
            where mirror.account == account
            where !facts.Any<MirrorRetired>(retired =>
                retired.mirror == mirror &&
                !facts.Any<MirrorRestored>(restored => restored.retired == retired))
            select mirror
        );

    private record ActivatedAccount(Account Account, IObservableCollection<Mirror> Mirrors);

    [Fact]
    public async Task Watch_AHandlerThatNeverReturnsDuringTheInitialRead_StillDeliversAndRemovesAnotherRow()
    {
        var j = JinagaTest.Create();
        var tenant = await j.Fact(new Tenant("contoso"));
        var held = await j.Fact(new Account(tenant, "held"));
        await j.Fact(new Mirror(held, "east"));

        var accounts = new List<Account>();
        var removals = 0;
        var heldHandlerEntered = new TaskCompletionSource<bool>();
        var neverCompletes = new TaskCompletionSource<bool>();

        var watch = j.Watch(AccountsWithACurrentMirror, tenant, async (Account account) =>
        {
            if (account.identifier == "held")
            {
                heldHandlerEntered.TrySetResult(true);
                await neverCompletes.Task;
            }
            lock (accounts)
            {
                accounts.Add(account);
            }
            Func<Task> removal = () =>
            {
                lock (accounts)
                {
                    accounts.Remove(account);
                }
                Interlocked.Increment(ref removals);
                return Task.CompletedTask;
            };
            return removal;
        });

        try
        {
            // The initial read is now inside the caller's handler for one row and will not
            // leave it. Its own Loaded never completes, which is the caller's doing.
            await heldHandlerEntered.Task;

            var other = await j.Fact(new Account(tenant, "other"));
            var mirror = await j.Fact(new Mirror(other, "west"));

            lock (accounts)
            {
                accounts.Select(account => account.identifier).Should().Equal("other");
            }

            await j.Fact(new MirrorRetired(mirror));

            lock (accounts)
            {
                accounts.Should().BeEmpty();
            }
            removals.Should().Be(1);
        }
        finally
        {
            neverCompletes.TrySetResult(true);
            watch.Stop();
        }
    }

    [Fact]
    public async Task Watch_AHandlerAwaitingAnotherRowOfItsOwnWatch_DeliversBoth()
    {
        var j = JinagaTest.Create();
        var tenant = await j.Fact(new Tenant("contoso"));
        var first = await j.Fact(new Account(tenant, "first"));
        await j.Fact(new Mirror(first, "east"));

        var accounts = new List<Account>();
        var firstHandlerEntered = new TaskCompletionSource<bool>();
        var secondDelivered = new TaskCompletionSource<bool>();

        var watch = j.Watch(AccountsWithACurrentMirror, tenant, async (Account account) =>
        {
            lock (accounts)
            {
                accounts.Add(account);
            }
            if (account.identifier == "first")
            {
                firstHandlerEntered.TrySetResult(true);
                // Waiting on a row this watch has not delivered yet. Nothing the observer
                // owes that row may wait on this handler in turn.
                await secondDelivered.Task;
            }
            else
            {
                secondDelivered.TrySetResult(true);
            }
            Func<Task> removal = () => Task.CompletedTask;
            return removal;
        });

        try
        {
            await firstHandlerEntered.Task;

            var second = await j.Fact(new Account(tenant, "second"));
            await j.Fact(new Mirror(second, "west"));

            await watch.Loaded;
            lock (accounts)
            {
                accounts.Select(account => account.identifier)
                    .Should().BeEquivalentTo(new[] { "first", "second" });
            }
        }
        finally
        {
            secondDelivered.TrySetResult(true);
            watch.Stop();
        }
    }

    [Fact]
    public async Task Watch_ChildRemovedWhileItsParentsHandlerIsHeld_DoesNotDeliverTheChild()
    {
        var j = JinagaTest.Create();
        var tenant = await j.Fact(new Tenant("contoso"));
        var account = await j.Fact(new Account(tenant, "alice"));
        var mirror = await j.Fact(new Mirror(account, "east"));

        var observed = new ObservedChildren();
        var watch = observed.Watch(j, ActivatedAccountsWithTheirCurrentMirrors, tenant);
        await watch.Loaded;
        observed.Hosts.Should().BeEmpty();

        // Activating the account adds the parent through a listener. Its handler registers
        // the child collection's handler and then blocks, so the child is pending delivery
        // out of the projection computed just now.
        var activation = j.Fact(new Activated(account));
        await observed.ParentHandlerEntered;

        await j.Fact(new MirrorRetired(mirror));

        observed.OpenGate();
        await activation;

        observed.Hosts.Should().BeEmpty();
        observed.AddedCalls.Should().Be(0);
        (await j.Query(CurrentMirrorsOfAccount, account)).Should().BeEmpty();

        watch.Stop();
    }

    /// <summary>
    /// Repeated, because the interval it guards opens and closes on the timing of a handler
    /// the test holds. One passing run says little; the same run twenty times over says the
    /// delivery does not depend on which side of the handler the restoration lands on.
    /// </summary>
    [Fact]
    public async Task Watch_ChildRestoredWhileItsParentsHandlerIsHeld_DeliversTheChildOnce()
    {
        for (int run = 0; run < 20; run++)
        {
            await ChildRestoredWhileItsParentsHandlerIsHeld(run);
        }
    }

    private static async Task ChildRestoredWhileItsParentsHandlerIsHeld(int run)
    {
        var j = JinagaTest.Create();
        var tenant = await j.Fact(new Tenant("contoso"));
        var account = await j.Fact(new Account(tenant, "alice"));
        var mirror = await j.Fact(new Mirror(account, "east"));

        var observed = new ObservedChildren();
        var watch = observed.Watch(j, ActivatedAccountsWithTheirCurrentMirrors, tenant);
        await watch.Loaded;

        var activation = j.Fact(new Activated(account));
        await observed.ParentHandlerEntered;

        var retired = await j.Fact(new MirrorRetired(mirror));
        await j.Fact(new MirrorRestored(retired));

        observed.OpenGate();
        await activation;

        observed.Hosts.Should().Equal(new[] { "east" }, $"run {run} should hold the restored mirror");
        observed.AddedCalls.Should().Be(1, $"run {run} should deliver it once");
        (await j.Query(CurrentMirrorsOfAccount, account)).Should().ContainSingle();

        watch.Stop();
    }

    /// <summary>
    /// A row the re-check decides absent and then decides present again, both while the
    /// initial read is held open by another row's handler.
    ///
    /// The first decision records a withdrawal against a delivery the initial read will
    /// never make, because the row was not in its result set. The second decision is newer
    /// than that withdrawal, so it is a new add. A withdrawal consumed by whichever add
    /// comes next, fresh or stale, loses the row outright: the watch never shows it while a
    /// query returns it. Rows the re-check decides absent and never held are routine, so
    /// nothing about this is a corner case.
    /// </summary>
    [Fact]
    public async Task Watch_ARowDecidedAbsentThenPresentDuringTheInitialRead_HoldsTheRow()
    {
        var j = JinagaTest.Create();
        var tenant = await j.Fact(new Tenant("contoso"));
        var held = await j.Fact(new Account(tenant, "held"));
        await j.Fact(new Mirror(held, "east"));
        var other = await j.Fact(new Account(tenant, "other"));

        var accounts = new List<Account>();
        var heldHandlerEntered = new TaskCompletionSource<bool>();
        var release = new TaskCompletionSource<bool>();

        var watch = j.Watch(AccountsWithACurrentMirror, tenant, async (Account account) =>
        {
            if (account.identifier == "held")
            {
                heldHandlerEntered.TrySetResult(true);
                await release.Task;
            }
            lock (accounts)
            {
                accounts.Add(account);
            }
            Func<Task> removal = () =>
            {
                lock (accounts)
                {
                    accounts.Remove(account);
                }
                return Task.CompletedTask;
            };
            return removal;
        });

        try
        {
            await heldHandlerEntered.Task;

            // One batch carries a mirror for the other account and that mirror's own
            // retirement, so the re-check decides the row absent.
            await j.Fact(new MirrorRetired(new Mirror(other, "west")));
            lock (accounts)
            {
                accounts.Should().BeEmpty();
            }

            // A second, current mirror makes the same row present again.
            await j.Fact(new Mirror(other, "south"));

            release.TrySetResult(true);
            await watch.Loaded;

            lock (accounts)
            {
                accounts.Select(account => account.identifier)
                    .Should().BeEquivalentTo(new[] { "held", "other" });
            }
            (await j.Query(AccountsWithACurrentMirror, tenant)).Should().HaveCount(2);
        }
        finally
        {
            release.TrySetResult(true);
            watch.Stop();
        }
    }

    /// <summary>
    /// A watch whose parent handler registers a child handler, reports that it has done so,
    /// and then holds until the test releases it.
    /// </summary>
    private class ObservedChildren
    {
        private readonly List<string> hosts = new List<string>();
        private readonly TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>();
        private readonly TaskCompletionSource<bool> gate = new TaskCompletionSource<bool>();
        private int addedCalls;
        private int removalCalls;

        public Task ParentHandlerEntered => entered.Task;
        public int AddedCalls => addedCalls;
        public int RemovalCalls => removalCalls;

        public IEnumerable<string> Hosts
        {
            get
            {
                lock (hosts)
                {
                    return hosts.ToArray();
                }
            }
        }

        public void OpenGate() => gate.TrySetResult(true);

        public IObserver Watch(JinagaClient j, Specification<Tenant, ActivatedAccount> specification, Tenant tenant)
        {
            return j.Watch(specification, tenant, async (ActivatedAccount projection) =>
            {
                projection.Mirrors.OnAdded(mirror =>
                {
                    Interlocked.Increment(ref addedCalls);
                    lock (hosts)
                    {
                        hosts.Add(mirror.host);
                    }
                    return () =>
                    {
                        Interlocked.Increment(ref removalCalls);
                        lock (hosts)
                        {
                            hosts.Remove(mirror.host);
                        }
                    };
                });
                entered.TrySetResult(true);
                await gate.Task;
                Func<Task> removal = () => Task.CompletedTask;
                return removal;
            });
        }
    }
}
