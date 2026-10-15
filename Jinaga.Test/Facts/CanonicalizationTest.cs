using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using Jinaga.Facts;

namespace Jinaga.Test.Facts
{
    /// <summary>
    /// A fact's hash is its identity, and the hash is taken over the canonical string, so
    /// .NET and jinaga.js have to spell every value the same way or one fact ends up with
    /// two identities. jinaga.js builds its canonical string with <c>JSON.stringify</c> and
    /// template literals, so these tests state what ECMAScript writes for each piece.
    ///
    /// Every expectation below can be checked against a running engine: a number row is
    /// <c>JSON.stringify(value)</c>, and a string row is <c>JSON.stringify(value)</c> for
    /// the quoted piece. They are written out here because CI runs the .NET build alone,
    /// with no engine to ask.
    /// </summary>
    public class CanonicalizationTest
    {
        [Theory]
        // Structural divergence: ECMAScript writes plain decimal out to 1e21.
        [InlineData(1e21, "1e+21")]
        [InlineData(1e20, "100000000000000000000")]
        [InlineData(1e18, "1000000000000000000")]
        [InlineData(1e17, "100000000000000000")]
        // And in the other direction, down to 1e-6.
        [InlineData(1e-7, "1e-7")]
        [InlineData(1e-6, "0.000001")]
        [InlineData(1e-5, "0.00001")]
        // The extremes of the double, where the digit sequence is shortest and longest.
        [InlineData(double.Epsilon, "5e-324")]
        [InlineData(double.MaxValue, "1.7976931348623157e+308")]
        [InlineData(9007199254740993.0, "9007199254740992")]
        // The sign rides outside the layout.
        [InlineData(-1e18, "-1000000000000000000")]
        [InlineData(-1e-7, "-1e-7")]
        // Ordinary values, which must keep the spelling they already had.
        [InlineData(0.0, "0")]
        [InlineData(4247.0, "4247")]
        [InlineData(123.456, "123.456")]
        [InlineData(0.1, "0.1")]
        [InlineData(0.001, "0.001")]
        public void ANumberIsSpelledAsEcmaScriptSpellsIt(double value, string expected)
        {
            CanonicalizeField("value", new FieldValueNumber(value))
                .Should().Be(FieldsOnly("\"value\":" + expected));
        }

