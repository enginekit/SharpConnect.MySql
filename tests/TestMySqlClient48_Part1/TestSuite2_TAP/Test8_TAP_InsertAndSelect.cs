//MIT, 2015-present, brezza92, EngineKit and contributors

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SharpConnect.MySql;
using SharpConnect.MySql.AsyncPatt;

namespace MySqlTest
{
    public class TestSet8_TAP : MySqlTestSet
    {
        [Test]
        public static async void T_InsertAndSelect_TAP()
        {

            System.Diagnostics.Stopwatch stopW = new System.Diagnostics.Stopwatch();
            stopW.Start();
            await Task.Run(async () =>
            {
                var connStr = GetMySqlConnString();
                var conn = new MySqlConnection(connStr);
                conn.UseConnectionPool = true;
                await conn.OpenAsync();
                //------------------------------------------
                //drop table if exist
                {
                    string sql = "drop table if exists test001";
                    var cmd = new MySqlCommand(sql, conn);
                    await cmd.ExecuteNonQueryAsync();
                }
                //------------------------------------------ 
                {
                    string sql = "create table test001(col_id  int(10) unsigned not null auto_increment, col1 int(10)," +
                    "col2 char(2),col3 varchar(255),col4 datetime, primary key(col_id) )";
                    var cmd = new MySqlCommand(sql, conn);
                    await cmd.ExecuteNonQueryAsync();
                }
                //---------------------------------------------
                for (int i = 0; i < 100; ++i)
                {
                    string sql = "insert into test001(col1,col2,col3,col4) values(10,'AA','123456789','0001-01-01')";
                    var cmd = new MySqlCommand(sql, conn);
                    await cmd.ExecuteNonQueryAsync();
                }
                {

                    //test prepared statement
                    string sql = "insert into test001(col1,col2,col3,col4) values(?col1,?col2,?col3,?col4)";
                    var cmd = new MySqlCommand(sql, conn);
                    await cmd.PrepareAsync(); //prepare  
                    CommandParams pars = cmd.Parameters;
                    for (int i = 0; i < 100; ++i)
                    {
                        pars.AddWithValue("?col1", 1000 + i);
                        pars.AddWithValue("?col2", "AA");
                        pars.AddWithValue("?col3", "how are you" + i);
                        pars.AddWithValue("?col4", DateTime.Now);
                        await cmd.ExecuteNonQueryAsync();
                        pars.Clear();
                    }
                    await cmd.ClosePrepareAsync();
                }



                //--------------------------------------------
                //select back
                {
                    string sql = "select * from test001";
                    var cmd = new MySqlCommand(sql, conn);
#if DEBUG
                    conn.dbugPleaseBreak = true;
#endif

                    int count = 0;
                    await cmd.ExecuteReaderAsync(reader =>
                    {
                        count++;
                        //if (count > 10)
                        //{
                        //    //test stop reader 
                        //reader.Stop();
                        //}
                    });
                }
            });
            //--------------------------------------------
            stopW.Stop();
            Report.WriteLine("avg:" + stopW.ElapsedTicks);
        }

    }
}