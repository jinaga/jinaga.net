using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Projections;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Xunit.Abstractions;

namespace Jinaga.Test.Storage;

/// <summary>
/// A replica reads nothing until it holds every given of the specification, as a fact of
/// the given's declared type. That is jinaga-spec's <c>Specification.read</c>, and the
/// shape that makes it load-bearing is a given named only inside a negated condition:
/// nothing in the outer matches joins it, so the store answered for a fact it did not
/// have, and loading the references of the row it returned faulted on the given.
/// </summary>
public class UnheldGivenReadTest
{
    private readonly ITestOutputHelper output;

    public UnheldGivenReadTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    /// <summary>
    /// The messages of a channel that a user has not read. The user is reached only from
    /// inside the negated condition, so the matches never require it.
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
    /// successor step that the matches do require.
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
        var store = new MemoryStore();
        var j = GivenJinagaClient(store);
        var channel = await j.Fact(new Channel("general"));
        await j.Fact(new Message(channel, "Where is everybody?"));
        var user = new User("---FAKE READER---");   // not saved

        var givenTuple = UnreadMessagesTuple(j, user, channel);

        (await store.Read(givenTuple, UnreadMessages, CancellationToken.None))
            .Should().BeEmpty();
        (await j.Query(UnreadMessages, user, channel)).Should().BeEmpty();

        await j.Fact(user);

        (await store.Read(givenTuple, UnreadMessages, CancellationToken.None))
            .Should().ContainSingle();
        (await j.Query(UnreadMessages, user, channel)).Should().ContainSingle()
            .Which.text.Should().Be("Where is everybody?");
    }

    [Fact]
    public async Task Watch_StartedWhileAGivenIsNotHeld_LoadsEmptyAndDeliversWhenItArrives()
    {
        var store = new MemoryStore();
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
            return Task.CompletedTask;
        });

        try
        {
            // The initial read used to answer for the unheld user, and loading the
            // references of that row threw KeyNotFoundException out of MemoryStore.Load.
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
        var store = new MemoryStore();
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

        (await store.Read(givenTuple, UnreadMessages, CancellationToken.None))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Read_WithEveryGivenHeld_ReadsTheRowsItAlwaysDid()
    {
        var store = new MemoryStore();
        var j = GivenJinagaClient(store);
        var user = await j.Fact(new User("---FAKE READER---"));
        var channel = await j.Fact(new Channel("general"));
        var message = await j.Fact(new Message(channel, "Where is everybody?"));

        var givenTuple = UnreadMessagesTuple(j, user, channel);

        (await store.Read(givenTuple, UnreadMessages, CancellationToken.None))
            .Should().ContainSingle();

        await j.Fact(new Read(message, user));

        // Still held, so the condition decides the row rather than the rule above.
        (await store.Read(givenTuple, UnreadMessages, CancellationToken.None))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Read_WithTheOnlyGivenNotHeld_ReadsNothing()
    {
        var store = new MemoryStore();
        var j = GivenJinagaClient(store);
        var channel = new Channel("general");   // not saved

        var givenTuple = FactReferenceTuple.Empty
            .Add(MessagesInChannel.Givens[0].Label.Name, Reference(j, MessagesInChannel, 0, channel));

        (await store.Read(givenTuple, MessagesInChannel, CancellationToken.None))
            .Should().BeEmpty();
        (await j.Query(MessagesInChannel, channel)).Should().BeEmpty();

        var saved = await j.Fact(channel);
        await j.Fact(new Message(saved, "Where is everybody?"));

        (await store.Read(givenTuple, MessagesInChannel, CancellationToken.None))
            .Should().ContainSingle();
    }

    /// <summary>
    /// An inverse read answers for the watch's own givens, so the rule covers it too. Nothing
    /// else reads an inverse against an unheld given: a watch registers its listeners before
    /// its first read, so a save can reach one while a given it names is still on its way.
    /// </summary>
    [Fact]
    public async Task Read_AnInverseWhoseWatchGivenIsNotHeld_ReadsNothingUntilItArrives()
    {
        var store = new MemoryStore();
        var j = GivenJinagaClient(store);
        var user = new User("---FAKE READER---");   // not saved
        var channel = await j.Fact(new Channel("general"));
        var message = await j.Fact(new Message(channel, "Where is everybody?"));

        var references = ImmutableDictionary<string, FactReference>.Empty
            .Add(UnreadMessages.Givens[0].Label.Name, Reference(j, UnreadMessages, 0, user))
            .Add(UnreadMessages.Givens[1].Label.Name, Reference(j, UnreadMessages, 1, channel));
        var messageReference = new FactReference("Chat.Message", j.Hash(message));

        // The inverses a saved message reaches, and that name the unheld user. An inverse that
        // does not name it could not be answering for it, so it would prove nothing here.
        var exact = InverseCandidates.ExactInverses(UnreadMessages);
        var pinnedOnMessage = exact.Adds
            .AddRange(exact.Removes)
            .Where(inverse =>
                inverse.Givens[inverse.Givens.Count - 1].Label.Type == "Chat.Message" &&
                inverse.Givens.Any(given => given.Label.Name == UnreadMessages.Givens[0].Label.Name))
            .ToImmutableList();
        pinnedOnMessage.Should().NotBeEmpty();

        var tuples = pinnedOnMessage
            .Select(inverse => inverse.Givens
                .Take(inverse.Givens.Count - 1)
                .Aggregate(
                    FactReferenceTuple.Empty,
                    (tuple, given) => tuple.Add(given.Label.Name, references[given.Label.Name]))
                .Add(InverseCandidates.PinLabel, messageReference))
            .ToImmutableList();

        foreach (var (inverse, givenTuple) in pinnedOnMessage.Zip(tuples, (i, t) => (i, t)))
        {
            (await store.Read(givenTuple, inverse, CancellationToken.None))
                .Should().BeEmpty();
        }

        // Saving the user is the only thing that changed, so the rule is what the emptiness
        // above rested on rather than an inverse that never had a row to report.
        await j.Fact(user);

        var answered = false;
        foreach (var (inverse, givenTuple) in pinnedOnMessage.Zip(tuples, (i, t) => (i, t)))
        {
            answered |= (await store.Read(givenTuple, inverse, CancellationToken.None)).Any();
        }
        answered.Should().BeTrue();
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

    private JinagaClient GivenJinagaClient(MemoryStore store)
    {
        return new JinagaClient(
            store,
            new FakeNetwork(output),
            ImmutableList<Specification>.Empty,
            NullLoggerFactory.Instance,
            new JinagaClientOptions());
    }
}
