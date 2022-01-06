using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Jinaga.Store.SQLite;
using System.IO;
using System.Collections.Generic;
using Xunit.Abstractions;
using System.Diagnostics;
using System.Linq;

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
        public async Task WriteAndReadBack()
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


        [Fact]
        public async Task CanEnableWalMode()
        {
            string dbFolderName = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dbFullName = Path.Combine(dbFolderName, "jinaga.db");
            File.Delete(dbFullName);
            ConnectionFactory connectionFactory = new(dbFullName);
            var mode = await connectionFactory.WithConnection(async (asyncConn) =>
            {
                string sql;
                sql = "PRAGMA journal_mode=WAL";
                return await asyncConn.ExecuteScalarAsync<string>(sql);
            },
                false
            );
            output.WriteLine("Mode: {0}", mode);
            mode.Should().Be("wal");
        }


        [Fact]
        public async Task WriteWhileReading()
        {
            output.WriteLine("{0}: Starting", DateTime.Now);

            string dbFolderName = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dbFullName = Path.Combine(dbFolderName, "jinaga.db");
            File.Delete(dbFullName);
            ConnectionFactory connectionFactory = new(dbFullName);

            output.WriteLine("{0}: createDb starting", DateTime.Now);
            //This 'random' db-access creates the db
            var createDb = connectionFactory.WithConnection(async (asyncConn) =>
                {
                    string sql;
                    sql = "SELECT * FROM fact_type";
                    return await asyncConn.QueryAsync<FactType>(sql);
                },
                 false
            );
            output.WriteLine("{0}: createDb started", DateTime.Now);
            await createDb;
            output.WriteLine("{0}: createDb ended", DateTime.Now);
            
            output.WriteLine("{0}: longRunningReadTxnStartedBeforeWriteTxn starting", DateTime.Now);
            var longRunningReadTxnStartedBeforeWriteTxn =  connectionFactory.WithTransaction(async (asyncConn) =>
                {                   
                    string sql;
                    sql = "SELECT * FROM fact_type";
                    var readResult1Before = await asyncConn.QueryAsync<FactType>(sql);
                    output.WriteLine("{0}: readResult1Before ended: {1}", DateTime.Now, readResult1Before.Aggregate("", (acc, next) => acc + "\r\n\t" + next.ToString()));
                    readResult1Before.Count.Should().Be(0);
                    await Task.Delay(5000);
                    var readResult2Before = await asyncConn.QueryAsync<FactType>(sql);
                    output.WriteLine("{0}: readResult2Before ended: {1}", DateTime.Now, readResult2Before.Aggregate("", (acc, next) => acc + "\r\n\t" + next.ToString()));
                    readResult2Before.Count.Should().Be(0);
                    return readResult2Before;
                },
                false
            );
            output.WriteLine("{0}: longRunningReadTxnStartedBeforeWriteTxn started", DateTime.Now);

            output.WriteLine("{0}: writeTxn starting", DateTime.Now);
            var writeTxn = connectionFactory.WithTransaction(async (asyncConn) =>
                {
                    string sql;
                    sql = @"INSERT INTO fact_type(name)
                                    VALUES
                                        ('row01'),
                                        ('row02'),
                                        ('row03')";
                    await asyncConn.ExecuteAsync(sql);
                    await Task.Delay(1000);
                    sql = @"INSERT INTO fact_type(name)
                                    VALUES
                                    ('row04'),
                                    ('row05'),
                                    ('row06')";
                    return await asyncConn.ExecuteAsync(sql);
                },
                false
            );
            output.WriteLine("{0}: writeTxn started", DateTime.Now);

            output.WriteLine("{0}: longRunningReadTxnStartedDuringWriteTxn starting", DateTime.Now);
            var longRunningReadTxnStartedDuringWriteTxn = connectionFactory.WithTransaction(async (asyncConn) =>
            {
                string sql;
                sql = "SELECT * FROM fact_type";
                var readResult1During = await asyncConn.QueryAsync<FactType>(sql);
                output.WriteLine("{0}: readResult1During ended: {1}", DateTime.Now, readResult1During.Aggregate("", (acc, next) => acc + "\r\n\t" + next.ToString()));
                readResult1During.Count.Should().Be(0);
                await Task.Delay(5000);
                var readResult2During = await asyncConn.QueryAsync<FactType>(sql);
                output.WriteLine("{0}: readResult2During ended: {1}", DateTime.Now, readResult2During.Aggregate("", (acc, next) => acc + "\r\n\t" + next.ToString()));
                readResult2During.Count.Should().Be(0);
                return readResult2During;
            },
                false
            );
            output.WriteLine("{0}: longRunningReadTxnStartedDuringWriteTxn started", DateTime.Now);

            await writeTxn;
            output.WriteLine("{0}: writeTxn ended", DateTime.Now);

            output.WriteLine("{0}: longRunningReadTxnStartedAfterWriteTxn starting", DateTime.Now);
            var longRunningReadTxnStartedAfterWriteTxn = connectionFactory.WithTransaction(async (asyncConn) =>
            {
                string sql;
                sql = "SELECT * FROM fact_type";
                var readResult1After = await asyncConn.QueryAsync<FactType>(sql);
                output.WriteLine("{0}: readResult1After ended: {1}", DateTime.Now, readResult1After.Aggregate("", (acc, next) => acc + "\r\n\t" + next.ToString()));
                readResult1After.Count.Should().Be(6);
                await Task.Delay(5000);
                var readResult2After = await asyncConn.QueryAsync<FactType>(sql);
                output.WriteLine("{0}: readResult2After ended: {1}", DateTime.Now, readResult2After.Aggregate("", (acc, next) => acc + "\r\n\t" + next.ToString()));
                readResult2After.Count.Should().Be(6);
                return readResult2After;
            },
                false
            );
            output.WriteLine("{0}: longRunningReadTxnStartedAfterWriteTxn started", DateTime.Now);

            await longRunningReadTxnStartedBeforeWriteTxn;
            await longRunningReadTxnStartedDuringWriteTxn;
            await longRunningReadTxnStartedAfterWriteTxn;        
        }

    }
}