        [Fact]
        public void NegativeZeroLosesItsSign()
        {
            // ECMAScript reports the zero case before it reaches the sign.
            CanonicalizeField("value", new FieldValueNumber(-0.0))
                .Should().Be(FieldsOnly("\"value\":0"));
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void ANumberJsonCannotSpellBecomesNull(double value)
        {
            // JSON.stringify writes null for these, which keeps the canonical string
            // parseable rather than carrying a token no JSON reader accepts.
            CanonicalizeField("value", new FieldValueNumber(value))
                .Should().Be(FieldsOnly("\"value\":null"));
        }

        /// <summary>
        /// One row per shape where .NET's own encoders and ECMAScript disagree, plus the
        /// two characters both of them escape. The third element is the quoted piece
        /// <c>JSON.stringify</c> writes for the second.
        /// </summary>
        private static readonly (string Description, string Value, string Expected)[] Corpus =
        {
            // The five C0 controls that have a shorthand, and the rest in lowercase hex.
            (
                "control characters",
                "\u0000\u0001\u0008\u0009\u000a\u000b\u000c\u000d\u000e\u001f",
                "\"\\u0000\\u0001\\b\\t\\n\\u000b\\f\\r\\u000e\\u001f\""
            ),
            // Characters .NET escapes and ECMAScript writes raw.
            ("delete", "a\u007fb", "\"a\u007fb\""),
            ("line and paragraph separators", "a\u2028b\u2029c", "\"a\u2028b\u2029c\""),
            ("byte order mark and no-break space", "a\ufeffb\u00a0c", "\"a\ufeffb\u00a0c\""),
            // An astral character stays one character rather than becoming two escapes.
            ("emoji", "party \U0001F389 time", "\"party \U0001F389 time\""),
            // A surrogate with no mate cannot be written raw, and its hex is lowercase.
            ("unpaired high surrogate", "a\ud83cb", "\"a\\ud83cb\""),
            ("unpaired low surrogate", "a\udf89b", "\"a\\udf89b\""),
            // The two characters that must be escaped, and nothing else beside them.
            ("quote and backslash", "say \"hi\"\\now", "\"say \\\"hi\\\"\\\\now\""),
            // Characters .NET's default encoder escapes.
            ("non-ASCII letter", "Caf\u00e9", "\"Caf\u00e9\""),
            ("angle brackets and plus", "Corp.Model<T>+Nested", "\"Corp.Model<T>+Nested\""),
            ("apostrophe and backtick", "O'Brien`s", "\"O'Brien`s\""),
        };

        public static TheoryData<string, string, string> Strings
        {
            get
            {
                var data = new TheoryData<string, string, string>();
                foreach (var row in Corpus)
                {
                    data.Add(row.Description, row.Value, row.Expected);
                }
                return data;
            }
        }

        /// <summary>
        /// The rows a JSON reader can hand back unchanged. A reader is free to substitute
        /// for an escaped surrogate that has no mate, so those two rows state their escape
        /// directly and take no part in the round trip below.
        /// </summary>
        public static TheoryData<string, string> ReadableStrings
        {
            get
            {
                var data = new TheoryData<string, string>();
                foreach (var row in Corpus)
                {
                    if (!HasUnpairedSurrogate(row.Value))
                    {
                        data.Add(row.Description, row.Value);
                    }
                }
                return data;
            }
        }

        [Fact]
        public void AStringFieldHoldingNullStaysNull()
        {
            // A nullable string property reaches canonicalization as a FieldValueString
            // holding null, so this is the spelling an existing fact already has.
            CanonicalizeField("value", new FieldValueString(null!))
                .Should().Be(FieldsOnly("\"value\":null"));
        }

        [Theory]
        [MemberData(nameof(Strings))]
        public void AStringIsEscapedAsEcmaScriptEscapesIt(string description, string value, string expected)
        {
            CanonicalizeField("value", new FieldValueString(value))
                .Should().Be(FieldsOnly("\"value\":" + expected), description);
        }

        [Theory]
        [MemberData(nameof(Strings))]
        public void AFieldNameIsEscapedTheSameWayItsValueIs(string description, string value, string expected)
        {
            CanonicalizeField(value, new FieldValueString("x"))
                .Should().Be(FieldsOnly(expected + ":\"x\""), description);
        }

        [Theory]
        [MemberData(nameof(Strings))]
        public void APredecessorRoleAndReferenceTypeAreEscapedTheSameWay(string description, string value, string expected)
        {
            var predecessor = new PredecessorSingle(value, new FactReference(value, "aGFzaA=="));

            Canonicalize(ImmutableList<Field>.Empty, ImmutableList.Create<Predecessor>(predecessor))
                .Should().Be(
                    "{\"fields\":{},\"predecessors\":{" +
                    expected + ":{\"hash\":\"aGFzaA==\",\"type\":" + expected + "}" +
                    "}}",
                    description);
        }

        [Theory]
        [MemberData(nameof(ReadableStrings))]
        public void TheCanonicalStringIsJsonThatReadsBackTheFactItDescribes(string description, string value)
        {
            // Derived rather than quoted: a reader that has never seen this writer has to
            // get the same name and the same value back out, which is what a replicator
            // parsing the canonical string does. A name embedded raw, or a value escaped
            // the wrong way, fails here with no expectation spelled out at all.
            var canonical = Canonicalize(
                ImmutableList.Create(new Field(value, new FieldValueString(value))),
                ImmutableList<Predecessor>.Empty);

            using var document = JsonDocument.Parse(canonical);
            var fields = document.RootElement.GetProperty("fields");

            fields.EnumerateObject().Select(field => field.Name).Should().Equal(new[] { value }, description);
            fields.GetProperty(value).GetString().Should().Be(value, description);
        }

        [Fact]
        public void FieldsKeepTheirOrdinalOrder()
        {
            // The sort runs on the name rather than on its escaped spelling, and both
            // platforms order by UTF-16 code unit.
            Canonicalize(
                ImmutableList.Create(
                    new Field("b", new FieldValueNumber(2)),
                    new Field("a", new FieldValueNumber(1)),
                    new Field("A", new FieldValueNumber(3))),
                ImmutableList<Predecessor>.Empty)
                .Should().Be(FieldsOnly("\"A\":3,\"a\":1,\"b\":2"));
        }

        private static bool HasUnpairedSurrogate(string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsHighSurrogate(value[index]))
                {
                    if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                    {
                        return true;
                    }
                    index++;
                }
                else if (char.IsLowSurrogate(value[index]))
                {
                    return true;
                }
            }
            return false;
        }

        private static string CanonicalizeField(string name, FieldValue value)
        {
            return Canonicalize(
                ImmutableList.Create(new Field(name, value)),
                ImmutableList<Predecessor>.Empty);
        }

        private static string Canonicalize(ImmutableList<Field> fields, ImmutableList<Predecessor> predecessors)
        {
            return Fact.Canonicalize(fields, predecessors);
        }

        private static string FieldsOnly(string fields)
        {
            return "{\"fields\":{" + fields + "},\"predecessors\":{}}";
        }
    }
}
