using FluentAssertions;
using Jinaga.Facts;
using Jinaga.Managers;
using Jinaga.Services;
using Jinaga.Store.SQLite;
using Jinaga.Test.Model;
using Jinaga.Test.Model.DWS;
using Jinaga.UnitTest;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Jinaga.Test
{


    public class SQLiteStoreTest
    {

        private readonly ITestOutputHelper output;
        public Stopwatch stopwatch;


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
        public async Task StoreRoundTripToUTC()
        {
            output.WriteLine($"{MyStopWatch.Start()}: BEGIN OF TESTS at {DateTime.Now}");
         
            DateTime now = DateTime.Parse("2021-07-04T01:39:43.241Z");
            var j = new Jinaga(new SQLiteStore());            
            var airlineDay = await j.Fact(new AirlineDay(new Airline("Airline1"), now));
            var flight = await j.Fact(new Flight(airlineDay, 555));
            //airlineDay = await j.Fact(new AirlineDay(new Airline("Airline2"), now));
            //airlineDay = await j.Fact(new AirlineDay(new Airline("Airline3"), DateTime.Today.AddDays(-1)));
            airlineDay.date.Kind.Should().Be(DateTimeKind.Utc);
            airlineDay.date.Hour.Should().Be(1);

            output.WriteLine($"{MyStopWatch.Elapsed()}: END OF TESTS at {DateTime.Now}\n\r");
        }


        [Fact]
        public async Task SavePredecessorMultiple()
        {
            output.WriteLine($"{MyStopWatch.Start()}: BEGIN OF TESTS at {DateTime.Now}");

            DateTime now = DateTime.Parse("2021-07-04T01:39:43.241Z");
            var j = new Jinaga(new SQLiteStore());
            var airline = new Airline("Airline1");            
            var user = await j.Fact(new User("fqjsdfqkfjqlm"));
            var passenger = await j.Fact(new Passenger(airline, user));
            var passengerName1 = await j.Fact(new PassengerName(passenger, "Michael", new PassengerName[0]));
            var passengerName2 = await j.Fact(new PassengerName(passenger, "Caden", new PassengerName[0]));
            var passengerName3 = await j.Fact(new PassengerName(passenger, "Jan", new PassengerName[] { passengerName1, passengerName2 }));
            
            //TODO: add test condition
            output.WriteLine($"{MyStopWatch.Elapsed()}: END OF TESTS at {DateTime.Now}\n\r");

        }


        [Fact]
        public async Task LoadNothingFromStore()
        {
            output.WriteLine($"{MyStopWatch.Start()}: BEGIN OF TESTS at {DateTime.Now}");
            IStore sqliteStore = new SQLiteStore();
            FactGraph factGraph = await sqliteStore.Load(ImmutableList<FactReference>.Empty, default);
            factGraph.FactReferences.Should().BeEmpty();
            output.WriteLine($"{MyStopWatch.Elapsed()}: END OF TESTS at {DateTime.Now}\n\r");
        }


        [Fact]
        public async Task LoadFromStore()
        {
            output.WriteLine($"{MyStopWatch.Start()}: BEGIN OF TESTS at {DateTime.Now}");

            //IStore Store = new MemoryStore();
            IStore Store = new SQLiteStore();

            DateTime now = DateTime.Parse("2021-07-04T01:39:43.241Z");
            var j = new Jinaga(Store);            
            var airline = new Airline("Airline1");           
            var user = await j.Fact(new User("fqjsdfqkfjqlm"));
            var passenger = await j.Fact(new Passenger(airline, user));
            var passengerName1 = await j.Fact(new PassengerName(passenger, "Michael", new PassengerName[0]));
            var passengerName2 = await j.Fact(new PassengerName(passenger, "Caden", new PassengerName[0]));
            var passengerName3 = await j.Fact(new PassengerName(passenger, "Jan", new PassengerName []{passengerName1, passengerName2}));

            var graph = new FactManager(Store).Serialize(passengerName3);            
            var lastRef = graph.Last;

            //var airlineFact = Fact.Create(
            //    "Skylane.Airline",
            //    ImmutableList<Field>.Empty.Add(new Field("identifier", new FieldValueString("Airline1"))),
            //    ImmutableList<Predecessor>.Empty
            //);

            //var userFact = Fact.Create(
            //     "Jinaga.User",
            //     ImmutableList<Field>.Empty.Add(new Field("publicKey", new FieldValueString("fqjsdfqkfjqlm"))),
            //     ImmutableList<Predecessor>.Empty
            // );

            //var passengerFact = Fact.Create(
            //     "Skylane.Passenger",
            //     ImmutableList<Field>.Empty,
            //     ImmutableList<Predecessor>.Empty
            //        .Add(new PredecessorSingle("airline", airlineFact.Reference))
            //        .Add(new PredecessorSingle("user", userFact.Reference))
            // );

            //var passengerName1Fact = Fact.Create(
            //    "Skylane.Passenger.Name",
            //    ImmutableList<Field>.Empty.Add(new Field("value", new FieldValueString("Michael"))),
            //    ImmutableList<Predecessor>.Empty
            //        .Add(new PredecessorSingle("passenger", passengerFact.Reference))
            //        .Add(new PredecessorMultiple("prior", ImmutableList<FactReference>.Empty))
            //);

            //var passengerName2Fact = Fact.Create(
            //    "Skylane.Passenger.Name",
            //    ImmutableList<Field>.Empty.Add(new Field("value", new FieldValueString("Caden"))),
            //    ImmutableList<Predecessor>.Empty
            //        .Add(new PredecessorSingle("passenger", passengerFact.Reference))
            //        .Add(new PredecessorMultiple("prior", ImmutableList<FactReference>.Empty))
            //);

            //var passengerName3Fact = Fact.Create(
            //    "Skylane.Passenger.Name",
            //    ImmutableList<Field>.Empty.Add(new Field("value", new FieldValueString("Jan"))),
            //    ImmutableList<Predecessor>.Empty
            //        .Add(new PredecessorSingle("passenger", passengerFact.Reference))
            //        .Add(new PredecessorMultiple("prior", ImmutableList<FactReference>.Empty
            //            .Add(passengerName1Fact.Reference)
            //            .Add(passengerName2Fact.Reference)
            //        ))
            //);
                 
            var factGraph = await Store.Load(ImmutableList<FactReference>.Empty.Add(lastRef), default);
            factGraph.FactReferences.Count.Should().Be(6);

            output.WriteLine($"{MyStopWatch.Elapsed()}: END OF TESTS at {DateTime.Now}\n\r");
        }







        [Fact]
        public async Task LoadFromStoreDWS()
        {
            output.WriteLine($"{MyStopWatch.Start()}: BEGIN OF TESTS at {DateTime.Now}");

            //IStore Store = new MemoryStore();
            IStore Store = new SQLiteStore();

            DateTime now = DateTime.Parse("2021-07-04T01:39:43.241Z");
            var j = new Jinaga(Store);

            var supplier = await j.Fact(new Supplier("abc-pubkey"));
            var client = await j.Fact(new Client(supplier, now));
            var yard = await j.Fact(new Yard(client, now));
            var yardAddress1 = await j.Fact(new YardAddress(yard, "myYardName1", "myRemark","myStreet","myHousNb","myPostalCode","myCity","myCountry", new YardAddress[0]));
            var yardAddress2 = await j.Fact(new YardAddress(yard, "myYardName2", "myRemark", "myStreet", "myHousNb", "myPostalCode", "myCity", "myCountry", new YardAddress[0]));
            var yardAddress3 = await j.Fact(new YardAddress(yard, "myYardName3", "myRemark", "myStreet", "myHousNb", "myPostalCode", "myCity", "myCountry", new YardAddress[] { yardAddress1, yardAddress2 }));
            var yardAddress4 = await j.Fact(new YardAddress(yard, "myYardName3", "myRemark", "myStreet2", "myHousNb", "myPostalCode", "myCity", "myCountry", new YardAddress[] { yardAddress3}));

            var graph = new FactManager(Store).Serialize(yardAddress4);
            var lastRef = graph.Last;      
            
            var factGraph = await Store.Load(ImmutableList<FactReference>.Empty.Add(lastRef), default);
            factGraph.FactReferences.Count.Should().Be(7);

            output.WriteLine($"{MyStopWatch.Elapsed()}: END OF TESTS at {DateTime.Now}\n\r");
        }







        [Fact]
        public async Task StoreRoundTripFromUTC()
        {
            DateTime now = DateTime.Parse("2021-07-04T01:39:43.241Z").ToUniversalTime();
            var j = new Jinaga(new SQLiteStore());
            var airlineDay = await j.Fact(new AirlineDay(new Airline("value"), now));
            airlineDay.date.Kind.Should().Be(DateTimeKind.Utc);
            airlineDay.date.Hour.Should().Be(1);
        }


        [Fact]
        public async Task WriteAndReadBack() 
        {
            output.WriteLine($"{MyStopWatch.Start()}: BEGIN OF TESTS at {DateTime.Now}");
            string dbFolderName = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dbFullName = Path.Combine(dbFolderName, "jinaga.db");
            File.Delete(dbFullName);
            ConnectionFactory connFactory = new(dbFullName);
            output.WriteLine($"{MyStopWatch.Elapsed()}: New database created");

            var nbOfRecordsInserted = await connFactory.WithTxnAsync((conn, id) =>
               {
                   string sql;
                   sql = @"INSERT INTO fact_type(name)
                                    VALUES
                                        ('DWS.Supplier'),
                                        ('DWS.Client'),
                                        ('DWS.Client.Name')";
                   return conn.ExecuteNonQuery(sql);
               },
               false
            );

            var recordsRead = await connFactory.WithTxnAsync((conn, id) =>
               {
                   string sql;
                   sql = @"SELECT *
                                    FROM fact_type
                                    ORDER BY name";
                   return conn.ExecuteQuery<FactType>(sql);
               },
                false
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
            output.WriteLine($"{MyStopWatch.Elapsed()}: END OF TESTS at {DateTime.Now}\n\r");
        }




        [Fact]
        public void ExponentialBackoffOk()
        {
            output.WriteLine($"{MyStopWatch.Start()}: BEGIN OF TESTS at {DateTime.Now}");
            string dbFolderName = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dbFullName = Path.Combine(dbFolderName, "jinaga.db");
            File.Delete(dbFullName);
            ConnectionFactory connFactory = new(dbFullName);
            output.WriteLine($"{MyStopWatch.Elapsed()}: New database created");

            try
            {
                var nbOfRecordsInserted = connFactory.WithConn((conn, id) =>
                    {
                        string sql;
                        sql = $"INSERT INTO fact_type(name) VALUES ('{DateTime.Now} - {Stopwatch.GetTimestamp(),15:N0}')";
                        //This statement will save a record
                        conn.ExecuteNonQuery(sql);
                        sql = $"INSERT INTO fact_type(NonExistingColumnName) VALUES ('ERROR @ {DateTime.Now} - {Stopwatch.GetTimestamp(),15:N0}')";
                        //This statement will fail, hence this 'insert' AND the previous 'insert' will be repeated by the backoff algo.
                        //As both 'inserts' are independant (not part of a transaction), on every attempt the first 'insert' will save a record.
                        return conn.ExecuteNonQuery(sql);
                    },
                    true
                );
            }
            catch (Exception e)
            {
                output.WriteLine("MyError: {0}", e.ToString());
            }

            var recordsReadAfterBackoff = connFactory.WithConn((conn, id) =>
                {
                    string sql;
                    sql = "SELECT * FROM fact_type ORDER BY name";
                    return conn.ExecuteQuery<FactType>(sql);
                }
            );

            output.WriteLine("recordsReadAfterBackoff: {0}", "\r\n\t" + string.Join("\r\n\t", recordsReadAfterBackoff));
            recordsReadAfterBackoff.Count().Should().Be(8);

            try
            {
                var nbOfRecordsInserted = connFactory.WithConn((conn, id) =>
                    {
                        string sql;
                        sql = $"INSERT INTO fact_type(name) VALUES ('{DateTime.Now} - {Stopwatch.GetTimestamp(),15:N0}')";
                        //This statement will save a record
                        conn.ExecuteNonQuery(sql);
                        sql = $"INSERT INTO fact_type(NonExistingColumnName) VALUES ('ERROR @ {DateTime.Now} - {Stopwatch.GetTimestamp(),15:N0}')";
                        //This statement will fail, hence this 'insert' AND the previous 'insert' will be repeated by the backoff algo.
                        //As both 'inserts' are independant (not part of a transaction), on every attempt the first 'insert' will save a record.
                        return conn.ExecuteNonQuery(sql);
                    },
                    false
                );
            }
            catch (Exception e)
            {
                output.WriteLine("MyError: {0}", e.ToString());
            }

            var recordsReadInTotal = connFactory.WithConn((conn, id) =>
            {
                string sql;
                sql = "SELECT * FROM fact_type ORDER BY name";
                return conn.ExecuteQuery<FactType>(sql);
            }
            );

            output.WriteLine("recordsReadInTotal: {0}", "\r\n\t" + string.Join("\r\n\t", recordsReadInTotal));
            recordsReadInTotal.Count().Should().Be(9);
            output.WriteLine($"{MyStopWatch.Elapsed()}: END OF TESTS at {DateTime.Now}");

        }


        [Fact]
        public void CanEnableWalMode()
        {
            output.WriteLine($"{MyStopWatch.Start()}: BEGIN OF TESTS at {DateTime.Now}");
            string dbFolderName = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dbFullName = Path.Combine(dbFolderName, "jinaga.db");
            File.Delete(dbFullName);
            ConnectionFactory connFactory = new(dbFullName);
            output.WriteLine($"{MyStopWatch.Elapsed()}: New database created");

            string mode;

            mode = connFactory.WithConn((conn, id) =>
                {
                    string sql;
                    sql = "PRAGMA journal_mode=DELETE";
                    return conn.ExecuteScalar<string>(sql);
                },
                 false
            );
            
            output.WriteLine($"{MyStopWatch.Elapsed()}: Mode: {mode}");
            mode.Should().Be("delete");

            mode = connFactory.WithConn((conn, id) =>
               {
                   string sql;
                   sql = "PRAGMA journal_mode=WAL";
                   return conn.ExecuteScalar<string>(sql);
               },
                    false
            );
            output.WriteLine($"{MyStopWatch.Elapsed()}: Mode: {mode}");
            mode.Should().Be("wal");
            output.WriteLine($"{MyStopWatch.Elapsed()}: END OF TESTS at {DateTime.Now}");
        }


        [Fact]
        public async Task WriteWhileReading()
        {
            output.WriteLine($"{MyStopWatch.Start()}: BEGIN OF TESTS at {DateTime.Now}");

            string dbFolderName = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dbFullName = Path.Combine(dbFolderName, "jinaga.db");
            File.Delete(dbFullName);
            ConnectionFactory connFactory = new(dbFullName);
            output.WriteLine($"{MyStopWatch.Elapsed()}: New database created \r\n");

            output.WriteLine($"{MyStopWatch.Elapsed()}: readTxnBefore starting");            
            var readTxnBefore = connFactory.WithTxnAsync((conn, id) =>
               {
                   output.WriteLine("{0}: readTxnBefore started", DateTime.Now);
                   string sql;
                   sql = "SELECT * FROM fact_type";
                   var readResult1Before = conn.ExecuteQuery<FactType>(sql);
                   output.WriteLine("{0}: readResult1Before ended: {1}", DateTime.Now, readResult1Before.Aggregate("", (acc, next) => acc + "\r\n\t" + next.ToString()));
                   readResult1Before.Count().Should().Be(0);
                   Thread.Sleep(5000);
                   var readResult2Before = conn.ExecuteQuery<FactType>(sql);
                   output.WriteLine("{0}: readResult2Before ended: {1}", DateTime.Now, readResult2Before.Aggregate("", (acc, next) => acc + "\r\n\t" + next.ToString()));
                   readResult2Before.Count().Should().Be(0);
                   output.WriteLine("{0}: readTxnBefore ended", DateTime.Now);
                   return readResult2Before;
               },
                false
            );

            await Task.Delay(100);

            output.WriteLine($"{MyStopWatch.Elapsed()}: writeTxn starting");            
            var writeTxn = connFactory.WithTxnAsync((conn, id) =>
                {
                    output.WriteLine("{0}: writeTxn started", DateTime.Now);
                    string sql;
                    sql = @"INSERT INTO fact_type(name)
                                                VALUES
                                                    ('row01'),
                                                    ('row02'),
                                                    ('row03')";
                    var result1 = conn.ExecuteNonQuery(sql);
                    output.WriteLine("{0}: writeTxn result1: {1}", DateTime.Now, result1);
                    Thread.Sleep(2000);
                    sql = @"INSERT INTO fact_type(name)
                                                VALUES
                                                ('row04'),
                                                ('row05'),
                                                ('row06')";
                    var result2 = conn.ExecuteNonQuery(sql);
                    output.WriteLine("{0}: writeTxn result2: {1}", DateTime.Now, result2);
                    output.WriteLine("{0}: writeTxn ended", DateTime.Now);
                    return result1 + result2;
                },
                false
            );

            await Task.Delay(100);
           
            output.WriteLine($"{MyStopWatch.Elapsed()}: writeTxn2 starting");
            var writeTxn2 = connFactory.WithTxnAsync((conn, id) =>
            {
                output.WriteLine("{0}: writeTxn2 started", DateTime.Now);
                string sql;
                sql = @"INSERT INTO fact_type(name)
                                                VALUES
                                                    ('row07'),
                                                    ('row08'),
                                                    ('row09')";
                var result1 = conn.ExecuteNonQuery(sql);
                output.WriteLine("{0}: writeTxn2 result1: {1}", DateTime.Now, result1);
                Thread.Sleep(2000);
                sql = @"INSERT INTO fact_type(name)
                                                VALUES
                                                ('row10'),
                                                ('row11'),
                                                ('row12')";
                var result2 = conn.ExecuteNonQuery(sql);
                output.WriteLine("{0}: writeTxn2 result2: {1}", DateTime.Now, result2);
                output.WriteLine("{0}: writeTxn2 ended", DateTime.Now);
                return result1 + result2;
            },
                true
            );

            await Task.Delay(100);
            
            output.WriteLine($"{MyStopWatch.Elapsed()}: readTxnDuring starting");
            var readTxnDuring = connFactory.WithTxnAsync((conn, id) =>
            {
                output.WriteLine("{0}: readTxnDuring started", DateTime.Now);
                string sql;
                sql = "SELECT * FROM fact_type";
                var readResult1During = conn.ExecuteQuery<FactType>(sql);
                output.WriteLine("{0}: readResult1During ended: {1}", DateTime.Now, readResult1During.Aggregate("", (acc, next) => acc + "\r\n\t" + next.ToString()));
                readResult1During.Count().Should().Be(0);
                Thread.Sleep(5000);
                var readResult2During = conn.ExecuteQuery<FactType>(sql);
                output.WriteLine("{0}: readResult2During ended: {1}", DateTime.Now, readResult2During.Aggregate("", (acc, next) => acc + "\r\n\t" + next.ToString()));
                readResult2During.Count().Should().Be(0);
                output.WriteLine("{0}: readTxnDuring ended", DateTime.Now);
                return readResult2During;
            },
                false
            );

            await writeTxn;
            await writeTxn2;
            
            output.WriteLine($"{MyStopWatch.Elapsed()}: readTxnAfter starting");
            var readTxnAfter = connFactory.WithTxnAsync((conn, id) =>           
            {
                output.WriteLine("{0}: readTxnAfter started", DateTime.Now);
                string sql;
                sql = "SELECT * FROM fact_type";
                var readResult1After = conn.ExecuteQuery<FactType>(sql);
                output.WriteLine("{0}: readResult1After ended: {1}", DateTime.Now, readResult1After.Aggregate("", (acc, next) => acc + "\r\n\t" + next.ToString()));
                readResult1After.Count().Should().Be(12);
                Thread.Sleep(5000);
                var readResult2After = conn.ExecuteQuery<FactType>(sql);
                output.WriteLine("{0}: readResult2After ended: {1}", DateTime.Now, readResult2After.Aggregate("", (acc, next) => acc + "\r\n\t" + next.ToString()));
                readResult2After.Count().Should().Be(12);
                output.WriteLine("{0}: readTxnAfter ended", DateTime.Now);
                return readResult2After;
            },
                false
            );


            await readTxnBefore;
            await readTxnDuring;
            await readTxnAfter;
            output.WriteLine($"{MyStopWatch.Elapsed()}: END OF TESTS at {DateTime.Now}");
        }


        //[Fact]
        ////public async Task MassiveConcurrentReads()
        //public void MassiveConcurrentReads()
        //{
        //    //const int MAX = 1;
        //    output.WriteLine($"{MyStopWatch.Start()}: BEGIN OF TESTS at {DateTime.Now}");

        //    string dbFolderName = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        //    string dbFullName = Path.Combine(dbFolderName, "jinaga.db");
        //    //File.Delete(dbFullName);
        //    ConnectionFactory connFactory = new(dbFullName);
        //    //output.WriteLine("{0}: createDb ended", DateTime.Now);


        //    //an initial synchronous read to make sure there is no recovery waiting to run at the next read
        //    //output.WriteLine("{0}: readTxnBefore starting", DateTime.Now);
        //    var readTxnBefore = connFactory.WithTxn((conn, id) =>
        //    {
        //        //  output.WriteLine("{0}: readTxnBefore started", DateTime.Now);
        //        string sql;
        //        sql = "SELECT count(*) FROM fact_type";
        //        var readResult1Before = conn.ExecuteScalar<FactType>(sql);
        //        //output.WriteLine("{0}: readTxnBefore ended", DateTime.Now);
        //        return readResult1Before;
        //    },
        //        false
        //    );
        //    readTxnBefore.Should().Be("0");

        //    //a small pause after the initial synchronous read to make sure there is no recovery thread anymore at the next read
        //    //await Task.Delay(5000);



        //    //for (int i = 1; i < MAX; i++)
        //    //{
        //    //    output.WriteLine("{0}: writeTxn starting", DateTime.Now);
        //    //    var writeTxn = connFactory.WithTxn((conn) =>
        //    //    {
        //    //        output.WriteLine("{0}: writeTxn started", DateTime.Now);
        //    //        string sql;
        //    //        sql = $"INSERT INTO fact_type(name) VALUES ('row{i,3:D3}');";
        //    //        var result = conn.ExecuteNonQuery(sql);
        //    //        output.WriteLine("{0}: writeTxn result: {1}", DateTime.Now, result);
        //    //        return result;
        //    //    },
        //    //        false
        //    //    );                
        //    //}          

        //    //const int MAX2 = 50;

        //    //Task<string>[] tasks2 = new Task<string>[MAX2];
        //    //for (int i = 0; i < MAX2; i++)
        //    //{
        //    //    output.WriteLine("{0}: {1} starting", DateTime.Now, i);
        //    //    tasks2[i] = connFactory.WithTxnAsync((conn, id) =>
        //    //        {
        //    //            output.WriteLine("{0}: {1} STARTED", DateTime.Now, id);                        
        //    //            string sql;
        //    //            sql = "SELECT count(*) FROM fact_type";

        //    //            var readResult1 = conn.ExecuteScalar<FactType>(sql);
        //    //            ////output.WriteLine("{0}: {1} readResult1 ended: {2}", DateTime.Now, i, readResult1.Aggregate("", (acc, next) => acc + "\r\n\t" + next.ToString()));
        //    //            ////readResult1.Count().Should().Be(0);
        //    //            Thread.Sleep(5000);

        //    //            var readResult2 = conn.ExecuteScalar<FactType>(sql);
        //    //            //output.WriteLine("{0}: {1} readResult2 ended: {2}", DateTime.Now, i, readResult2.Aggregate("", (acc, next) => acc + "\r\n\t" + next.ToString()));
        //    //            //readResult2.Count().Should().Be(0);
        //    //            output.WriteLine("{0}: {1} ENDED", DateTime.Now, id);
        //    //            return readResult2;
        //    //        },
        //    //        true,
        //    //        i
        //    //    );

        //    //}
        //    //Task.WaitAll(tasks2);

        //    //=======================================================

        //    output.WriteLine($"{MyStopWatch.Elapsed()}: All finished");
        //    output.WriteLine(ConnectionFactory.myLog);
        //}




        [Fact]
        public async Task MassiveConcurrentReadTransactionsWithoutASingleRollback()
        {
            //Attention: watch out for unnoticed exceptions in threads !!!

            //Following measures have a major infleunce on the number of SqlBusy errors:
            //- Adding to each connection: SQLite.BusyTimeout(_db, 10).  This seems to be the most efficient way to solve the SqlBusy/Locked issue.
            //- Having a read-thread open before the others, to close it after all the others
            //- Starting each thread with a slight delay like 15ms, although the thread runs much longer than 15ms.

            const int MAX2 = 60;
            //Max2 + 1 for the waiting at the end, and + 1 for the preThread
            //Barrier barrier = new Barrier(MAX2 + 1 + 1);
            Barrier barrier = new Barrier(MAX2 + 1);
            Thread[] threads = new Thread[MAX2];

            output.WriteLine($"{MyStopWatch.Start()}: BEGIN OF TESTS at {DateTime.Now}");

            string dbFolderName = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dbFullName = Path.Combine(dbFolderName, "jinaga.db");
            File.Delete(dbFullName);
            ConnectionFactory connFactory = new(dbFullName);
            output.WriteLine($"{MyStopWatch.Elapsed()}: New database created");

            //Having a read-thread open before the others, to close it after all the others, decreases the number of SqlBusy-errors significantly.
            //Thread preThread = new Thread((param) =>
            //{
            //    connFactory.WithTxn<string>((conn, id) =>
            //    {
            //        output.WriteLine($"{MyStopWatch.Elapsed()}: {id:D2} -- STARTED Read-Thread: {Thread.CurrentThread.ManagedThreadId}");

            //        string sql;
            //        sql = $"SELECT count(*) FROM fact_type";                    
            //        var readResult2 = conn.ExecuteScalar<FactType>(sql);

            //        Thread.Sleep(14000);

            //        readResult2 = conn.ExecuteScalar<FactType>(sql);

            //        output.WriteLine($"{MyStopWatch.Elapsed()}: {id:D2} -- Result read: count(*) = {readResult2}");
            //        output.WriteLine($"{MyStopWatch.Elapsed()}: {id:D2} -- ENDING Read-Thread: {Thread.CurrentThread.ManagedThreadId}");
            //        return readResult2;
            //    },
            //        true,
            //        (int)param
            //    );
            //    barrier.SignalAndWait();
            //}
            //);
            //preThread.Start(999);


            await Task.Delay(1000);


            for (int i = 0; i < MAX2; i++)
            {
                //Starting each thread with a slight delay is another way to decrease the number of SqlBusy-errors significantly
                //await Task.Delay(1);  // a delay of less then 15ms, will be converted into roughly 15ms

                threads[i] = new Thread((param) =>
                {
                    connFactory.WithTxn<string>((conn, id) =>
                    {
                        output.WriteLine($"{MyStopWatch.Elapsed()}: {id:D2} -- STARTED Read-Thread: {Thread.CurrentThread.ManagedThreadId}");

                        Thread.Sleep(4000);

                        string sql;
                        sql = $"SELECT count(*) FROM fact_type";
                        var readResult2 = conn.ExecuteScalar<FactType>(sql);

                        Thread.Sleep(2000);

                        readResult2 = conn.ExecuteScalar<FactType>(sql);

                        output.WriteLine($"{MyStopWatch.Elapsed()}: {id:D2} -- Result read: count(*) = {readResult2}");
                        output.WriteLine($"{MyStopWatch.Elapsed()}: {id:D2} -- ENDING Read-Thread: {Thread.CurrentThread.ManagedThreadId}");
                        return readResult2;
                    },
                        true,
                        (int)param
                    );
                    barrier.SignalAndWait();
                }
                );
                threads[i].Start(i);
            }
            barrier.SignalAndWait();

            output.WriteLine($"{MyStopWatch.Elapsed()}: END OF TESTS at {DateTime.Now}\n\r");

            output.WriteLine(ConnectionFactory.myLog);

            MyStopWatch.ElapsedMilliSeconds().Should().BeLessThan(8000, "that means there have been no rollbacks, as we like it");

        }


    }
}