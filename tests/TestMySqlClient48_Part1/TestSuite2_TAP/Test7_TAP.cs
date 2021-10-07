//MIT, 2015-present, brezza92, EngineKit and contributors

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SharpConnect.MySql;
using SharpConnect.MySql.AsyncPatt;

namespace MySqlTest
{
    public class TestSet7_TAP : MySqlTestSet
    {
        [Test]
        public static async void T_ChangeDbAsync()
        {
            //open ,cnang db sync, close
            var connStr = GetMySqlConnString();
            var conn = new MySqlConnection(connStr);
            
            await conn.OpenAsync();
            await conn.ChangeDbAsync("mysql");
            await conn.CloseAsync();
        }
        [Test]
        public static async void T_Ping_Async()
        {
            //open ,ping, close
            var connStr = GetMySqlConnString();
            var conn = new MySqlConnection(connStr);
            await conn.OpenAsync();
            bool pingResult = await conn.PingAsync();
            await conn.CloseAsync();

        }
        [Test]
        public static async void T_ResetConnection_Async()
        {
            //open ,ping, close
            var connStr = GetMySqlConnString();
            var conn = new MySqlConnection(connStr);
            await conn.OpenAsync();

            {
                var cmd = new MySqlCommand(new SqlStringTemplate("set @x=20;", false), conn);
                await cmd.ExecuteNonQueryAsync();
            }
            {
                var cmd = new MySqlCommand(new SqlStringTemplate("set @y=@x+10;", false), conn);
                await cmd.ExecuteNonQueryAsync();
            }
            {
                var cmd = new MySqlCommand(new SqlStringTemplate("select @x,@y", false), conn);

                await cmd.ExecuteReaderAsync(reader =>
                {
                    int x = reader.GetInt32(0);
                    int y = reader.GetInt32(1);

                    //test values
                    if (x != 20 || y != x + 10)
                    {
                        throw new NotSupportedException();
                    }
                });
            }

            //
            await conn.ResetConnectionAsync();

            {
                var cmd = new MySqlCommand(new SqlStringTemplate("select @x,@y", false), conn);
                await cmd.ExecuteReaderAsync(reader =>
                {
                    int x = reader.GetInt32(0);
                    int y = reader.GetInt32(1);

                    //test values after reset conn
                    if (x != 0 || y != 0)
                    {
                        throw new NotSupportedException();
                    }
                });
            }

            await conn.CloseAsync();
        }
        [Test]
        public static async void T_OpenAndClose_TAP()
        {

            System.Diagnostics.Stopwatch stopW = new System.Diagnostics.Stopwatch();
            stopW.Start();
            //----------------------------------------
            //1. form1: call another async method
            await DoTaskAsync();
            //--------------------------------------------
            //2. form2: create task, then start and wait
            var t2 = new Task(async () =>
            {
                var connStr = GetMySqlConnString();
                var conn = new MySqlConnection(connStr);
                conn.UseConnectionPool = true;
                await conn.OpenAsync();
                await conn.CloseAsync();
            });
            t2.Start();
            t2.Wait();
            //--------------------------------------------
            //form3: use simple helper, create task and run,store task in **named** var and  wait
            var t3 = Task.Run(async () =>
            {
                var connStr = GetMySqlConnString();
                var conn = new MySqlConnection(connStr);
                conn.UseConnectionPool = true;
                await conn.OpenAsync();
                await conn.CloseAsync();
            });
            await t3;//**
            //--------------------------------------------
            //form4: use simple helper, create task and run, store task in **anonymous** var and wait
            await Task.Run(async () =>
            {
                var connStr = GetMySqlConnString();
                var conn = new MySqlConnection(connStr);
                conn.UseConnectionPool = true;
                await conn.OpenAsync();
                await conn.CloseAsync();
            });
            //--------------------------------------------
            stopW.Stop();
            Report.WriteLine("avg:" + stopW.ElapsedTicks);

        }

        static async Task DoTaskAsync()
        {
            var connStr = GetMySqlConnString();
            var conn = new MySqlConnection(connStr);
            conn.UseConnectionPool = true;
            await conn.OpenAsync();
            await conn.CloseAsync();
        }
    }
}