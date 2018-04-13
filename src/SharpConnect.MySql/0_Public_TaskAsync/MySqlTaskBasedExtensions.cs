﻿//MIT, 2016, brezza92, EngineKit and contributors  

using System.Threading.Tasks;
namespace SharpConnect.MySql.AsyncPatt
{




    public static class MySqlTaskBasedExtensions
    {
        public delegate void MySqlDataReaderReadDelegate(MySqlDataReader reader);
        public static Task OpenAsync(this MySqlConnection conn)
        {
            var tcs = new TaskCompletionSource<int>();
            conn.InternalOpen(() => tcs.SetResult(0));
            return tcs.Task;
        }
        public static Task CloseAsync(this MySqlConnection conn)
        {
            var tcs = new TaskCompletionSource<int>();
            conn.Close(() => tcs.SetResult(0));
            return tcs.Task;
        }
        //------------------------------------------------------------
        public static Task PrepareAsync(this MySqlCommand cmd)
        {
            var tcs = new TaskCompletionSource<int>();
            cmd.Prepare(() => tcs.SetResult(0));
            return tcs.Task;
        }
        public static void Stop(this MySqlDataReader reader)
        {
            reader.StopReadingNextRow = true;
        }


        public static Task ExecuteNonQueryAsync(this MySqlCommand cmd)
        {
            var tcs = new TaskCompletionSource<int>();
            cmd.ExecuteNonQuery(() => tcs.SetResult(0));
            return tcs.Task;
        }

        public static Task ExecuteReaderAsync(this MySqlCommand cmd, MySqlDataReaderReadDelegate readerDel)
        {
            var tcs = new TaskCompletionSource<int>();
            cmd.ExecuteReader(exec_reader =>
            {
                //reader is ready
                //then read
                //reader.InternalRead() may be blocked, 
                //so we use thread pool to notify 
                System.Threading.ThreadPool.QueueUserWorkItem(state =>
                {
                    while (exec_reader.InternalRead())
                    {
                        //
                        readerDel(exec_reader);
                        if (exec_reader.StopReadingNextRow)
                        {
                            //close the reader and break
                            break;
                        }
                    }
                    //
                    exec_reader.InternalClose();
                    tcs.SetResult(0);
                    //
                });

            });
            return tcs.Task;
        }

    }
}