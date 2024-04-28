using System;
using Jinaga;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Jinaga.Test.Facts;

public class LocalFactTest
{
    private ITestOutputHelper output;

    public LocalFactTest(ITestOutputHelper output)
    {
        this.output = output;
    }
    
    [Fact]
    public async Task LocalFact_NotUploaded()
    {
        var school = new School(Guid.NewGuid());
        var network = GivenPopulatedNetwork(school);
        var j = GivenJinagaClient(network);

        await j.Local.Fact(new Course(school, "Chemestry 101"));

        network.UploadedFacts.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoteFact_Uploaded()
    {
        var school = new School(Guid.NewGuid());
        var network = GivenPopulatedNetwork(school);
        var j = GivenJinagaClient(network);

        await j.Fact(new Course(school, "Chemestry 101"));

        network.UploadedFacts.Should().HaveCount(2);
    }

    private FakeNetwork GivenPopulatedNetwork(School school)
    {
        FakeNetwork network = new FakeNetwork(output);
        network.AddFeed("courses", new object[]
        {
            school,
            new Course(school, "Computer Science 101"),
            new Course(school, "Computer Science 102")
        });
        return network;
    }

    private static JinagaClient GivenJinagaClient(FakeNetwork network)
    {
        return new JinagaClient(new MemoryStore(), network, NullLoggerFactory.Instance);
    }
}