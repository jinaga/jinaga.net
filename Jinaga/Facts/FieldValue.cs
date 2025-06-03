using System;

namespace Jinaga.Facts
{
    /// <summary>
    /// Represents a field value in a fact, providing conversion methods between .NET types and their serialized representations.
    /// </summary>
    public abstract class FieldValue
    {
        public static FieldValue Undefined { get; } = new FieldValueUndefined();
        public static FieldValue Null { get; } = new FieldValueNull();

        #region DateTime Conversion Methods

        /// <summary>
        /// Converts a DateTime to ISO 8601 string format in UTC.
        /// </summary>
        /// <param name="dateTime">The DateTime to convert.</param>
        /// <returns>ISO 8601 formatted string in UTC.</returns>
        public static string DateTimeToIso8601String(DateTime dateTime)
        {
            var utcDateTime = dateTime.Kind == DateTimeKind.Utc
                ? dateTime
                : dateTime.ToUniversalTime();
            return utcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }

        /// <summary>
        /// Converts a nullable DateTime to nullable ISO 8601 string format.
        /// </summary>
        /// <param name="dateTime">The nullable DateTime to convert.</param>
        /// <returns>ISO 8601 formatted string or null.</returns>
        public static string? NullableDateTimeToNullableIso8601String(DateTime? dateTime)
        {
            return dateTime.HasValue
                ? DateTimeToIso8601String(dateTime.Value)
                : null;
        }

        /// <summary>
        /// Converts an ISO 8601 string to DateTime in UTC.
        /// </summary>
        /// <param name="str">The ISO 8601 string to parse.</param>
        /// <returns>DateTime in UTC, or Unix epoch if parsing fails.</returns>
        public static DateTime FromIso8601String(string str)
        {
            return DateTime.TryParse(str, out var dateTime)
                ? dateTime.ToUniversalTime()
                : DateTime.UnixEpoch;
        }

        /// <summary>
        /// Converts an ISO 8601 string to nullable DateTime.
        /// </summary>
        /// <param name="str">The ISO 8601 string to parse.</param>
        /// <returns>DateTime in UTC, or null if string is empty or parsing fails.</returns>
        public static DateTime? FromNullableIso8601String(string str)
        {
            return string.IsNullOrEmpty(str)
                ? (DateTime?)null
                : DateTime.TryParse(str, out var dateTime)
                ? dateTime.ToUniversalTime()
                : (DateTime?)null;
        }

        #endregion

        #region DateTimeOffset Conversion Methods

        /// <summary>
        /// Converts a DateTimeOffset to ISO 8601 string format with timezone offset.
        /// </summary>
        /// <param name="dateTimeOffset">The DateTimeOffset to convert.</param>
        /// <returns>ISO 8601 formatted string with timezone offset.</returns>
        public static string DateTimeOffsetToIso8601String(DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
        }

        /// <summary>
        /// Converts a nullable DateTimeOffset to nullable ISO 8601 string format.
        /// </summary>
        /// <param name="dateTimeOffset">The nullable DateTimeOffset to convert.</param>
        /// <returns>ISO 8601 formatted string or null.</returns>
        public static string? NullableDateTimeOffsetToNullableIso8601String(DateTimeOffset? dateTimeOffset)
        {
            return dateTimeOffset.HasValue
                ? DateTimeOffsetToIso8601String(dateTimeOffset.Value)
                : null;
        }

        /// <summary>
        /// Converts an ISO 8601 string to DateTimeOffset.
        /// </summary>
        /// <param name="str">The ISO 8601 string to parse.</param>
        /// <returns>DateTimeOffset, or Unix epoch if parsing fails.</returns>
        public static DateTimeOffset FromIso8601StringToDateTimeOffset(string str)
        {
            return DateTimeOffset.TryParse(str, out var dateTimeOffset)
                ? dateTimeOffset
                : DateTimeOffset.UnixEpoch;
        }

        /// <summary>
        /// Converts an ISO 8601 string to nullable DateTimeOffset.
        /// </summary>
        /// <param name="str">The ISO 8601 string to parse.</param>
        /// <returns>DateTimeOffset, or null if string is empty or parsing fails.</returns>
        public static DateTimeOffset? FromNullableIso8601StringToNullableDateTimeOffset(string str)
        {
            return string.IsNullOrEmpty(str)
                ? (DateTimeOffset?)null
                : DateTimeOffset.TryParse(str, out var dateTimeOffset)
                ? dateTimeOffset
                : (DateTimeOffset?)null;
        }

        #endregion

        #region TimeSpan Conversion Methods

        /// <summary>
        /// Converts a TimeSpan to ISO 8601 duration string format.
        /// </summary>
        /// <param name="timeSpan">The TimeSpan to convert.</param>
        /// <returns>ISO 8601 duration formatted string.</returns>
        public static string TimeSpanToIso8601String(TimeSpan timeSpan)
        {
            return timeSpan.ToString("c");
        }

        /// <summary>
        /// Converts a nullable TimeSpan to nullable ISO 8601 duration string format.
        /// </summary>
        /// <param name="timeSpan">The nullable TimeSpan to convert.</param>
        /// <returns>ISO 8601 duration formatted string or null.</returns>
        public static string? NullableTimeSpanToNullableIso8601String(TimeSpan? timeSpan)
        {
            return timeSpan.HasValue
                ? TimeSpanToIso8601String(timeSpan.Value)
                : null;
        }

