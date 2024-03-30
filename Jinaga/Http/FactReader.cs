using Jinaga.Facts;
using Jinaga.Records;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Http
{
    internal class FactReader
    {

        public static Fact ReadFact(FactRecord fact)
        {
            return Fact.Create(
                fact.Type,
                fact.Fields.Select(field => ReadField(field)).ToImmutableList(),
                fact.Predecessors.Select(predecessor => ReadPredecessor(predecessor)).ToImmutableList()
            );
        }

        private static Field ReadField(KeyValuePair<string, Records.FieldValue> field)
        {
            return new Field(field.Key, ReadFieldValue(field.Value));
        }

        private static Facts.FieldValue ReadFieldValue(Records.FieldValue value)
        {
            if (value is Records.FieldValueString stringValue)
            {
                return new Facts.FieldValueString(stringValue.Value);
            }
            else if (value is Records.FieldValueNumber numberValue)
            {
                return new Facts.FieldValueNumber(numberValue.Value);
            }
            else if (value is Records.FieldValueBoolean booleanValue)
            {
                return new Facts.FieldValueBoolean(booleanValue.Value);
            }
            else if (value is Records.FieldValueNull)
            {
                return Facts.FieldValue.Null;
            }
            else
            {
                throw new ArgumentException($"Unknown value type {value.GetType().Name}");
            }
        }

        private static Predecessor ReadPredecessor(KeyValuePair<string, PredecessorSet> pair)
        {
            if (pair.Value is PredecessorSetSingle single)
            {
                return new PredecessorSingle(pair.Key, ReadFactReference(single.Reference));
            }
            else if (pair.Value is PredecessorSetMultiple multiple)
            {
                return new PredecessorMultiple(pair.Key, multiple.References
                    .Select(r => ReadFactReference(r)).ToImmutableList());
            }
            else
            {
                throw new ArgumentException($"Unknown predecessor set type {pair.Value.GetType().Name}");
            }
        }

        public static Facts.FactReference ReadFactReference(Records.FactReference reference)
        {
            return new Facts.FactReference(reference.Type, reference.Hash);
        }
    }
}
