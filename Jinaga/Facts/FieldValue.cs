namespace Jinaga.Facts
{
    public class FieldValue
    {
    }

    public class FieldValueString : FieldValue
    {
        public FieldValueString(string stringValue)
        {
            StringValue = stringValue;
        }

        public string StringValue { get; }
    }
}