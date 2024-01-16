using System;

namespace Jinaga.Facts
{
    public abstract class FieldValue
    {
        public static FieldValue Undefined { get; } = new FieldValueUndefined();

        public static string ToIso8601String(DateTime dateTime)
        {
            var utcDateTime = dateTime.Kind == DateTimeKind.Utc
                ? dateTime
                : dateTime.ToUniversalTime();
            return utcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }

        public static DateTime FromIso8601String(string str)
        {
            return DateTime.Parse(str).ToUniversalTime();
        }

        public abstract string StringValue { get; }
        public abstract double DoubleValue { get; }
        public abstract bool BoolValue { get; }
    }

    public class FieldValueUndefined : FieldValue
    {
        public override string StringValue => string.Empty;
        public override double DoubleValue => default;
        public override bool BoolValue => false;
    }

    public class FieldValueString : FieldValue
    {
        // This constructor is called via a compiled expression
        public FieldValueString(string stringValue)
        {
            StringValue = stringValue;
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

        public override string StringValue => BoolValue ? "true" : "false";
        public override double DoubleValue => BoolValue ? 1.0 : 0.0;
        public override bool BoolValue { get; }
    }
}