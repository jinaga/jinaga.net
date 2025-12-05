using Jinaga.Projections;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;
using Xunit.Abstractions;

namespace Jinaga.Test
{
    public class QueueProcessorTest
    {
        private readonly ITestOutputHelper output;

        public QueueProcessorTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public async Task MultipleFacts_SavedInQuickSuccession_UsesSingleNetworkOperation()
        {
            // Arrange
            var network = new CountingFakeNetwork(output);
            var options = new JinagaClientOptions { QueueProcessingDelay = 50 };
            var j = new JinagaClient(new PersistentMemoryStore(), network, ImmutableList<Specification>.Empty, NullLoggerFactory.Instance, options);
            
            // Act
            await j.Fact(new TestFact("fact1"));
            await j.Fact(new TestFact("fact2"));
            await j.Fact(new TestFact("fact3"));
            
            await Task.Delay(200); // Wait for debounce period
            
            // Assert
            network.SaveCallCount.Should().Be(1);
            network.UploadedFacts.Count.Should().Be(3);
        }

        [Fact]
        public async Task FactsSavedWithLongerDelays_UseMultipleNetworkOperations()
        {
            // Arrange
            var network = new CountingFakeNetwork(output);
            var options = new JinagaClientOptions { QueueProcessingDelay = 50 };
            var j = new JinagaClient(new MemoryStore(), network, ImmutableList<Specification>.Empty, NullLoggerFactory.Instance, options);
            
            // Act
            await j.Fact(new TestFact("fact1"));
            await Task.Delay(100); // Wait longer than debounce period
            
            await j.Fact(new TestFact("fact2"));
            await Task.Delay(100); // Wait longer than debounce period
            
            await j.Fact(new TestFact("fact3"));
            await Task.Delay(100); // Wait longer than debounce period
            
            // Assert
            network.SaveCallCount.Should().Be(3);
            network.UploadedFacts.Count.Should().Be(3);
        }

        [Fact]
        public async Task QueueProcessingDelayZero_DisablesDebouncing()
        {
            // Arrange
            var network = new CountingFakeNetwork(output);
            var options = new JinagaClientOptions { QueueProcessingDelay = 0 };
            var j = new JinagaClient(new MemoryStore(), network, ImmutableList<Specification>.Empty, NullLoggerFactory.Instance, options);
            
            // Act
            await j.Fact(new TestFact("fact1"));
            await j.Fact(new TestFact("fact2"));
            await j.Fact(new TestFact("fact3"));
            
            await Task.Delay(50); // Short delay to ensure processing completes
            
            // Assert
            network.SaveCallCount.Should().Be(3); // Each fact should trigger a separate save
            network.UploadedFacts.Count.Should().Be(3);
        }

        [Fact]
        public async Task Push_ProcessesQueueImmediately()
        {
            // Arrange
            var network = new CountingFakeNetwork(output);
            var options = new JinagaClientOptions { QueueProcessingDelay = 1000 }; // Long delay
            var j = new JinagaClient(new MemoryStore(), network, ImmutableList<Specification>.Empty, NullLoggerFactory.Instance, options);
            
            // Act
            await j.Fact(new TestFact("fact1"));
            await j.Push(); // Should process immediately without waiting
            
            // Assert
            network.SaveCallCount.Should().Be(1);
            network.UploadedFacts.Count.Should().Be(1);
        }

        [Fact]
        public async Task CreatingRelatedFacts_UsesSingleNetworkOperation()
        {
            // Arrange
            var network = new CountingFakeNetwork(output);
            var options = new JinagaClientOptions { QueueProcessingDelay = 50 };
            var j = new JinagaClient(new PersistentMemoryStore(), network, ImmutableList<Specification>.Empty, NullLoggerFactory.Instance, options);
            
            // Act - Create a blog site with content
            var user = await j.Fact(new User("test-user"));
            var site = await j.Fact(new Jinaga.Test.Model.Site(user, "example.com"));
            var content = await j.Fact(new Content(site, "/blog/post1"));
            var publish = await j.Fact(new Publish(content, DateTime.UtcNow));
            
            await Task.Delay(100); // Wait for debounce period
            
            // Assert
            network.SaveCallCount.Should().Be(1); // All facts should be saved in one operation
            network.UploadedFacts.Count.Should().Be(4);
        }
    }
}
