using System.Collections;
using System.Reflection;

using Microsoft.Data.Analysis;

namespace Jinaga.Notebooks.Test.Tables;

/// <summary>
/// A table is a projection of one sequence, so the sequence is read once. A
/// column built from a second reading of a lazy source gives a row values that
/// no single item ever carried, and a source that cannot be read twice gives no
/// table at all. Both tests drive the extension through a source that reports
/// how many times it was read, rather than asserting on the shape of the code.
/// </summary>
public class AsTableEnumerationTest
{
    [Fact]
    public void ASourceThatCannotBeReadTwiceFillsEveryColumn()
    {
        var source = new OneShotSequence<Reading>(new[] { Reading.At(1), Reading.At(2) });

        var table = source.AsTable();

        table.Columns.Select(column => column.Name).Should().BeEquivalentTo(
            Properties<Reading>().Select(property => property.Name),
            "the table should carry one column per property of the item type");
        foreach (var column in table.Columns)
        {
            column.Length.Should().Be(2,
                "column {0} should hold a value for every item in the source", column.Name);
            for (long row = 0; row < column.Length; row++)
                column[row].Should().NotBeNull(
                    "column {0} row {1} should be populated", column.Name, row);
        }
    }

    [Fact]
    public void EveryColumnOfARowComesFromOneReading()
    {
        var table = new StampedSequence().AsTable();

        for (long row = 0; row < 2; row++)
        {
            var reported = Properties<Stamped>()
                .Select(property => (property.Name, Reading: Stamped.ReadingOf(property.Name, table[property.Name][row])))
                .ToList();

            reported.Select(entry => entry.Reading).Distinct().Should().HaveCount(1,
                "every column of row {0} should carry the same reading of the source, but they report {1}",
                row, string.Join(", ", reported.Select(entry => $"{entry.Name}={entry.Reading}")));
        }
    }

    private static PropertyInfo[] Properties<T>() =>
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    /// <summary>
    /// One property of every kind <c>AsTable</c> gives a typed column, plus one it
    /// renders as text, so "every column" means every branch of the projection.
    /// </summary>
    public class Reading
    {
        public string Label { get; set; } = "";
        public int Count { get; set; }
        public double Average { get; set; }
        public bool Valid { get; set; }
        public DateTime TakenAt { get; set; }
        public Guid Token { get; set; }
        public TimeSpan Elapsed { get; set; }

        public static Reading At(int index) => new Reading
        {
            Label = $"reading {index}",
            Count = index,
            Average = index,
            Valid = index % 2 == 1,
            TakenAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(index),
            Token = new Guid($"00000000-0000-0000-0000-{index:D12}"),
            Elapsed = TimeSpan.FromMinutes(index)
        };
    }

    /// <summary>
    /// Every property encodes which reading of the source produced it, so a row
    /// assembled from two readings disagrees with itself.
    /// </summary>
    public class Stamped
    {
        public static readonly DateTime Origin = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public string Label { get; set; } = "";
        public int Count { get; set; }
        public double Average { get; set; }
        public DateTime TakenAt { get; set; }
        public Guid Token { get; set; }
        public TimeSpan Elapsed { get; set; }

        public static Stamped From(int reading) => new Stamped
        {
            Label = $"reading {reading}",
            Count = reading,
            Average = reading,
            TakenAt = Origin.AddDays(reading),
            Token = new Guid($"00000000-0000-0000-0000-{reading:D12}"),
            Elapsed = TimeSpan.FromMinutes(reading)
        };

        /// <summary>
        /// Recovers the reading a cell was stamped with. A property with no decoder
        /// throws, so adding one to <see cref="Stamped"/> cannot silently go unchecked.
        /// </summary>
        public static int ReadingOf(string propertyName, object? cell) => propertyName switch
        {
            nameof(Label) => int.Parse(((string)cell!).Split(' ').Last()),
            nameof(Count) => (int)cell!,
            nameof(Average) => (int)(double)cell!,
            nameof(TakenAt) => ((DateTime)cell! - Origin).Days,
            nameof(Token) => int.Parse(((Guid)cell!).ToString().Split('-').Last()),
            nameof(Elapsed) => (int)TimeSpan.Parse((string)cell!).TotalMinutes,
            _ => throw new InvalidOperationException(
                $"{propertyName} carries no reading, so this test cannot tell which reading filled its column.")
        };
    }

    /// <summary>
    /// A sequence that can be read only once, as a deferred query over a forward-only
    /// reader is.
    /// </summary>
    private sealed class OneShotSequence<T> : IEnumerable<T>
    {
        private readonly IReadOnlyList<T> items;
        private int readings;

        public OneShotSequence(IReadOnlyList<T> items)
        {
            this.items = items;
        }

        public IEnumerator<T> GetEnumerator()
        {
            readings++;
            if (readings > 1)
                throw new InvalidOperationException(
                    $"This sequence was read {readings} times, and it can be read only once.");
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A sequence whose items change with every reading, as a generator over a
    /// clock or a counter does.
    /// </summary>
    private sealed class StampedSequence : IEnumerable<Stamped>
    {
        private int readings;

        public IEnumerator<Stamped> GetEnumerator()
        {
            readings++;
            return Items(readings).GetEnumerator();
        }

        private static IEnumerable<Stamped> Items(int reading)
        {
            yield return Stamped.From(reading);
            yield return Stamped.From(reading);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
