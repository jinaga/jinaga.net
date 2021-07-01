using System;
using System.Collections.Immutable;
using FluentAssertions;
using Jinaga.Facts;
using Jinaga.Test.Model;
using Xunit;

namespace Jinaga.Test.Facts
{
    public class SerializeTest
    {
        [Fact]
        public void SerializeType()
        {
            var fact = FactSerializer.Serialize(new Airline("value"));
            fact.Type.Should().Be("Skylane.Airline");
        }
        
        [Fact]
        public void SerializeField()
        {
            var fact = FactSerializer.Serialize(new Airline("value"));
            var field = fact.Fields.Should().ContainSingle().Subject;
            field.Name.Should().Be("identifier");
            field.Value.Should().BeOfType<FieldValueString>().Which
                .StringValue.Should().Be("value");
        }
    }
}
