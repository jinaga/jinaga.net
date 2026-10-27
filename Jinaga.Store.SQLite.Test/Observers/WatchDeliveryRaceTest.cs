using Jinaga.Observers;
using Jinaga.Store.SQLite.Test.Fakes;
using Jinaga.Store.SQLite.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Jinaga.Store.SQLite.Test.Observers;

/// <summary>
/// What a watch delivers while one of its added handlers has not returned, against the SQLite
/// store. The same intervals are covered against the in-memory store in <c>Jinaga.Test</c>; a
/// shape that holds for one store and not the other is a failure, and the decision that
/// settles each of these rows is a pinned read, which each store renders its own way.
/// </summary>
public class WatchDeliveryRaceTest
{
    private readonly ITestOutputHelper output;

    public WatchDeliveryRaceTest(ITestOutputHelper output)
    {
        this.output = output;
    }

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

    /// <summary>
    /// The accounts of a tenant that have a current mirror, each with its current mirrors.
    /// A second mirror re-decides the account while it is already held, so the account can be
    /// delivered by a listener while an older result that holds it is still pending.
    /// </summary>
    private static readonly Specification<Tenant, AccountWithMirrors> AccountsWithTheirCurrentMirrors =
        Given<Tenant>.Match((tenant, facts) =>
            from account in facts.OfType<Account>()
            where account.tenant == tenant
            where facts.Any<Mirror>(mirror =>
                mirror.account == account &&
                !facts.Any<MirrorRetired>(retired => retired.mirror == mirror))
            select new AccountWithMirrors(
                account,
                facts.Observable(
                    from mirror in facts.OfType<Mirror>()
                    where mirror.account == account
                    where !facts.Any<MirrorRetired>(retired => retired.mirror == mirror)
                    select mirror))
        );

    private record ActivatedAccount(Account Account, IObservableCollection<Mirror> Mirrors);

    private record AccountWithMirrors(Account Account, IObservableCollection<Mirror> Mirrors);

    [Fact]
    public async Task Watch_ChildRemovedWhileItsParentsHandlerIsHeld_DoesNotDeliverTheChild()
    {
        await WithClient(async j =>
        {
            var tenant = await j.Fact(new Tenant("contoso"));
            var account = await j.Fact(new Account(tenant, "alice"));
            var mirror = await j.Fact(new Mirror(account, "east"));

            var observed = new ObservedChildren();
            var watch = observed.Watch(j, ActivatedAccountsWithTheirCurrentMirrors, tenant);
            try
            {
                await watch.Loaded;
                observed.Hosts.Should().BeEmpty();

                // Activating the account adds the parent through a listener. Its handler
                // registers the child collection's handler and then blocks, so the child is
                // pending delivery out of the projection computed just now.
                var activation = j.Fact(new Activated(account));
                await observed.ParentHandlerEntered;

                await j.Fact(new MirrorRetired(mirror));

                observed.OpenGate();
                await activation;

                observed.Hosts.Should().BeEmpty();
                observed.AddedCalls.Should().Be(0);
                (await j.Query(CurrentMirrorsOfAccount, account)).Should().BeEmpty();
            }
            finally
            {
                observed.OpenGate();
                watch.Stop();
            }
        });
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

    private async Task ChildRestoredWhileItsParentsHandlerIsHeld(int run)
    {
        await WithClient(async j =>
        {
            var tenant = await j.Fact(new Tenant("contoso"));
            var account = await j.Fact(new Account(tenant, "alice"));
            var mirror = await j.Fact(new Mirror(account, "east"));

            var observed = new ObservedChildren();
            var watch = observed.Watch(j, ActivatedAccountsWithTheirCurrentMirrors, tenant);
            try
            {
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
            }
            finally
            {
                observed.OpenGate();
                watch.Stop();
            }
        });
    }

    /// <summary>
    /// A row the re-check decides absent and then decides present again, both while the
    /// initial read is held open by another row's handler. The second decision is newer than
    /// the withdrawal the first one recorded, so it is a new add.
    /// </summary>
    [Fact]
    public async Task Watch_ARowDecidedAbsentThenPresentDuringTheInitialRead_HoldsTheRow()
    {
        await WithClient(async j =>
        {
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
        });
    }

    /// <summary>
    /// A row in the initial read's result set, delivered by a listener and then removed, all
    /// while the initial read is held open by another row's handler.
    ///
    /// The removal reaches a row that is held, so it calls the removal function and retires
    /// the row. The initial read still owes that row a delivery out of a result computed
    /// before the removal, and that delivery must not bring it back.
    /// </summary>
    [Fact]
    public async Task Watch_ARowDeliveredThenRemovedDuringTheInitialRead_StaysRemoved()
    {
        await WithClient(async j =>
        {
            var tenant = await j.Fact(new Tenant("contoso"));
            var held = await j.Fact(new Account(tenant, "held"));
            await j.Fact(new Mirror(held, "east"));
            var other = await j.Fact(new Account(tenant, "other"));
            var first = await j.Fact(new Mirror(other, "west"));

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
                lock (accounts)
                {
                    // The initial read reached the held row first, so it still owes the other.
                    accounts.Should().BeEmpty();
                }

                // A second mirror re-decides the other account, which delivers it now.
                var second = await j.Fact(new Mirror(other, "south"));
                lock (accounts)
                {
                    accounts.Select(account => account.identifier).Should().Equal("other");
                }

                // Retiring both mirrors removes it.
                await j.Fact(new MirrorRetired(first));
                await j.Fact(new MirrorRetired(second));
                lock (accounts)
                {
                    accounts.Should().BeEmpty();
                }

                release.TrySetResult(true);
                await watch.Loaded;

                lock (accounts)
                {
                    accounts.Select(account => account.identifier).Should().Equal("held");
                }
                (await j.Query(AccountsWithACurrentMirror, tenant)).Should().ContainSingle();
            }
            finally
            {
                release.TrySetResult(true);
                watch.Stop();
            }
        });
    }

