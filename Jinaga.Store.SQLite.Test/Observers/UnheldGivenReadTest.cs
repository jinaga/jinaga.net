using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using Jinaga.Store.SQLite.Test.Fakes;
using Jinaga.Store.SQLite.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;
using Xunit.Abstractions;

namespace Jinaga.Store.SQLite.Test.Observers;

/// <summary>
/// A replica reads nothing until it holds every given of the specification, as a fact of
/// the given's declared type. The SQLite store required a given to be stored only where
/// its query joined the given's fact row, so a given named only inside a negated condition
/// was joined in that condition's subquery alone and the outer query never required it.
///
/// The store renders a specification into SQL rather than walking it, so the rule has to
/// be exercised here and not only against the in-memory store. Unlike that store, this one
/// did not fault on the row it returned: <c>SQLiteStore.Load</c> does not throw on a
/// missing reference, so a watch quietly showed a message the replica could not attribute.
/// </summary>
public class UnheldGivenReadTest
{
    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "UnheldGivenReadTest.db");

    private readonly ITestOutputHelper output;

    public UnheldGivenReadTest(ITestOutputHelper output)
    {
        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);

        this.output = output;
    }

    /// <summary>
    /// The messages of a channel that a user has not read. The user is reached only from
    /// inside the negated condition, so the outer query never joins it.
    /// </summary>
    private static readonly Specification<User, Channel, Message> UnreadMessages =
        Given<User, Channel>.Match((user, channel, facts) =>
            from message in facts.OfType<Message>()
            where message.channel == channel
            where !facts.Any<Read>(read => read.message == message && read.user == user)
            select message
        );

    /// <summary>
    /// The messages of a channel, with the channel as its one given, reached by a
    /// successor step that the outer query does join.
    /// </summary>
    private static readonly Specification<Channel, Message> MessagesInChannel =
        Given<Channel>.Match((channel, facts) =>
            from message in facts.OfType<Message>()
            where message.channel == channel
            select message
        );

    [Fact]
    public async Task Read_WithAGivenTheStoreDoesNotHold_ReadsNothingUntilItArrives()
    {
        var store = new SQLiteStore(SQLitePath, NullLoggerFactory.Instance);
        var j = GivenJinagaClient(store);
        var channel = await j.Fact(new Channel("general"));
        await j.Fact(new Message(channel, "Where is everybody?"));
        var user = new User("---FAKE READER---");   // not saved

        var givenTuple = UnreadMessagesTuple(j, user, channel);

        (await ((IStore)store).Read(givenTuple, UnreadMessages, CancellationToken.None))
            .Should().BeEmpty();
        (await j.Query(UnreadMessages, user, channel)).Should().BeEmpty();

        await j.Fact(user);

        (await ((IStore)store).Read(givenTuple, UnreadMessages, CancellationToken.None))
            .Should().ContainSingle();
        (await j.Query(UnreadMessages, user, channel)).Should().ContainSingle()
            .Which.text.Should().Be("Where is everybody?");
    }

    [Fact]
    public async Task Watch_StartedWhileAGivenIsNotHeld_LoadsEmptyAndDeliversWhenItArrives()
    {
        var store = new SQLiteStore(SQLitePath, NullLoggerFactory.Instance);
        var j = GivenJinagaClient(store);
        var channel = await j.Fact(new Channel("general"));
        await j.Fact(new Message(channel, "Where is everybody?"));
        var user = new User("---FAKE READER---");   // not saved

        var messages = new List<Message>();
        var watch = j.Watch(UnreadMessages, user, channel, added =>
        {
            lock (messages)
            {
                messages.Add(added);
            }
        });

        try
        {
            await watch.Loaded;
            lock (messages)
            {
                messages.Should().BeEmpty();
            }

            await j.Fact(user);

            lock (messages)
            {
                messages.Should().ContainSingle()
                    .Which.text.Should().Be("Where is everybody?");
            }
            (await j.Query(UnreadMessages, user, channel)).Should().ContainSingle();
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task Read_WithAGivenStoredUnderAnotherType_ReadsNothing()
    {
        var store = new SQLiteStore(SQLitePath, NullLoggerFactory.Instance);
        var j = GivenJinagaClient(store);
        var channel = await j.Fact(new Channel("general"));
        await j.Fact(new Message(channel, "Where is everybody?"));

        // Both labels take the channel's reference, so the reference under the label
        // declared a user is stored. Holding a reference is not holding a fact of the
        // given's type, and only the type tells the two apart.
        var channelReference = Reference(j, UnreadMessages, 1, channel);
        var givenTuple = FactReferenceTuple.Empty
            .Add(UnreadMessages.Givens[0].Label.Name, channelReference)
            .Add(UnreadMessages.Givens[1].Label.Name, channelReference);

        (await ((IStore)store).Read(givenTuple, UnreadMessages, CancellationToken.None))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Read_WithEveryGivenHeld_ReadsTheRowsItAlwaysDid()
    {
        var store = new SQLiteStore(SQLitePath, NullLoggerFactory.Instance);
        var j = GivenJinagaClient(store);
        var user = await j.Fact(new User("---FAKE READER---"));
        var channel = await j.Fact(new Channel("general"));
        var message = await j.Fact(new Message(channel, "Where is everybody?"));

        var givenTuple = UnreadMessagesTuple(j, user, channel);

        (await ((IStore)store).Read(givenTuple, UnreadMessages, CancellationToken.None))
            .Should().ContainSingle();

        await j.Fact(new Read(message, user));

        // Still held, so the condition decides the row rather than the rule above.
        (await ((IStore)store).Read(givenTuple, UnreadMessages, CancellationToken.None))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Read_WithTheOnlyGivenNotHeld_ReadsNothing()
    {
        var store = new SQLiteStore(SQLitePath, NullLoggerFactory.Instance);
        var j = GivenJinagaClient(store);
        var channel = new Channel("general");   // not saved

        var givenTuple = FactReferenceTuple.Empty
            .Add(MessagesInChannel.Givens[0].Label.Name, Reference(j, MessagesInChannel, 0, channel));

        (await ((IStore)store).Read(givenTuple, MessagesInChannel, CancellationToken.None))
            .Should().BeEmpty();
        (await j.Query(MessagesInChannel, channel)).Should().BeEmpty();

        var saved = await j.Fact(channel);
        await j.Fact(new Message(saved, "Where is everybody?"));

        (await ((IStore)store).Read(givenTuple, MessagesInChannel, CancellationToken.None))
            .Should().ContainSingle();
    }

    private static FactReferenceTuple UnreadMessagesTuple(JinagaClient j, User user, Channel channel) =>
        FactReferenceTuple.Empty
            .Add(UnreadMessages.Givens[0].Label.Name, Reference(j, UnreadMessages, 0, user))
            .Add(UnreadMessages.Givens[1].Label.Name, Reference(j, UnreadMessages, 1, channel));

    /// <summary>
    /// The reference a tuple binds to the given at <paramref name="index"/>, with the type
    /// taken from the specification rather than written out again, so the type a test
    /// supplies is the one the store compares against.
    /// </summary>
    private static FactReference Reference<TFact>(JinagaClient j, Specification specification, int index, TFact fact) =>
        new FactReference(specification.Givens[index].Label.Type, j.Hash(fact));

    private JinagaClient GivenJinagaClient(SQLiteStore store)
    {
        return new JinagaClient(
            store,
            new FakeNetwork(output),
            [],
            NullLoggerFactory.Instance,
            new JinagaClientOptions());
    }
}
