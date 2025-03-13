using System.Globalization;

namespace Jinaga.Test.Facts;

[FactType("Tests.Nullable.Decimal")]
internal record NullableDecimal(decimal? Value);

[FactType("Tests.Nullable.Integer")]
internal record NullableInteger(int? Value);

[FactType("Tests.Nullable.String")]
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
internal record NullableString(string? Value);
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

[FactType("Tests.Nullable.DateTime")]
internal record NullableDateTime(DateTime? Value);

[FactType("Tests.Nullable.DateTimeOffset")]
internal record NullableDateTimeOffset(DateTimeOffset? Value);

[FactType("Tests.Nullable.Guid")]
internal record NullableGuid(Guid? Value);

[FactType("Tests.Nullable.Boolean")]
internal record NullableBoolean(bool? Value);


public class NullableTests
{
    [Fact]
    public async Task TestNullableDecimalWithValue()
    {
        var j = JinagaTest.Create();

        var zero = await j.Fact(new NullableDecimal(0m));

        zero.Value.Should().Be(0m);
    }

    [Fact]
    public async Task TestNullableDecimalWithNull()
    {
        var j = JinagaTest.Create();

        var nullValue = await j.Fact(new NullableDecimal(null));

        nullValue.Value.Should().BeNull();
    }

    [Fact]
    public async Task TestNullableIntegerWithValue()
    {
        var j = JinagaTest.Create();

        var zero = await j.Fact(new NullableInteger(0));

        zero.Value.Should().Be(0);
    }

    [Fact]
    public async Task TestNullableIntegerWithNull()
    {
        var j = JinagaTest.Create();

        var nullValue = await j.Fact(new NullableInteger(null));

        nullValue.Value.Should().BeNull();
    }

    [Fact]
    public async Task TestNullableStringWithValue()
    {
        var j = JinagaTest.Create();

        var empty = await j.Fact(new NullableString(""));

        empty.Value.Should().Be("");
    }

    [Fact]
    public async Task TestNullableStringWithNull()
    {
        var j = JinagaTest.Create();

        var nullValue = await j.Fact(new NullableString(null));

        nullValue.Value.Should().BeNull();
    }

    [Fact]
    public async Task TestNullableDateTimeWithValue()
    {
        var j = JinagaTest.Create();

        var date = DateTime.ParseExact("2021-01-01", "yyyy-MM-dd", CultureInfo.InvariantCulture).ToUniversalTime();
        var nonNull = await j.Fact(new NullableDateTime(date));

        nonNull.Value.Should().Be(date);
    }

    [Fact]
    public async Task TestNullableDateTimeWithNull()
    {
        var j = JinagaTest.Create();

        var nullValue = await j.Fact(new NullableDateTime(null));

        nullValue.Value.Should().BeNull();
    }

    [Fact]
    public async Task TestNullableDateTimeOffsetWithValue()
    {
        var j = JinagaTest.Create();

        var dateTimeOffset = DateTimeOffset.ParseExact("2021-01-01T00:00:00.000+00:00", "yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture);
        var nonNull = await j.Fact(new NullableDateTimeOffset(dateTimeOffset));

        nonNull.Value.Should().Be(dateTimeOffset);
    }

    [Fact]
    public async Task TestNullableDateTimeOffsetWithNull()
    {
        var j = JinagaTest.Create();

        var nullValue = await j.Fact(new NullableDateTimeOffset(null));

        nullValue.Value.Should().BeNull();
    }

    [Fact]
    public async Task TestNullableGuidWithValue()
    {
        var j = JinagaTest.Create();

        var zero = await j.Fact(new NullableGuid(Guid.Empty));

        zero.Value.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task TestNullableGuidWithNull()
    {
        var j = JinagaTest.Create();

        var nullValue = await j.Fact(new NullableGuid(null));

        nullValue.Value.Should().BeNull();
    }

    [Fact]
    public async Task TestNullableBooleanWithValue()
    {
        var j = JinagaTest.Create();

        var zero = await j.Fact(new NullableBoolean(false));

        zero.Value.Should().BeFalse();
    }

    [Fact]
    public async Task TestNullableBooleanWithNull()
    {
        var j = JinagaTest.Create();

        var nullValue = await j.Fact(new NullableBoolean(null));

        nullValue.Value.Should().BeNull();
    }
}