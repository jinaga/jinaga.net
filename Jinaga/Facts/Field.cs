namespace Jinaga.Facts
{
    public class Field
    {
        public Field(string name, FieldValue value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public FieldValue Value { get; }
    }
}