        /// <summary>
        /// Converts an ISO 8601 duration string to TimeSpan.
        /// </summary>
        /// <param name="str">The ISO 8601 duration string to parse.</param>
        /// <returns>TimeSpan, or zero if parsing fails.</returns>
        public static TimeSpan FromIso8601StringToTimeSpan(string str)
        {
            return TimeSpan.TryParse(str, out var timeSpan)
                ? timeSpan
                : TimeSpan.Zero;
        }

        /// <summary>
        /// Converts an ISO 8601 duration string to nullable TimeSpan.
        /// </summary>
        /// <param name="str">The ISO 8601 duration string to parse.</param>
        /// <returns>TimeSpan, or null if string is empty or parsing fails.</returns>
        public static TimeSpan? FromNullableIso8601StringToNullableTimeSpan(string str)
        {
            return string.IsNullOrEmpty(str)
                ? (TimeSpan?)null
                : TimeSpan.TryParse(str, out var timeSpan)
                ? timeSpan
                : (TimeSpan?)null;
        }

        #endregion

        #region Guid Conversion Methods

        /// <summary>
        /// Converts a Guid to string format using the "D" format specifier.
        /// </summary>
        /// <param name="guid">The Guid to convert.</param>
        /// <returns>String representation of the Guid.</returns>
        public static string GuidToString(Guid guid)
        {
            return guid.ToString("D");
        }

        /// <summary>
        /// Converts a nullable Guid to nullable string format.
        /// </summary>
        /// <param name="guid">The nullable Guid to convert.</param>
        /// <returns>String representation of the Guid or null.</returns>
        public static string? NullableGuidToNullableString(Guid? guid)
        {
            return guid.HasValue
                ? guid.Value.ToString("D")
                : null;
        }

        /// <summary>
        /// Converts a string to Guid.
        /// </summary>
        /// <param name="str">The string to parse.</param>
        /// <returns>Guid, or empty Guid if parsing fails.</returns>
        public static Guid GuidFromString(string str)
        {
            return Guid.TryParse(str, out var guid)
                ? guid
                : Guid.Empty;
        }

        /// <summary>
        /// Converts a string to nullable Guid.
        /// </summary>
        /// <param name="str">The string to parse.</param>
        /// <returns>Guid, or null if string is empty or parsing fails.</returns>
        public static Guid? FromNullableGuidString(string str)
        {
            return string.IsNullOrEmpty(str)
                ? (Guid?)null
                : Guid.TryParse(str, out var guid)
                ? guid
                : (Guid?)null;
        }

        #endregion

        public abstract string StringValue { get; }
        public abstract double DoubleValue { get; }
        public abstract bool BoolValue { get; }
        public virtual bool IsNull => false;
    }

    public class FieldValueUndefined : FieldValue
    {
        public override string StringValue => string.Empty;
        public override double DoubleValue => default;
        public override bool BoolValue => false;
        public override bool IsNull => true;
    }

    public class FieldValueString : FieldValue
    {
        // This constructor is called via a compiled expression
        public FieldValueString(string stringValue)
        {
            StringValue = stringValue;
        }

        // This method is called via a compiled expression
        public static FieldValue From(string? nullableString)
        {
            return nullableString != null
                ? new FieldValueString(nullableString)
                : FieldValue.Null;
        }

        public override string StringValue { get; }
        public override double DoubleValue => double.TryParse(StringValue, out var doubleValue) ? doubleValue : (double)default;
        public override bool BoolValue => bool.TryParse(StringValue, out var boolValue) ? boolValue : (bool)default;
    }

    public class FieldValueNumber : FieldValue
    {
        // This constructor is called via a compiled expression
        public FieldValueNumber(double doubleValue)
        {
            DoubleValue = doubleValue;
        }

        // This method is called via a compiled expression
        public static FieldValue From(double? nullableDouble)
        {
            return nullableDouble.HasValue
                ? new FieldValueNumber(nullableDouble.Value)
                : FieldValue.Null;
        }

        public override string StringValue => string.Format("{0}", DoubleValue);
        public override double DoubleValue { get; }
        public override bool BoolValue => DoubleValue != 0.0;
    }

    public class FieldValueBoolean : FieldValue
    {
        // This constructor is called via a compiled expression
        public FieldValueBoolean(bool boolValue)
        {
            BoolValue = boolValue;
        }

        // This method is called via a compiled expression
        public static FieldValue From(bool? nullableBool)
        {
            return nullableBool.HasValue
                ? new FieldValueBoolean(nullableBool.Value)
                : FieldValue.Null;
        }

        public override string StringValue => BoolValue ? "true" : "false";
        public override double DoubleValue => BoolValue ? 1.0 : 0.0;
        public override bool BoolValue { get; }
    }

    public class FieldValueNull : FieldValue
    {
        public override string StringValue => null!;
        public override double DoubleValue => default;
        public override bool BoolValue => false;
        public override bool IsNull => true;
    }
}