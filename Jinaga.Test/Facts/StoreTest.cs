using Jinaga;
using Jinaga.Test.Model;

namespace Jinaga.Test.Facts
{
    public class StoreTest
    {
        [Fact]
        public async Task StoreRoundTripToUTC()
        {
            DateTime now = DateTime.Parse("2021-07-04T01:39:43.241Z");
            var j = JinagaTest.Create();
            var airlineDay = await j.Fact(new AirlineDay(new Airline("value"), now));
            airlineDay.date.Kind.Should().Be(DateTimeKind.Utc);
            airlineDay.date.Hour.Should().Be(1);
        }

        [Fact]
        public async Task StoreRoundTripFromUTC()
        {
            DateTime now = DateTime.Parse("2021-07-04T01:39:43.241Z").ToUniversalTime();
            var j = JinagaTest.Create();
            var airlineDay = await j.Fact(new AirlineDay(new Airline("value"), now));
            airlineDay.date.Kind.Should().Be(DateTimeKind.Utc);
            airlineDay.date.Hour.Should().Be(1);
        }

        [Fact]
        public async Task CannotSaveFactWithType()
        {
            var j = JinagaTest.Create();
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await j.Fact(new FactWithType("value"));
            });
        }
    }
}

[FactType("FactWithType")]
public record FactWithType(string type) { }