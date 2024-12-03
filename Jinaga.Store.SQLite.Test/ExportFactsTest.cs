using Jinaga.DefaultImplementations;
using Jinaga.Facts;
using Jinaga.Store.SQLite.Test.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using System.Text.Json;

namespace Jinaga.Store.SQLite.Test
{
    public class ExportFactsTest
    {
        [Fact]
        public async Task ExportFactsToJson_ShouldReturnJsonString()
        {
            var jinagaClient = GivenJinagaClient();

            var site = await jinagaClient.Fact(new Site("qedcode.com"));
            var post = await jinagaClient.Fact(new Post(site, "2022-08-16T15:23:13.231Z"));
            var title1 = await jinagaClient.Fact(new Title(post, "Introducing Jinaga Replicator", new Title[0]));
            var title2 = await jinagaClient.Fact(new Title(post, "Introduction to the Jinaga Replicator", new[] { title1 }));

            using var memoryStream = new MemoryStream();
            await jinagaClient.Internal.ExportFactsToJson(memoryStream);
            memoryStream.Position = 0;
            var json = await new StreamReader(memoryStream).ReadToEndAsync();

            json.Should().Be(
                """
                [
                    {
                        "hash": "wJ9ShDT5oSZPRZt/q4kTArPD3ToIBjGdv1hTVFnJqcrdFWaxZ+SkHQU/C+xOOzGCWBdMhxIlXT0ielCknAxS7w==",
                        "type": "Blog.Site",
                        "predecessors": {},
                        "fields": {
                            "domain": "qedcode.com"
                        }
                    },
                    {
                        "hash": "1u8l+re6edyddf18SzzFahsjhYB8naAoRO1bPUf2+wpF2SN1yAHCHIXwodi21StOL8jZWJJjBYijhmhN1ijKrQ==",
                        "type": "Blog.Post",
                        "predecessors": {
                            "site": {
                                "hash": "wJ9ShDT5oSZPRZt/q4kTArPD3ToIBjGdv1hTVFnJqcrdFWaxZ+SkHQU/C+xOOzGCWBdMhxIlXT0ielCknAxS7w==",
                                "type": "Blog.Site"
                            }
                        },
                        "fields": {
                            "createdAt": "2022-08-16T15:23:13.231Z"
                        }
                    },
                    {
                        "hash": "CgkszleH2PlA2ck14bVIlMhXy7so8ML5wANqFCWTXxKLDShKGZEdYR8syeTxSfJchBAUexN6dddfOb4eAG8B0g==",
                        "type": "Blog.Post.Title",
                        "predecessors": {
                            "post": {
                                "hash": "1u8l+re6edyddf18SzzFahsjhYB8naAoRO1bPUf2+wpF2SN1yAHCHIXwodi21StOL8jZWJJjBYijhmhN1ijKrQ==",
                                "type": "Blog.Post"
                            },
                            "prior": []
                        },
                        "fields": {
                            "value": "Introducing Jinaga Replicator"
                        }
                    },
                    {
                        "hash": "8FTdXjzf37e5lL6JCoS2Ibkwd58wr0uOKbo6lJc1R4Ai9E6+k3ZFHtX/8fmC6s115WloU2fNLptJlqrC2pvAJQ==",
                        "type": "Blog.Post.Title",
                        "predecessors": {
                            "post": {
                                "hash": "1u8l+re6edyddf18SzzFahsjhYB8naAoRO1bPUf2+wpF2SN1yAHCHIXwodi21StOL8jZWJJjBYijhmhN1ijKrQ==",
                                "type": "Blog.Post"
                            },
                            "prior": [
                                {
                                    "hash": "CgkszleH2PlA2ck14bVIlMhXy7so8ML5wANqFCWTXxKLDShKGZEdYR8syeTxSfJchBAUexN6dddfOb4eAG8B0g==",
                                    "type": "Blog.Post.Title"
                                }
                            ]
                        },
                        "fields": {
                            "value": "Introduction to the Jinaga Replicator"
                        }
                    }
                ]

                """
            );

            var facts = JsonSerializer.Deserialize<List<object>>(json);
            facts.Should().NotBeNull();
            facts.Should().HaveCount(4);
        }

        [Fact]
        public async Task ExportFactsToFactual_ShouldReturnFactualString()
        {
            var jinagaClient = GivenJinagaClient();

            var site = await jinagaClient.Fact(new Site("qedcode.com"));
            var post = await jinagaClient.Fact(new Post(site, "2022-08-16T15:23:13.231Z"));
            var title1 = await jinagaClient.Fact(new Title(post, "Introducing Jinaga Replicator", new Title[0]));
            var title2 = await jinagaClient.Fact(new Title(post, "Introduction to the Jinaga Replicator", new[] { title1 }));

            using var memoryStream = new MemoryStream();
            await jinagaClient.Internal.ExportFactsToFactual(memoryStream);
            memoryStream.Position = 0;
            var factual = await new StreamReader(memoryStream).ReadToEndAsync();

            factual.Should().Be(
                """
                let f1: Blog.Site = {
                    domain: "qedcode.com"
                }

                let f2: Blog.Post = {
                    createdAt: "2022-08-16T15:23:13.231Z",
                    site: f1
                }

                let f3: Blog.Post.Title = {
                    value: "Introducing Jinaga Replicator",
                    post: f2,
                    prior: []
                }

                let f4: Blog.Post.Title = {
                    value: "Introduction to the Jinaga Replicator",
                    post: f2,
                    prior: [f3]
                }


                """
            );
        }

        private static async Task<string> ConcatAsync(IAsyncEnumerable<string> chunks)
        {
            var result = new StringBuilder();
            await foreach (var chunk in chunks)
            {
                result.Append(chunk);
            }
            return result.ToString();
        }

        private static JinagaClient GivenJinagaClient()
        {
            var store = new SQLiteStore(
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                    "ExportFactsTest.db"),
                new NullLoggerFactory());
            var jinagaClient = new JinagaClient(
                store,
                new LocalNetwork(),
                PurgeConditions.Empty,
                new NullLoggerFactory());
            return jinagaClient;
        }
    }
}
