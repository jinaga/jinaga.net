using System.Collections.Generic;
using FluentAssertions;
using Jinaga.Http;
using Jinaga.Records;
using Xunit;

namespace Jinaga.Test.Http
{
    public class MessageSerializerTest
    {
        private const string ExampleLoginResponse = "{\"userFact\":{\"type\":\"Jinaga.User\",\"hash\":\"0WWlFbZH+gMoP3QGXO7/mf6hIQC/2iN7wd0peEQKFVvnJMp3gTVvi4eXfVd3DSa81MSzAVp7zVxXIiRnJTF0Kw==\",\"predecessors\":{},\"fields\":{\"publicKey\":\"-----BEGIN RSA PUBLIC KEY-----\\nMIGJAoGBAIBsKomutukULWw2zoTW2ECMrM8VmD2xvfpl3R4qh1whzuXV+A4EfRKMb/UAjEfw\\n5nBmWvcObGyYUgygKrlNeOhf3MnDj706rej6ln9cKGL++ZNsJgJsogaAtmkPihWVGi908fdP\\nLQrWTF5be0b/ZP258Zs3CTpcRTpTvhzS5TC1AgMBAAE=\\n-----END RSA PUBLIC KEY-----\\n\"}},\"profile\":{\"displayName\":\"Michael Perry\"}}";

        [Fact]
        public void DeserializeLoginResponse()
        {
            var response = MessageSerializer.Deserialize<LoginResponse>(ExampleLoginResponse);

            response.UserFact.Type.Should().Be("Jinaga.User");
            response.UserFact.Hash.Should().Be("0WWlFbZH+gMoP3QGXO7/mf6hIQC/2iN7wd0peEQKFVvnJMp3gTVvi4eXfVd3DSa81MSzAVp7zVxXIiRnJTF0Kw==");
            response.UserFact.Predecessors.Should().BeEmpty();
            response.UserFact.Fields["publicKey"].Should().BeOfType<FieldValueString>().Which
                .Value.Should().Be("-----BEGIN RSA PUBLIC KEY-----\nMIGJAoGBAIBsKomutukULWw2zoTW2ECMrM8VmD2xvfpl3R4qh1whzuXV+A4EfRKMb/UAjEfw\n5nBmWvcObGyYUgygKrlNeOhf3MnDj706rej6ln9cKGL++ZNsJgJsogaAtmkPihWVGi908fdP\nLQrWTF5be0b/ZP258Zs3CTpcRTpTvhzS5TC1AgMBAAE=\n-----END RSA PUBLIC KEY-----\n");
            response.Profile.DisplayName.Should().Be("Michael Perry");
        }

        private const string ExampleQueryRequest = "{\"start\":{\"type\":\"ImprovingU.Catalog\",\"hash\":\"hRMOCiRybC5CW1hpu9HOkctvK3iyEEZ3ohbjqGHpLAa7WL7lJ+wOlbUvmEkdJN3Q+XCtQTYEzflnWl3R0VgR+Q==\"},\"query\":\"S.catalog F.type=\\\"ImprovingU.Idea\\\" N(S.idea F.type=\\\"ImprovingU.Idea.Deletion\\\" N(S.ideaDeletion F.type=\\\"ImprovingU.Idea.Restore\\\"))\"}";

        [Fact]
        public void SerializeQueryRequest()
        {
            var request = new QueryRequest
            {
                Start = new FactReference
                {
                    Type = "ImprovingU.Catalog",
                    Hash = "hRMOCiRybC5CW1hpu9HOkctvK3iyEEZ3ohbjqGHpLAa7WL7lJ+wOlbUvmEkdJN3Q+XCtQTYEzflnWl3R0VgR+Q=="
                },
                Query = "S.catalog F.type=\"ImprovingU.Idea\" N(S.idea F.type=\"ImprovingU.Idea.Deletion\" N(S.ideaDeletion F.type=\"ImprovingU.Idea.Restore\"))"
            };
            var json = MessageSerializer.Serialize(request);

            json.Should().Be(ExampleQueryRequest);
        }

