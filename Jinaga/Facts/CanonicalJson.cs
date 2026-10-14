using System;
using System.Globalization;
using System.Text;

namespace Jinaga.Facts
{
    /// <summary>
    /// Writes the scalar pieces of a fact's canonical string the way ECMAScript does.
    ///
    /// A fact's hash is its identity, and the hash is taken over the canonical string, so
    /// every platform that computes it has to spell the same value the same way. jinaga.js
    /// builds its canonical string with <c>JSON.stringify</c> and template literals, which
    /// means its string escaping is <c>JSON.stringify</c>'s and its number formatting is
    /// <c>Number::toString</c>'s. Neither is what .NET's own serializers produce, so these
    /// two methods implement those two ECMAScript operations directly.
    ///
    /// Use them for every string and every number that reaches the canonical string: field
    /// values, field names, predecessor roles, and predecessor reference types. A piece
    /// written any other way forks the fact's identity for the values where the two
    /// spellings differ.
    /// </summary>
    internal static class CanonicalJson
    {
        /// <summary>
        /// Writes a string as <c>JSON.stringify</c> does: quoted, with only <c>"</c>,
        /// <c>\</c>, the C0 control characters, and unpaired surrogates escaped. Every
        /// other character is written raw, so an emoji stays an emoji rather than becoming
        /// a surrogate-pair escape.
        /// </summary>
        public static string SerializeString(string value)
        {
            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    default:
                        // The remaining C0 controls have no shorthand, and a surrogate with
                        // no mate cannot be written raw, because JSON.stringify is well
                        // formed. Everything else, including U+007F, U+00A0, U+2028, U+2029
                        // and every astral character, is written as it stands.
                        if (character < ' ' || (char.IsSurrogate(character) && !IsPaired(value, index)))
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }
                        break;
                }
            }
            builder.Append('"');
            return builder.ToString();
        }

        /// <summary>
        /// Writes a number as ECMAScript's <c>Number::toString</c> does, which is what
        /// <c>JSON.stringify</c> writes for a finite number: the shortest digit sequence
        /// that round-trips, laid out in plain decimal while the decimal point lands within
        /// the window ECMAScript allows, and in exponential form with a lowercase
        /// <c>e</c> and an unpadded exponent outside it.
        /// </summary>
        public static string SerializeNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                // JSON.stringify writes null for a number JSON cannot spell.
                return "null";
            }
            if (value == 0.0)
            {
                // Negative zero prints as "0", because ECMAScript's Number::toString
                // reports it as the zero case before it reaches the sign.
                return "0";
            }
            return value < 0.0
                ? "-" + FormatPositive(-value)
                : FormatPositive(value);
        }

        private static bool IsPaired(string value, int index)
        {
            return char.IsHighSurrogate(value[index])
                ? index + 1 < value.Length && char.IsLowSurrogate(value[index + 1])
                : index > 0 && char.IsHighSurrogate(value[index - 1]);
        }

        private static string FormatPositive(double value)
        {
            // ECMAScript's Number::toString is defined over the shortest digit sequence
            // that round-trips, which is exactly what the "R" format produces here, and
            // over n, the position of the decimal point relative to the first of those
            // digits. The layout rules below are that operation's own cases, in its order.
            SignificantDigits(value.ToString("R", CultureInfo.InvariantCulture), out string digits, out int n);
            int k = digits.Length;

            if (k <= n && n <= 21)
            {
                return digits + new string('0', n - k);
            }
            if (0 < n && n <= 21)
            {
                return digits.Substring(0, n) + "." + digits.Substring(n);
            }
            if (-6 < n && n <= 0)
            {
                return "0." + new string('0', -n) + digits;
            }
            string exponent = (n > 0 ? "e+" : "e-") +
                Math.Abs(n - 1).ToString(CultureInfo.InvariantCulture);
            return k == 1
                ? digits + exponent
                : digits.Substring(0, 1) + "." + digits.Substring(1) + exponent;
        }

        private static void SignificantDigits(string roundTrip, out string digits, out int n)
        {
            int exponentIndex = roundTrip.IndexOfAny(new[] { 'E', 'e' });
            string mantissa = exponentIndex < 0 ? roundTrip : roundTrip.Substring(0, exponentIndex);
            int exponent = exponentIndex < 0
                ? 0
                : int.Parse(
                    roundTrip.Substring(exponentIndex + 1),
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture);

            int pointIndex = mantissa.IndexOf('.');
            int integerLength = pointIndex < 0 ? mantissa.Length : pointIndex;
            string allDigits = pointIndex < 0 ? mantissa : mantissa.Remove(pointIndex, 1);

            int leadingZeros = 0;
            while (leadingZeros < allDigits.Length - 1 && allDigits[leadingZeros] == '0')
            {
                leadingZeros++;
            }

            digits = allDigits.Substring(leadingZeros).TrimEnd('0');
            n = integerLength + exponent - leadingZeros;
        }
    }
}
