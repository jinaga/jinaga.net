namespace Jinaga.Test.Facts;

[FactType("Tests.Nullable.Decimal")]
internal record NullableDecimal(decimal? Value);

public class NullableTests
{
    [Fact]
    public async Task TestNullableDecimal()
    {
        var j = JinagaTest.Create();

        // Both of these calls throw exceptions.
        var zero = await j.Fact(new NullableDecimal(0m));
        var nullValue = await j.Fact(new NullableDecimal(null));

        zero.Value.Should().Be(0m);
        nullValue.Value.Should().BeNull();
    }
}