        private const string ExampleQueryResponse = "{\"results\":[[{\"type\":\"ImprovingU.Idea\",\"hash\":\"UcZ+HbQDBtoIri6EkBNwff5ZQKL9fJJku47aKA1VOOhq2CHuMz1K/axmgSaeJ2V1fsYs7OzPaOUA54BQbUmQtg==\"}],[{\"type\":\"ImprovingU.Idea\",\"hash\":\"0xFdYdxXD8EQy8JRkVOS1EPDtbcYmO84EWAIQdI+Ta1ivmmNLwSCHMZP0t/t6gQgyB1n/XHE8KakKizqu2wPtg==\"},{\"type\":\"ImprovingU.Idea.Name\",\"hash\":\"3+yRVVn54nyUePXtVWMwmBQ/rHxPktEPEGDLHz+0Gh0X0DlOuz6XS5/0itWistrSsUtcNFm/rxIgT+fcs5KAgw==\"}]]}";

        [Fact]
        public void DeserializeQueryResponse()
        {
            var response = MessageSerializer.Deserialize<QueryResponse>(ExampleQueryResponse);

            response.Results.Count.Should().Be(2);
            response.Results[0].SuchThat(first =>
            {
                first.Count.Should().Be(1);
                first[0].Type.Should().Be("ImprovingU.Idea");
                first[0].Hash.Should().Be("UcZ+HbQDBtoIri6EkBNwff5ZQKL9fJJku47aKA1VOOhq2CHuMz1K/axmgSaeJ2V1fsYs7OzPaOUA54BQbUmQtg==");
            });
            response.Results[1].SuchThat(second =>
            {
                second.Count.Should().Be(2);
                second[0].Type.Should().Be("ImprovingU.Idea");
                second[0].Hash.Should().Be("0xFdYdxXD8EQy8JRkVOS1EPDtbcYmO84EWAIQdI+Ta1ivmmNLwSCHMZP0t/t6gQgyB1n/XHE8KakKizqu2wPtg==");
                second[1].Type.Should().Be("ImprovingU.Idea.Name");
                second[1].Hash.Should().Be("3+yRVVn54nyUePXtVWMwmBQ/rHxPktEPEGDLHz+0Gh0X0DlOuz6XS5/0itWistrSsUtcNFm/rxIgT+fcs5KAgw==");
            });
        }

        private const string ExampleLoadRequest = "{\"references\":[{\"type\":\"ImprovingU.Idea\",\"hash\":\"0xFdYdxXD8EQy8JRkVOS1EPDtbcYmO84EWAIQdI+Ta1ivmmNLwSCHMZP0t/t6gQgyB1n/XHE8KakKizqu2wPtg==\"},{\"type\":\"ImprovingU.Idea\",\"hash\":\"1FsyDXgCOBT3MMjQTXxXePldrnWz7iiSBjCCRoDy83A/IoxK0mqqRxi3LK+/3M8ADvyLepMAqWXXWm6LLC/mtA==\"}]}";

        [Fact]
        public void SerializeLoadRequest()
        {
            var request = new LoadRequest
            {
                References = new List<FactReference>()
                {
                    new FactReference
                    {
                        Type = "ImprovingU.Idea",
                        Hash = "0xFdYdxXD8EQy8JRkVOS1EPDtbcYmO84EWAIQdI+Ta1ivmmNLwSCHMZP0t/t6gQgyB1n/XHE8KakKizqu2wPtg=="   
                    },
                    new FactReference
                    {
                        Type = "ImprovingU.Idea",
                        Hash = "1FsyDXgCOBT3MMjQTXxXePldrnWz7iiSBjCCRoDy83A/IoxK0mqqRxi3LK+/3M8ADvyLepMAqWXXWm6LLC/mtA=="
                    }
                }
            };
            var json = MessageSerializer.Serialize(request);

            json.Should().Be(ExampleLoadRequest);
        }

