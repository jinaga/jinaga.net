using System;

namespace Jinaga.Facts
{
    public abstract class FieldValue
    {
        public static FieldValue Undefined { get; } = new FieldValueUndefined();
        public static FieldValue Null { get; } = new FieldValueNull();

        public static string DateTimeToIso8601String(DateTime dateTime)
        {
            var utcDateTime = dateTime.Kind == DateTimeKind.Utc
                ? dateTime
                : dateTime.ToUniversalTime();
            return utcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }

        public static string? NullableDateTimeToNullableIso8601String(DateTime? dateTime)
        {
            return dateTime.HasValue
                ? DateTimeToIso8601String(dateTime.Value)
                : null;
        }

        public static string DateTimeOffsetToIso8601String(DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
        }

        public static string? NullableDateTimeOffsetToNullableIso8601String(DateTimeOffset? dateTimeOffset)
        {
            return dateTimeOffset.HasValue
                ? DateTimeOffsetToIso8601String(dateTimeOffset.Value)
                : null;
        }

        public static string TimeSpanToIso8601String(TimeSpan timeSpan)
        {
            return timeSpan.ToString("c");
        }

        public static string? NullableTimeSpanToNullableIso8601String(TimeSpan? timeSpan)
        {
            return timeSpan.HasValue
                ? TimeSpanToIso8601String(timeSpan.Value)
                : null;
        }

        public static DateTime FromIso8601String(string str)
        {
            return DateTime.TryParse(str, out var dateTime)
                ? dateTime.ToUniversalTime()
                : DateTime.UnixEpoch;
        }

        public static DateTime? FromNullableIso8601String(string str)
        {
            return string.IsNullOrEmpty(str)
                ? (DateTime?)null
                : DateTime.TryParse(str, out var dateTime)
                ? dateTime.ToUniversalTime()
                : (DateTime?)null;
        }

        public static DateTimeOffset FromIso8601StringToDateTimeOffset(string str)
        {
            return DateTimeOffset.TryParse(str, out var dateTimeOffset)
                ? dateTimeOffset
                : DateTimeOffset.UnixEpoch;
        }

        public static DateTimeOffset? FromNullableIso8601StringToNullableDateTimeOffset(string str)
        {
            return string.IsNullOrEmpty(str)
                ? (DateTimeOffset?)null
                : DateTimeOffset.TryParse(str, out var dateTimeOffset)
                ? dateTimeOffset
                : (DateTimeOffset?)null;
        }

        public static TimeSpan FromIso8601StringToTimeSpan(string str)
        {
            return TimeSpan.TryParse(str, out var timeSpan)
                ? timeSpan
                : TimeSpan.Zero;
        }

        public static TimeSpan? FromNullableIso8601StringToNullableTimeSpan(string str)
        {
            return string.IsNullOrEmpty(str)
                ? (TimeSpan?)null
                : TimeSpan.TryParse(str, out var timeSpan)
                ? timeSpan
                : (TimeSpan?)null;
        }

        public static string GuidToString(Guid guid)
        {
            return guid.ToString("D");
        }

        public static string? NullableGuidToNullableString(Guid? guid)
        {
            return guid.HasValue
                ? guid.Value.ToString("D")
                : null;
        }

        public static Guid GuidFromString(string str)
        {
            return Guid.TryParse(str, out var guid)
                ? guid
                : Guid.Empty;
        }

        public static Guid? FromNullableGuidString(string str)
        {
            return string.IsNullOrEmpty(str)
                ? (Guid?)null
                : Guid.TryParse(str, out var guid)
                ? guid
                : (Guid?)null;
        }

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