using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Jinaga.Store.SQLite;
using System.IO;
using System.Collections.Generic;
using Xunit.Abstractions;

namespace Jinaga.Test
{
    public class SQLiteStoreTest
    {

        private readonly ITestOutputHelper output;


        public SQLiteStoreTest(ITestOutputHelper output)
        {
            this.output = output;
        }


        private class FactType
        {
            public int fact_type_id { get; set; }
            public string name { get; set; }

            public override string ToString()
            {
                return $"{fact_type_id}-{name}" ;
            }
        }


        [Fact]
        public async Task CanWriteAndReadBack()
        {
            output.WriteLine("Started: {0}", DateTime.Now);
            string dbFolderName = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dbFullName = Path.Combine(dbFolderName, "jinaga.db");
            File.Delete(dbFullName);
            ConnectionFactory connectionFactory = new(dbFullName);

            var nbOfRecordsInserted = await connectionFactory.WithConnection(async (asyncConn) =>
                {
                    string sql;
                    sql = @"INSERT INTO fact_type(name)
                            VALUES
                                ('DWS.Supplier'),
                                ('DWS.Client'),
                                ('DWS.Client.Name')";
                    return await asyncConn.ExecuteAsync(sql);
                }
            );

            var recordsRead = await connectionFactory.WithConnection(async (asyncConn) =>
                {
                    string sql;
                    sql = @"SELECT *
                            FROM fact_type
                            ORDER BY name";
                    return await asyncConn.QueryAsync<FactType>(sql);
                }
            );

            output.WriteLine("NbOfRecordsInserted: {0}", nbOfRecordsInserted);
            output.WriteLine("RecordsRead: {0}", "\r\n\t" + string.Join("\r\n\t", recordsRead));

            nbOfRecordsInserted.Should().Be(3);
            recordsRead.Should().BeEquivalentTo(
                new List<FactType> {
                    new FactType{fact_type_id = 2, name = "DWS.Client" },
                    new FactType{fact_type_id = 3, name = "DWS.Client.Name" },
                    new FactType{fact_type_id = 1, name = "DWS.Supplier" }
                }
            );
        }

    }
}
