﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace RedisCore
{
    public class Program
    {
        const int PipelinedCount = 500000, RequestResponseCount = 10000,
            BatchSize = 1000, BatchCount = PipelinedCount / BatchSize,
            CorpusLoops = 10;
        static string[] GetCorpus()
        {
            return GetCorpus("TaleOfTwoCities.txt") ?? GetCorpus("../TaleOfTwoCities.txt") ?? new string[0];
        }

        static string[] GetCorpus(string path)
        {
            return File.Exists(path) ? File.ReadAllLines(path) : null;
        }
        static void Collect()
        {
            for (int i = 0; i < 5; i++)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
            }
        }
        public static void Main()
        {
            Thread.CurrentThread.Name = "Main";

            //RunTest<NetworkStreamClientChannelFactory>();

            RunTest<SocketConnectionClientChannelFactory>();

            //RunTest<UvClientChannelFactory>();
        }

        const int UpdateUIEvery = 1000;
        public static void RunTest<T>() where T : ClientChannelFactory, new()
        {
            
            using (var factory = new T())
            using (var conn = new RedisConnection())
            {
                Console.WriteLine($"Channel factory {factory} connecting...");
                conn.Connect(factory, "127.0.0.1:6379");

                Thread.Sleep(1000);
                if (conn.IsConnected)
                {
                    Console.WriteLine("RedisCore (networking hub of StackExchange.Redis using 'channels') Connected successfully");
                }
                else
                {
                    Console.WriteLine("Failed to connect; is redis running?");
                    return;
                }
                Stopwatch timer;

                Console.WriteLine();
                Console.WriteLine($"Sending {PipelinedCount} pings synchronously fire-and-forget (pipelined) ...");
                Collect();
                timer = Stopwatch.StartNew();
                // starting at 1 so that we can wait on the last one and still send the right amount
                for (int i = 1; i < PipelinedCount; i++)
                {
                    conn.Ping(fireAndForget: true);
                    if ((i % UpdateUIEvery) == 0) Console.Write('.');
                }
                conn.Ping(); // block
                timer.Stop();
                Console.WriteLine();
                Console.WriteLine($"{timer.ElapsedMilliseconds}ms; {((PipelinedCount * 1000.0) / timer.ElapsedMilliseconds):F0} ops/s");

                Console.WriteLine();
                Console.WriteLine($"Sending {(BatchSize * BatchCount) + 1} pings synchronously fire-and-forget ({BatchCount} batches of {BatchSize}) ...");
                Collect();
                timer = Stopwatch.StartNew();

                int total = 0;
                for (int i = 0; i < BatchCount; i++)
                {
                    var batch = conn.CreateBatch();
                    for (int j = 0; j < BatchSize; j++)
                    {
                        batch.PingAysnc(true);
                        if ((total++ % UpdateUIEvery) == 0) Console.Write('.');

                    }
                    batch.Execute();
                }
                conn.Ping(); // block
                timer.Stop();
                Console.WriteLine();
                Console.WriteLine($"{timer.ElapsedMilliseconds}ms; {((((BatchSize * BatchCount) + 1) * 1000.0) / timer.ElapsedMilliseconds):F0} ops/s");

                Console.WriteLine();
                Console.WriteLine($"Sending {RequestResponseCount} pings synchronously req/resp/req/resp/...");
                Collect();
                timer = Stopwatch.StartNew();
                for (int i = 0; i < RequestResponseCount; i++)
                {
                    conn.Ping();
                    if ((i % UpdateUIEvery) == 0) Console.Write('.');
                }
                timer.Stop();
                Console.WriteLine();
                Console.WriteLine($"{timer.ElapsedMilliseconds}ms; {((RequestResponseCount * 1000.0) / timer.ElapsedMilliseconds):F0} ops/s");

                Console.WriteLine();
                Console.WriteLine("Loading corpus...");
                var corpus = GetCorpus();
                var received = new string[corpus.Length];
                Console.WriteLine($"Sending {CorpusLoops * corpus.Length} echoes synchronously req/resp/req/resp/...");
                Collect();

                total = 0;
                timer = Stopwatch.StartNew();
                for (int j = 0; j < CorpusLoops; j++)
                {
                    for (int i = 0; i < corpus.Length; i++)
                    {
                        received[i] = conn.Echo(corpus[i]);
                        if ((total++ % UpdateUIEvery) == 0) Console.Write('.');
                    }
                }
                timer.Stop();
                Console.WriteLine();
                Console.WriteLine($"{timer.ElapsedMilliseconds}ms; {((CorpusLoops * corpus.Length * 1000.0) / timer.ElapsedMilliseconds):F0} ops/s");
                Console.WriteLine($"Correct data received: {received.SequenceEqual(corpus)}");

                PingAsync(conn);


                Console.WriteLine();
                factory.Shutdown(conn.Channel);
            }

        }

        private static async void PingAsync(RedisConnection conn)
        {
            try
            {
                Console.WriteLine();
                Console.WriteLine($"Sending {PipelinedCount} pings asynchronously fire-and-forget (pipelined) ...");
                Collect();
                var timer = Stopwatch.StartNew();
                // starting at 1 so that we can wait on the last one and still send the right amount
                for (int i = 1; i < PipelinedCount; i++)
                {
                    await conn.PingAsync(fireAndForget: true);
                    if ((i % UpdateUIEvery) == 0) Console.Write('.');
                }
                Console.WriteLine("waiting for final ping...");
                await conn.PingAsync(); // block
                Console.WriteLine("Final ping received...");
                timer.Stop();
                Console.WriteLine($"{timer.ElapsedMilliseconds}ms; {((PipelinedCount * 1000.0) / timer.ElapsedMilliseconds):F0} ops/s");

                Console.WriteLine();
                Console.WriteLine($"Sending {(BatchSize * BatchCount) + 1} pings asynchronously fire-and-forget ({BatchCount} batches of {BatchSize}) ...");
                Collect();
                timer = Stopwatch.StartNew();

                Task ignored = null;
                int total = 0;
                for (int i = 0; i < BatchCount; i++)
                {
                    var batch = conn.CreateBatch();
                    for (int j = 0; j < BatchSize; j++)
                    {
                        ignored = batch.PingAysnc(true);
                        if ((total++ % UpdateUIEvery) == 0) Console.Write('.');
                    }
                    await batch.ExecuteAsync();
                }
                await conn.PingAsync(); // block
                timer.Stop();
                Console.WriteLine();
                Console.WriteLine($"{timer.ElapsedMilliseconds}ms; {((((BatchSize * BatchCount) + 1) * 1000.0) / timer.ElapsedMilliseconds):F0} ops/s");

                Console.WriteLine();
                Console.WriteLine($"Sending {RequestResponseCount} pings asynchronously req/resp/req/resp/...");
                Collect();
                timer = Stopwatch.StartNew();
                for (int i = 0; i < RequestResponseCount; i++)
                {
                    await conn.PingAsync();
                    if ((i % UpdateUIEvery) == 0) Console.Write('.');
                }
                timer.Stop();
                Console.WriteLine();
                Console.WriteLine($"{timer.ElapsedMilliseconds}ms; {((RequestResponseCount * 1000.0) / timer.ElapsedMilliseconds):F0} ops/s");
            } catch(Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
            }
        }

        [Conditional("DEBUG")]
        internal static void WriteStatus(string message)
        {
            var thread = Thread.CurrentThread;
            Console.WriteLine($"[{thread.ManagedThreadId}:{thread.Name}] {message}");
        }

        internal static void WriteError(Exception ex, [CallerMemberName] string caller = null)
        {
            Console.Error.WriteLine($"{caller} threw {ex.GetType().Name}");
            Console.Error.WriteLine(ex.Message);
        }
    }




}