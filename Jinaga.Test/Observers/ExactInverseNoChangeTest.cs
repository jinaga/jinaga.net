using Jinaga.Test.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Jinaga.Test.Observers;

/// <summary>
/// A save that an inverse reports but that changes no row. A sibling of the witness the
/// save touched still decides the row the same way, so the row stays exactly as it is and
/// the watch owes no notification.
///
/// These are jinaga-spec's <c>exists-not-exists-second-mirror</c> and
/// <c>not-exists-not-exists-second-freeze</c>. They are the cases the exact inverses have
/// to get right without a second read to fall back on: a remove inverse that reported the
/// row here would retire a row the store still holds, and an add inverse that reported it
/// would deliver one the store does not. Each test then falsifies the remaining witness, so
/// a watch that reported nothing because it was listening to nothing fails too.
/// </summary>
public class ExactInverseNoChangeTest
{
    /// <summary>
    /// The accounts of a tenant that have a mirror nobody has retired. One retirement
    /// among two mirrors leaves the condition satisfied by the other.
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
    /// The accounts of a tenant that have no mirror left standing. A mirror stands until it
    /// is retired, so the account is held only once every mirror has been. Nested negation,
    /// and the complement of the specification above, so the row it gains is the row the
    /// other one loses.
    /// </summary>
    /// <remarks>
    /// The inner witness is a <see cref="Mirror"/> rather than a <see cref="Block"/> because
    /// a fact is identified by its content: <c>Block</c> names only its chore, so two of them
    /// on one chore are one fact, and "one of two" is not a state the store can hold. A
    /// mirror carries a host, so east and west are two.
    /// </remarks>
    private static readonly Specification<Tenant, Account> AccountsWithNoStandingMirror =
        Given<Tenant>.Match((tenant, facts) =>
            from account in facts.OfType<Account>()
            where account.tenant == tenant
            where !facts.Any<Mirror>(mirror =>
                mirror.account == account &&
                !facts.Any<MirrorRetired>(retired => retired.mirror == mirror))
            select account
        );

    [Fact]
    public async Task Watch_RetiringOneOfTwoMirrors_DeliversNoRemoval()
    {
        var j = JinagaTest.Create();
        var tenant = await j.Fact(new Tenant("contoso"));
        var account = await j.Fact(new Account(tenant, "alice"));
        var east = await j.Fact(new Mirror(account, "east"));
        var west = await j.Fact(new Mirror(account, "west"));

        var held = new List<Account>();
        var removals = 0;
        var watch = j.Watch(AccountsWithACurrentMirror, tenant, (Account a) =>
        {
            lock (held)
            {
                held.Add(a);
            }
            Func<Task> removal = () =>
            {
                Interlocked.Increment(ref removals);
                lock (held)
                {
                    held.Remove(a);
                }
                return Task.CompletedTask;
            };
            return Task.FromResult(removal);
        });
        await watch.Loaded;

        held.Should().ContainSingle();

        // The west mirror still has no retirement, so the account's condition is satisfied
        // either way and the row never leaves.
        await j.Fact(new MirrorRetired(east));

        removals.Should().Be(0);
        held.Should().ContainSingle();
        (await j.Query(AccountsWithACurrentMirror, tenant)).Should().ContainSingle();

        // Retiring the last one is the change, which proves the watch was listening at all.
        await j.Fact(new MirrorRetired(west));

        removals.Should().Be(1);
        held.Should().BeEmpty();
        (await j.Query(AccountsWithACurrentMirror, tenant)).Should().BeEmpty();

        watch.Stop();
    }

    [Fact]
    public async Task Watch_RetiringOneOfTwoMirrors_DeliversNoAddToTheComplement()
    {
        var j = JinagaTest.Create();
        var tenant = await j.Fact(new Tenant("contoso"));
        var account = await j.Fact(new Account(tenant, "alice"));
        var east = await j.Fact(new Mirror(account, "east"));
        var west = await j.Fact(new Mirror(account, "west"));

        // Two mirrors, or the retirement below would be the last one and the row would be a
        // real change.
        j.Hash(east).Should().NotBe(j.Hash(west));

        var held = new List<Account>();
        var watch = j.Watch(AccountsWithNoStandingMirror, tenant, (Account a) =>
        {
            lock (held)
            {
                held.Add(a);
            }
            Func<Task> removal = () =>
            {
                lock (held)
                {
                    held.Remove(a);
                }
                return Task.CompletedTask;
            };
            return Task.FromResult(removal);
        });
        await watch.Loaded;

        // Both mirrors stand, so the account is not a row to begin with.
        held.Should().BeEmpty();

        // West still stands, so retiring east leaves the account out.
        await j.Fact(new MirrorRetired(east));

        held.Should().BeEmpty();
        (await j.Query(AccountsWithNoStandingMirror, tenant)).Should().BeEmpty();

        // Retiring the last one is the change, which proves the watch was listening at all.
        await j.Fact(new MirrorRetired(west));

        held.Should().ContainSingle();
        (await j.Query(AccountsWithNoStandingMirror, tenant)).Should().ContainSingle();

        watch.Stop();
    }
}
