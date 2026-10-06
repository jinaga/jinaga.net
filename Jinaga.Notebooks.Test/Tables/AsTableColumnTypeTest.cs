using Microsoft.Data.Analysis;

namespace Jinaga.Notebooks.Test.Tables;

/// <summary>
/// A column's type is what lets a notebook sort, filter and aggregate it. A
/// number rendered into a string column can do none of those, and its text
/// depends on the current culture. So each numeric property type is pinned to
/// the column type it must produce, and each cell is read back as a number
/// rather than as text. An enum carries no numeric order a reader would want,
/// so it stays the member name, pinned here so it stays that way.
/// </summary>
public class AsTableColumnTypeTest
{
    [Fact]
    public void ALongGivesAnInt64Column()
    {
        var table = Measures().AsTable();

        table.Columns["Count"].Should().BeOfType<Int64DataFrameColumn>();
        table.Columns["OptionalCount"].Should().BeOfType<Int64DataFrameColumn>();
        table.Columns["Count"][0].Should().Be(7L);
        table.Columns["OptionalCount"][0].Should().Be(8L);
    }

    [Fact]
    public void AFloatGivesASingleColumn()
    {
        var table = Measures().AsTable();

        table.Columns["Ratio"].Should().BeOfType<SingleDataFrameColumn>();
        table.Columns["OptionalRatio"].Should().BeOfType<SingleDataFrameColumn>();
        table.Columns["Ratio"][0].Should().Be(1.5f);
        table.Columns["OptionalRatio"][0].Should().Be(2.5f);
    }

    [Fact]
    public void ADecimalGivesADecimalColumn()
    {
        var table = Measures().AsTable();

        table.Columns["Amount"].Should().BeOfType<DecimalDataFrameColumn>();
        table.Columns["OptionalAmount"].Should().BeOfType<DecimalDataFrameColumn>();
        table.Columns["Amount"][0].Should().Be(1.25m);
        table.Columns["OptionalAmount"][0].Should().Be(2.25m);
    }

    [Fact]
    public void ANullInANullableNumericIsANullCell()
    {
        var table = Measures().AsTable();

        table.Columns["OptionalCount"][1].Should().BeNull();
        table.Columns["OptionalRatio"][1].Should().BeNull();
        table.Columns["OptionalAmount"][1].Should().BeNull();
    }

    [Fact]
    public void AnEnumIsShownByItsName()
    {
        var table = new[]
        {
            new Alert { Level = Severity.High }
        }.AsTable();

        table.Columns["Level"].Should().BeOfType<StringDataFrameColumn>();
        table.Columns["Level"][0].Should().Be("High");
    }

    /// <summary>
    /// Each numeric kind in both its plain and its nullable form. The second row
    /// leaves every nullable one empty, so a null has a row to land in.
    /// </summary>
    private static IEnumerable<Measure> Measures() => new[]
    {
        new Measure
        {
            Count = 7L,
            OptionalCount = 8L,
            Ratio = 1.5f,
            OptionalRatio = 2.5f,
            Amount = 1.25m,
            OptionalAmount = 2.25m
        },
        new Measure
        {
            Count = 9L,
            OptionalCount = null,
            Ratio = 3.5f,
            OptionalRatio = null,
            Amount = 3.25m,
            OptionalAmount = null
        }
    };

    public class Measure
    {
        public long Count { get; set; }
        public long? OptionalCount { get; set; }
        public float Ratio { get; set; }
        public float? OptionalRatio { get; set; }
        public decimal Amount { get; set; }
        public decimal? OptionalAmount { get; set; }
    }

    public enum Severity
    {
        Low,
        High
    }

    public class Alert
    {
        public Severity Level { get; set; }
    }
}