    /// <summary>
    /// The same interval one level down. A parent in the initial read's result set is
    /// delivered by a listener along with its children, and one child is then removed. When
    /// the initial read reaches the parent it finds it held, and the children of its older
    /// projection must not bring back the one that was removed.
    /// </summary>
    [Fact]
    public async Task Watch_AChildDeliveredThenRemovedDuringTheInitialRead_StaysRemoved()
    {
        await WithClient(async j =>
        {
            var tenant = await j.Fact(new Tenant("contoso"));
            var held = await j.Fact(new Account(tenant, "held"));
            await j.Fact(new Mirror(held, "north"));
            var alice = await j.Fact(new Account(tenant, "alice"));
            var east = await j.Fact(new Mirror(alice, "east"));

            var observed = new GatedAccounts();
            observed.Gate("held");
            var watch = j.Watch(AccountsWithTheirCurrentMirrors, tenant, (AccountWithMirrors row) =>
                observed.Added(row.Account, row.Mirrors));

            try
            {
                await observed.Entered("held");
                observed.Hosts.Should().BeEmpty();

                // A second mirror re-decides alice, which delivers her and both mirrors now.
                await j.Fact(new Mirror(alice, "west"));
                observed.Hosts.Should().BeEquivalentTo(new[] { "east", "west" });

                // Retiring one mirror removes that child. Alice stays, through the other.
                await j.Fact(new MirrorRetired(east));
                observed.Hosts.Should().BeEquivalentTo(new[] { "west" });

                observed.Release("held");
                await watch.Loaded;

                observed.Hosts.Should().BeEquivalentTo(new[] { "north", "west" });
                observed.ChildAddedCalls.Should().Be(observed.ChildRemovalCalls + 2);
            }
            finally
            {
                observed.ReleaseAll();
                watch.Stop();
            }
        });
    }

