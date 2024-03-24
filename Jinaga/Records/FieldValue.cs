namespace Jinaga.Records
{
    public abstract class FieldValue
    {
        public static FieldValue Null { get; } = new FieldValueNull();

        public static FieldValueString From(string value)
        {
            return new FieldValueString
            {
                Value = value
            };
        }

        public static FieldValueBoolean From(bool value)
        {
            return new FieldValueBoolean
            {
                Value = value
            };
        }

        public static FieldValueNumber From(double value)
        {
            return new FieldValueNumber
            {
                Value = value
            };
        }
    }
}