using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace MarsDapperWrongResults
{
    class Program
    {
        private static void Main(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
            if (string.IsNullOrEmpty(connectionString))
                throw new Exception("Expected CONNECTION_STRING env variable");
            
            // will count how many responses contain correct result
            var successCount = 0;
            // will count how many responses return incorrect result
            var wrongCount = 0;
            // will count the number of exceptions
            var exceptionCount = 0;
            
            // will contain the list of results returned from threads
            var resultsFromThreads = new Queue<QueryResult>();
            
            // create many threads. all of them execute the same query but with different index
            var threads = new List<(Thread, ThreadState)>();
            for (var i = 0; i < 2000; i++)
            {
                // this object will be passed to every thread
                var threadInitState = new ThreadState
                {
                    ConnectionString = connectionString,
                    Index = i,
                    Finished = false,
                };
                
                var thread = new Thread((threadStateObj) =>
                {
                    // in thread, pull the data out of init object
                    var threadState = (ThreadState)threadStateObj;
                    string threadConnectionString;
                    int threadIndex;
                    lock (threadState)
                    {
                        threadConnectionString = threadState.ConnectionString;
                        threadIndex = threadState.Index;
                    }
                    
                    // perform the query
                    var queryResult = QueryAndVerify(threadIndex, threadConnectionString).Result;
                    
                    // put result back into queue
                    lock (resultsFromThreads) { resultsFromThreads.Enqueue(queryResult); }
                    // mark this thread as finished
                    lock (threadState) { threadState.Finished = true; }
                });
                
                // add thread and thread state to list for monitoring completion
                threads.Add((thread, threadInitState));
                // start the thread
                thread.Start(threadInitState);
            }
            
            // loop this until all threads are finished
            
            var finished = false;
            while (!finished)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
                
                // check for all finished
                
                finished = threads.All(item =>
                {
                    var state = item.Item2;
                    lock (state)
                    {
                        return state.Finished;
                    }
                });
                
                // pull all results from queue and print them
                
                var newResults = new List<QueryResult>();
                lock (resultsFromThreads)
                {
                    newResults.AddRange(resultsFromThreads);
                    resultsFromThreads.Clear();
                }

                foreach (var result in newResults)
                {
                    if (result.Success)
                    {
                        Console.WriteLine($"{result.Index} \tSuccess");
                        successCount += 1;
                    }
                    else if (result.Exception == null)
                    {
                        Console.WriteLine($"{result.Index} \tWrong result");
                        wrongCount += 1;
                    }
                    else
                    {
                        Console.WriteLine($"{result.Index} \t{result.Exception.Message}");
                        exceptionCount += 1;
                    }
                }
            }
            
            // not required, but join all threads before quiting
            foreach (var (t, _) in threads)
            {
                t.Join();
            }
            
            // print summary
            
            Console.WriteLine("Successful results: " + successCount);
            Console.WriteLine("Wrong results: " + wrongCount);
            Console.WriteLine("Exceptions: " + exceptionCount);
        }
        
        /// <summary>
        /// Perform a db query and verify if returned result matches.
        /// Return an object that contains success flag or an exception.
        /// </summary>
        private static async Task<QueryResult> QueryAndVerify(int index, string connectionString)
        {
            var noDapper = false; // flip this flag to run without dapper
            
            try
            {
                using (var cn = new SqlConnection(connectionString))
                {
                    await cn.OpenAsync();
                    var tx = cn.BeginTransaction(IsolationLevel.ReadCommitted);
                    var sql = @"select @Id as Id";
                    
                    if (noDapper)
                    {
                        var command = new SqlCommand(sql, cn, tx);
                        command.Transaction = tx;
                        command.CommandTimeout = 1;
                        command.Parameters.AddWithValue("Id", index);
                        
                        var result = -1;
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var columnIndex = reader.GetOrdinal("Id");
                                result = reader.GetInt32(columnIndex);
                                break;
                            }
                        }
                        
                        tx.Commit();

                        return new QueryResult
                        {
                            Success = result == index,
                            Index = index,
                        };
                    }
                    else
                    {
                        var commandDef = new CommandDefinition(
                            @"select @Id as Id",
                            new {Id = index},
                            transaction: tx,
                            1
                        );

                        var row = (await cn.QueryAsync<Row>(commandDef))
                            .FirstOrDefault();

                        tx.Commit();

                        return new QueryResult
                        {
                            Success = row?.Id == index,
                            Index = index,
                        };
                    }
                }
            }
            catch (Exception e)
            {
                return new QueryResult
                {
                    Success = false,
                    Exception = e,
                    Index = index,
                };
            }
        }
        
        private class Row
        {
            public int Id { get; set; }
        }
        
        private class QueryResult
        {
            public bool Success;
            public Exception Exception;
            public long Index;
        }
        
        private class ThreadState
        {
            public bool Finished;
            public string ConnectionString;
            public int Index;
        }
    }
}