    /// <summary>
    /// A child removed while its parent's handler is held, with the initial read ending in
    /// that interval. The parent was added by a listener, so its delivery is not the initial
    /// read's, and the end of the initial read must not drop the withdrawal that delivery
    /// still needs.
    /// </summary>
    [Fact]
    public async Task Watch_TheInitialReadEndingWhileAParentIsHeld_StillWithdrawsItsRemovedChild()
    {
        await WithClient(async j =>
        {
            var tenant = await j.Fact(new Tenant("contoso"));
            var held = await j.Fact(new Account(tenant, "held"));
            await j.Fact(new Activated(held));
            var alice = await j.Fact(new Account(tenant, "alice"));
            var east = await j.Fact(new Mirror(alice, "east"));

            var observed = new GatedAccounts();
            observed.Gate("held");
            observed.Gate("alice");
            var watch = j.Watch(ActivatedAccountsWithTheirCurrentMirrors, tenant, (ActivatedAccount row) =>
                observed.Added(row.Account, row.Mirrors));

            try
            {
                await observed.Entered("held");

                // Activating alice adds her through a listener. Her handler registers the child
                // collection's handler and then blocks, so east is pending delivery out of the
                // projection computed just now.
                var activation = j.Fact(new Activated(alice));
                await observed.Entered("alice");

                await j.Fact(new MirrorRetired(east));

                // The initial read ends while alice's delivery is still in progress.
                observed.Release("held");
                await watch.Loaded;

                observed.Release("alice");
                await activation;

                observed.Hosts.Should().BeEmpty();
                observed.ChildAddedCalls.Should().Be(0);
                (await j.Query(CurrentMirrorsOfAccount, alice)).Should().BeEmpty();
            }
            finally
            {
                observed.ReleaseAll();
                watch.Stop();
            }
        });
    }

    /// <summary>
    /// Runs the body against a client over a database of its own, and deletes it afterward,
    /// so a repeated run decides its rows from its own store rather than from what an
    /// earlier run left behind.
    /// </summary>
    private async Task WithClient(Func<JinagaClient, Task> body)
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JinagaSQLiteTest",
            $"WatchDeliveryRaceTest-{Guid.NewGuid():N}.db");
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        var j = new JinagaClient(
            new SQLiteStore(path, NullLoggerFactory.Instance),
            new FakeNetwork(output),
            [],
            NullLoggerFactory.Instance,
            new JinagaClientOptions());
        try
        {
            await body(j);
        }
        finally
        {
            await j.Unload();
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    /// <summary>
    /// A watch of accounts and their mirrors whose handler holds, for each account the test
    /// gates, until the test releases that account. Each parent registers its child handler
    /// before it holds.
    /// </summary>
    private class GatedAccounts
    {
        private readonly Dictionary<string, TaskCompletionSource<bool>> entered = new();
        private readonly Dictionary<string, TaskCompletionSource<bool>> gates = new();
        private readonly List<string> hosts = new List<string>();
        private int childAddedCalls;
        private int childRemovalCalls;

        public int ChildAddedCalls => Volatile.Read(ref childAddedCalls);
        public int ChildRemovalCalls => Volatile.Read(ref childRemovalCalls);

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

        public void Gate(string identifier)
        {
            lock (gates)
            {
                entered[identifier] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                gates[identifier] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        public Task Entered(string identifier)
        {
            lock (gates)
            {
                return entered[identifier].Task;
            }
        }

        public void Release(string identifier)
        {
            lock (gates)
            {
                gates[identifier].TrySetResult(true);
            }
        }

        public void ReleaseAll()
        {
            lock (gates)
            {
                foreach (var gate in gates.Values)
                {
                    gate.TrySetResult(true);
                }
            }
        }

        public async Task<Func<Task>> Added(Account account, IObservableCollection<Mirror> mirrors)
        {
            mirrors.OnAdded(mirror =>
            {
                Interlocked.Increment(ref childAddedCalls);
                lock (hosts)
                {
                    hosts.Add(mirror.host);
                }
                return () =>
                {
                    Interlocked.Increment(ref childRemovalCalls);
                    lock (hosts)
                    {
                        hosts.Remove(mirror.host);
                    }
                };
            });

            TaskCompletionSource<bool>? gate = null;
            lock (gates)
            {
                if (gates.TryGetValue(account.identifier, out var found))
                {
                    entered[account.identifier].TrySetResult(true);
                    gate = found;
                }
            }
            if (gate != null)
            {
                await gate.Task;
            }
            Func<Task> removal = () => Task.CompletedTask;
            return removal;
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
