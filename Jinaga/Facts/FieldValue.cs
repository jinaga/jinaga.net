using System;

namespace Jinaga.Facts
{
    public abstract class FieldValue
    {
        public static string ToIso8601String(DateTime dateTime)
        {
            var utcDateTime = dateTime.Kind == DateTimeKind.Utc
                ? dateTime
                : dateTime.ToUniversalTime();
            return utcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }
    }

    public class FieldValueString : FieldValue
    {
        public FieldValueString(string stringValue)
        {
            StringValue = stringValue;
        }

        public string StringValue { get; }
    }

    public class FieldValueNumber : FieldValue
    {
        public FieldValueNumber(double doubleValue)
        {
            DoubleValue = doubleValue;
        }

        public double DoubleValue { get; }
    }

    public class FieldValueBoolean : FieldValue
    {
        public FieldValueBoolean(bool boolValue)
        {
            BoolValue = boolValue;
        }

        public bool BoolValue { get; }
    }
}