        private const string ExampleLoadResponse = "{\"facts\":[{\"type\":\"ImprovingU.Idea\",\"hash\":\"UcZ+HbQDBtoIri6EkBNwff5ZQKL9fJJku47aKA1VOOhq2CHuMz1K/axmgSaeJ2V1fsYs7OzPaOUA54BQbUmQtg==\",\"predecessors\":{\"catalog\":{\"type\":\"ImprovingU.Catalog\",\"hash\":\"hRMOCiRybC5CW1hpu9HOkctvK3iyEEZ3ohbjqGHpLAa7WL7lJ+wOlbUvmEkdJN3Q+XCtQTYEzflnWl3R0VgR+Q==\"},\"from\":{\"type\":\"Jinaga.User\",\"hash\":\"KhiICU2Ce40p5b/28wNtmPkMm8Cr+nlsva+VwKlofhvmruf/HDGZ7d7Vx/9SnrUnVboPmMvWkm6jYzdQIt8umQ==\"}},\"fields\":{\"title\":\"Improv(ing): It’s FUN and Improving!\",\"createdAt\":\"2020-01-21T14:54:25.517Z\"}},{\"type\":\"ImprovingU.UserName\",\"hash\":\"4kRE4+0eUF9trNgj6J2JXX4JG6M465jzna5sqgXzP95+9dRToYN4WEMpkISnbimi+zxhfacPlurnK6VVh6acdQ==\",\"predecessors\":{\"prior\":[],\"from\":{\"type\":\"Jinaga.User\",\"hash\":\"Vzel/71ZDoX3P5yULRnjYgfgIvimMzDnAYpkfTlwc6qzTItshGfbmeJcWRDjEQnp1vjFL9YRjtgiPFjsqbM6Dw==\"}},\"fields\":{\"value\":\"Topo Gigio\"}}]}";

        [Fact]
        public void DeserializeLoadResponse()
        {
            var response = MessageSerializer.Deserialize<LoadResponse>(ExampleLoadResponse);

            response.Facts.Count.Should().Be(2);

            response.Facts[0].Type.Should().Be("ImprovingU.Idea");
            response.Facts[0].Hash.Should().Be("UcZ+HbQDBtoIri6EkBNwff5ZQKL9fJJku47aKA1VOOhq2CHuMz1K/axmgSaeJ2V1fsYs7OzPaOUA54BQbUmQtg==");
            response.Facts[0].Predecessors["catalog"].Should().BeOfType<PredecessorSetSingle>().Which
                .Reference.SuchThat(catalog => {
                    catalog.Type.Should().Be("ImprovingU.Catalog");
                    catalog.Hash.Should().Be("hRMOCiRybC5CW1hpu9HOkctvK3iyEEZ3ohbjqGHpLAa7WL7lJ+wOlbUvmEkdJN3Q+XCtQTYEzflnWl3R0VgR+Q==");
                });

            response.Facts[1].Type.Should().Be("ImprovingU.UserName");
            response.Facts[1].Hash.Should().Be("4kRE4+0eUF9trNgj6J2JXX4JG6M465jzna5sqgXzP95+9dRToYN4WEMpkISnbimi+zxhfacPlurnK6VVh6acdQ==");
            response.Facts[1].Predecessors["prior"].Should().BeOfType<PredecessorSetMultiple>().Which
                .References.Should().BeEmpty();
            response.Facts[1].Predecessors["from"].Should().BeOfType<PredecessorSetSingle>().Which
                .Reference.SuchThat(from =>
                {
                    from.Type.Should().Be("Jinaga.User");
                    from.Hash.Should().Be("Vzel/71ZDoX3P5yULRnjYgfgIvimMzDnAYpkfTlwc6qzTItshGfbmeJcWRDjEQnp1vjFL9YRjtgiPFjsqbM6Dw==");
                });
            response.Facts[1].Fields["value"].Should().BeOfType<FieldValueString>().Which
                .Value.Should().Be("Topo Gigio");
        }

