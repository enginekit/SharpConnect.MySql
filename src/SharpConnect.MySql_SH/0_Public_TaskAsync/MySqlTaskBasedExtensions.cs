//MIT, 2016-present, brezza92, EngineKit and contributors 
using System;
#if !NET20
using System.Threading.Tasks;
namespace SharpConnect.MySql.AsyncPatt
{

    public static class MySqlTaskBasedExtensions
    {

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
        public static Task ClosePrepareAsync(this MySqlCommand cmd)
        {
            var tcs = new TaskCompletionSource<int>();
            cmd.ClosePrepare(() => tcs.SetResult(0));
            return tcs.Task;
        }
        public static Task<bool> PingAsync(this MySqlConnection conn)
        {
            var tcs = new TaskCompletionSource<bool>();
            conn.Ping(() => tcs.SetResult(conn.LatestPingResult));
            return tcs.Task;
        }
        public static Task ChangeDbAsync(this MySqlConnection conn, string newDbName)
        {
            var tcs = new TaskCompletionSource<int>();
            conn.ChangeDB(newDbName, () => tcs.SetResult(0));
            return tcs.Task;
        }
        public static Task ResetConnectionAsync(this MySqlConnection conn)
        {
            var tcs = new TaskCompletionSource<int>();
            conn.ResetConnection(() => tcs.SetResult(0));
            return tcs.Task;
        }
        public static Task ExecuteNonQueryAsync(this MySqlCommand cmd)
        {
            var tcs = new TaskCompletionSource<int>();
            //TODO: add/test exception handler
            cmd.ExecuteNonQuery(() => tcs.SetResult(0));
            return tcs.Task;
        }

        /// <summary>
        /// execute reader, loop for each row and close
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="readerDel"></param>
        /// <returns></returns>
        public static Task ExecuteReaderAsync(this MySqlCommand cmd, Action<MySqlDataReader> readerDel)
        {

            var tcs = new TaskCompletionSource<int>();
            cmd.InternalExecuteReader(exec_reader =>
            {
                //reader is ready
                //then read
                //reader.InternalRead() may be blocked, 
                //so we use thread pool to notify 
                System.Threading.ThreadPool.QueueUserWorkItem(state =>
                {
                    while (exec_reader.InternalRead())
                    {
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
#endif