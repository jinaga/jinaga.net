using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Jinaga.Store.SQLite;
using System.IO;
using System.Collections.Generic;
using Xunit.Abstractions;
using System.Diagnostics;

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
                return $"{fact_type_id}-{name}";
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


        [Fact]
        public async Task ExponentialBackoffOk()
        {
            output.WriteLine("Started: {0}", DateTime.Now);
            string dbFolderName = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dbFullName = Path.Combine(dbFolderName, "jinaga.db");
            File.Delete(dbFullName);
            ConnectionFactory connectionFactory = new(dbFullName);

            try
            {
                var nbOfRecordsInserted = await connectionFactory.WithConnection(async (asyncConn) =>
                    {
                        string sql;
                        sql = $"INSERT INTO fact_type(name) VALUES ('{DateTime.Now} - {Stopwatch.GetTimestamp(),15:N0}')";
                        //This statement will save a record
                        await asyncConn.ExecuteAsync(sql);                         
                        sql = $"INSERT INTO fact_type(NonExistingColumnName) VALUES ('ERROR @ {DateTime.Now} - {Stopwatch.GetTimestamp(),15:N0}')";
                        //This statement will fail, hence this 'insert' AND the previous 'insert' will be repeated by the backoff algo.
                        //As both 'inserts' are independant (not part of a transaction), on every attempt the first 'insert' will save a record.
                        return await asyncConn.ExecuteAsync(sql);
                    }
                );
            }
            catch (Exception e)
            {
                output.WriteLine("MyError: {0}", e.ToString());
            }

            var recordsReadAfterBackoff = await connectionFactory.WithConnection(async (asyncConn) =>
                {
                    string sql;
                    sql = @"SELECT *
                                FROM fact_type
                                ORDER BY name";
                    return await asyncConn.QueryAsync<FactType>(sql);
                }
            );

            output.WriteLine("recordsReadAfterBackoff: {0}", "\r\n\t" + string.Join("\r\n\t", recordsReadAfterBackoff));
            recordsReadAfterBackoff.Count.Should().Be(6);

            try
            {
                //Same as above, but this time without backoff, so only one more record will be added to the database.
                var nbOfRecordsInserted = await connectionFactory.WithConnection(async (asyncConn) =>
                    {
                        string sql;
                        sql = $"INSERT INTO fact_type(name) VALUES ('{DateTime.Now} - {Stopwatch.GetTimestamp(),15:N0}')";
                        await asyncConn.ExecuteAsync(sql);
                        sql = $"INSERT INTO fact_type(NonExistingColumnName) VALUES ('ERROR @ {DateTime.Now} - {Stopwatch.GetTimestamp(),15:N0}')";
                        return await asyncConn.ExecuteAsync(sql);
                    },
                    false
                );
            }
            catch
            {
            }

            var recordsReadInTotal = await connectionFactory.WithConnection(async (asyncConn) =>
            {
                string sql;
                sql = @"SELECT *
                                FROM fact_type
                                ORDER BY name";
                return await asyncConn.QueryAsync<FactType>(sql);
            }
            );

            output.WriteLine("recordsReadInTotal: {0}", "\r\n\t" + string.Join("\r\n\t", recordsReadInTotal));
            recordsReadInTotal.Count.Should().Be(7);
        }

    }
}