        private const string ExampleSaveRequest = "{\"facts\":[{\"type\":\"Jinaga.User\",\"hash\":\"0WWlFbZH+gMoP3QGXO7/mf6hIQC/2iN7wd0peEQKFVvnJMp3gTVvi4eXfVd3DSa81MSzAVp7zVxXIiRnJTF0Kw==\",\"predecessors\":{},\"fields\":{\"publicKey\":\"-----BEGIN RSA PUBLIC KEY-----\\nMIGJAoGBAIBsKomutukULWw2zoTW2ECMrM8VmD2xvfpl3R4qh1whzuXV+A4EfRKMb/UAjEfw\\n5nBmWvcObGyYUgygKrlNeOhf3MnDj706rej6ln9cKGL++ZNsJgJsogaAtmkPihWVGi908fdP\\nLQrWTF5be0b/ZP258Zs3CTpcRTpTvhzS5TC1AgMBAAE=\\n-----END RSA PUBLIC KEY-----\\n\"}},{\"type\":\"ImprovingU.Company\",\"hash\":\"J5qqJ3eZwAqLQaAYuW0vx1TuEjdQziimdTpInAoWmMYZfSP9GzkfmousG0+x5fGQelJOsOgSCdyQpAHrqhfZsg==\",\"predecessors\":{\"from\":{\"type\":\"Jinaga.User\",\"hash\":\"0WWlFbZH+gMoP3QGXO7/mf6hIQC/2iN7wd0peEQKFVvnJMp3gTVvi4eXfVd3DSa81MSzAVp7zVxXIiRnJTF0Kw==\"}},\"fields\":{\"name\":\"Improving\"}},{\"type\":\"ImprovingU.Abstract\",\"hash\":\"r+OSVLsMALCUCD0O/oD+0pqJZtKcybtYyfedW1UxJWwe+X+EzeqCFmA05yP4YZuYwu+Dhr3I8AyiS2kdPuGung==\",\"predecessors\":{\"idea\":{\"type\":\"ImprovingU.Idea\",\"hash\":\"mf4FG/R8fxaH2LTDKbLROQaHmDigDc1p/lr7dg5l+7+1wlWQuGChmEXVoQ7xQ8ckeLPvms0SS7wIVsy7/kEKlg==\"},\"prior\":[]},\"fields\":{\"value\":\"How Improving does sales.\"}}]}";

        [Fact]
        public void SerializeSaveRequest()
        {
            var request = MessageSerializer.Serialize(new SaveRequest
            {
                Facts = new List<FactRecord>
                {
                    new FactRecord
                    {
                        Type = "Jinaga.User",
                        Hash = "0WWlFbZH+gMoP3QGXO7/mf6hIQC/2iN7wd0peEQKFVvnJMp3gTVvi4eXfVd3DSa81MSzAVp7zVxXIiRnJTF0Kw==",
                        Predecessors = new Dictionary<string, PredecessorSet>(),
                        Fields = new Dictionary<string, FieldValue>
                        {
                            { "publicKey", FieldValue.From("-----BEGIN RSA PUBLIC KEY-----\nMIGJAoGBAIBsKomutukULWw2zoTW2ECMrM8VmD2xvfpl3R4qh1whzuXV+A4EfRKMb/UAjEfw\n5nBmWvcObGyYUgygKrlNeOhf3MnDj706rej6ln9cKGL++ZNsJgJsogaAtmkPihWVGi908fdP\nLQrWTF5be0b/ZP258Zs3CTpcRTpTvhzS5TC1AgMBAAE=\n-----END RSA PUBLIC KEY-----\n") }
                        }
                    },
                    new FactRecord
                    {
                        Type = "ImprovingU.Company",
                        Hash = "J5qqJ3eZwAqLQaAYuW0vx1TuEjdQziimdTpInAoWmMYZfSP9GzkfmousG0+x5fGQelJOsOgSCdyQpAHrqhfZsg==",
                        Predecessors = new Dictionary<string, PredecessorSet>
                        {
                            {
                                "from",
                                new PredecessorSetSingle
                                {
                                    Reference = new FactReference
                                    {
                                        Type = "Jinaga.User",
                                        Hash = "0WWlFbZH+gMoP3QGXO7/mf6hIQC/2iN7wd0peEQKFVvnJMp3gTVvi4eXfVd3DSa81MSzAVp7zVxXIiRnJTF0Kw=="
                                    }
                                }
                            }
                        },
                        Fields = new Dictionary<string, FieldValue>
                        {
                            { "name", FieldValue.From("Improving") }
                        }
                    },
                    new FactRecord
                    {
                        Type = "ImprovingU.Abstract",
                        Hash = "r+OSVLsMALCUCD0O/oD+0pqJZtKcybtYyfedW1UxJWwe+X+EzeqCFmA05yP4YZuYwu+Dhr3I8AyiS2kdPuGung==",
                        Fields = new Dictionary<string, FieldValue>
                        {
                            { "value", FieldValue.From("How Improving does sales.") }
                        },
                        Predecessors = new Dictionary<string, PredecessorSet>
                        {
                            {
                                "idea",
                                new PredecessorSetSingle
                                {
                                    Reference = new FactReference
                                    {
                                        Type = "ImprovingU.Idea",
                                        Hash = "mf4FG/R8fxaH2LTDKbLROQaHmDigDc1p/lr7dg5l+7+1wlWQuGChmEXVoQ7xQ8ckeLPvms0SS7wIVsy7/kEKlg=="
                                    }
                                }
                            },
                            {
                                "prior",
                                new PredecessorSetMultiple
                                {
                                    References = new List<FactReference>()
                                }
                            }
                        }
                    }
                }
            });

            request.Should().Be(ExampleSaveRequest);
        }

        private const string ExampleFactWithAllFieldTypes = "{\"type\":\"Made.Up.Fact\",\"hash\":\"7Mq7tcuv9UZeBrFV/n+mm9UdiO9nniB0chsiTyxNlusBqQhg67Sdu4e7ORNeqrl5JdbV78vKw4lVDABTpa0Ueg==\",\"predecessors\":{},\"fields\":{\"poem\":\"Twas brillig and the slithy toves did gyre and gimble in the wabe\",\"classic\":true,\"stars\":11}}";

        [Fact]
        public void SerializeAFactWithAllFieldTypes()
        {
            var fact = MessageSerializer.Serialize(new FactRecord
            {
                Type = "Made.Up.Fact",
                Hash = "7Mq7tcuv9UZeBrFV/n+mm9UdiO9nniB0chsiTyxNlusBqQhg67Sdu4e7ORNeqrl5JdbV78vKw4lVDABTpa0Ueg==",
                Fields = new Dictionary<string, FieldValue>
                {
                    {
                        "poem",
                        FieldValue.From("Twas brillig and the slithy toves did gyre and gimble in the wabe")
                    },
                    {
                        "classic",
                        FieldValue.From(true)
                    },
                    {
                        "stars",
                        FieldValue.From(11.0)
                    }
                },
                Predecessors = new Dictionary<string, PredecessorSet>()
            });

            fact.Should().Be(ExampleFactWithAllFieldTypes);
        }

        [Fact]
        public void DeserializeAFactWithAllFieldTypes()
        {
            var fact = MessageSerializer.Deserialize<FactRecord>(ExampleFactWithAllFieldTypes);

            fact.Type.Should().Be("Made.Up.Fact");
            fact.Hash.Should().Be("7Mq7tcuv9UZeBrFV/n+mm9UdiO9nniB0chsiTyxNlusBqQhg67Sdu4e7ORNeqrl5JdbV78vKw4lVDABTpa0Ueg==");
            fact.Fields.Count.Should().Be(3);

            fact.Fields["poem"].Should().BeOfType<FieldValueString>().Which
                .Value.Should().Be("Twas brillig and the slithy toves did gyre and gimble in the wabe");
            fact.Fields["classic"].Should().BeOfType<FieldValueBoolean>().Which
                .Value.Should().BeTrue();
            fact.Fields["stars"].Should().BeOfType<FieldValueNumber>().Which
                .Value.Should().Be(11.0);

            fact.Predecessors.Should().BeEmpty();
        }
    }
}
