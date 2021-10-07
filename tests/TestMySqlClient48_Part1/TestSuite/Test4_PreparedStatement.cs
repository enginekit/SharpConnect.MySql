//MIT, 2015-present, brezza92, EngineKit and contributors

using System;
using System.Collections.Generic;
using SharpConnect.MySql;
using SharpConnect.MySql.SyncPatt;
namespace MySqlTest
{
    public class TestSet4_PreparedStatement : MySqlTestSet
    {
        [Test]
        public static void T_PrepareStatement()
        {
            var connStr = GetMySqlConnString();
            var conn = new MySqlConnection(connStr);
            conn.Open();
            {
                string sql = "drop table if exists test001";
                var cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }
            {
                string sql = "create table test001(col_id  int(10) unsigned not null auto_increment, col1 int(10)," +
                "col2 char(2),col3 varchar(255),col4 datetime, primary key(col_id) )";
                var cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }

            {
                string sql = "insert into test001(col1,col2,col3,col4) values(?col1,?col2,?col3,?col4)";
                var cmd = new MySqlCommand(sql, conn);
                cmd.Prepare();
                for (int i = 0; i < 100; ++i)
                {
                    var pars = cmd.Parameters;
                    pars.AddWithValue("?col1", 10);
                    pars.AddWithValue("?col2", "AA");
                    pars.AddWithValue("?col3", "0123456789");
                    pars.AddWithValue("?col4", "0001-01-01");
                    cmd.ExecuteNonQuery();
                }
            }
            {
                string sql = "select col1,col2 from test001 where col1>?col1_v";
                var cmd = new MySqlCommand(sql, conn);
                cmd.Prepare();
                cmd.Parameters.AddWithValue("?col1_v", 0);
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {

                }
                reader.Close();
            }
            conn.Close();
            Report.WriteLine("ok");
        }
        [Test]
        public static void T_PrepareStatement2()
        {
            var connStr = GetMySqlConnString();
            var conn = new MySqlConnection(connStr);
            conn.Open();
            {
                string sql = "drop table if exists test001";
                var cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }
            {
                string sql = "create table test001(col_id  int(10) unsigned not null auto_increment, col1 int(10)," +
                "col2 char(2),col3 varchar(255),col4 datetime, primary key(col_id) )";
                var cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }

            {
                //test unused prepare statement
                string sql = "insert into test001(col1,col2,col3,col4) values(?col1,?col2,?col3,?col4)";
                var cmd = new MySqlCommand(sql, conn);
                cmd.Prepare();
            }
            {
                //test unused prepare statement
                string sql = "insert into test001(col1,col2,col3,col4) values(?col1,?col2,?col3,?col4)";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    //auto close and dispose internal query status
                    cmd.Prepare();
                }
            }

            {
                string sql = "insert into test001(col1,col2,col3,col4) values(?col1,?col2,?col3,?col4)";
                var cmd = new MySqlCommand(sql, conn);
                for (int i = 0; i < 100; ++i)
                {
                    var pars = cmd.Parameters;
                    pars.AddWithValue("?col1", 10);
                    pars.AddWithValue("?col2", "AA");
                    pars.AddWithValue("?col3", "0123456789");
                    pars.AddWithValue("?col4", "0001-01-01");
                    cmd.ExecuteNonQuery();
                }
            }
            {
                string sql = "insert into test001(col1,col2,col3,col4) values(?col1,?col2,?col3,?col4)";
                var cmd = new MySqlCommand(sql, conn);
                cmd.Prepare();
                for (int i = 0; i < 100; ++i)
                {
                    var pars = cmd.Parameters;
                    pars.AddWithValue("?col1", 10);
                    pars.AddWithValue("?col2", "AA");
                    pars.AddWithValue("?col3", "0123456789");
                    pars.AddWithValue("?col4", "0001-01-01");
                    cmd.ExecuteNonQuery();
                }
                cmd.ClosePrepare();
            }
            {
                string sql = "select col1,col2 from test001 where col1>?col1_v";
                var cmd = new MySqlCommand(sql, conn);
                cmd.Prepare();
                cmd.Parameters.AddWithValue("?col1_v", 0);
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {

                }
                reader.Close();
            }
            conn.Close();
            Report.WriteLine("ok");
        }
        [Test]
        public static void T_PrepareStatement_withContext()
        {
            var connStr = GetMySqlConnString();
            var conn = new MySqlConnection(connStr);
            conn.Open();
            {
                string sql = "drop table if exists test001";
                var cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();

            }

            {
                string sql = "create table test001(col_id  int(10) unsigned not null auto_increment, col1 int(10)," +
                "col2 char(2),col3 varchar(255),col4 datetime, primary key(col_id) )";
                var cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }

            {
                string sql = "insert into test001(col1,col2,col3,col4) values(?col1,?col2,?col3,?col4)";
                var cmd = new MySqlCommand(sql, conn);
                cmd.Prepare();
                for (int i = 0; i < 100; ++i)
                {
                    var pars = cmd.Parameters;
                    pars.AddWithValue("?col1", 10);
                    pars.AddWithValue("?col2", "AA");
                    pars.AddWithValue("?col3", "0123456789");
                    pars.AddWithValue("?col4", "0001-01-01");
                    cmd.ExecuteNonQuery();
                }
                cmd.ClosePrepare();
            }
            {

                string sql = "select col1,col2 from test001 where col1>?col1_v";
                var cmd = new MySqlCommand(sql, conn);
                cmd.Prepare();
            }
            {
                string sql = "select col1,col2 from test001 where col1>?col1_v";
                var cmd = new MySqlCommand(sql, conn);
                cmd.Prepare();
                //cmd.Parameters.AddWithValue("?col1_v", 0);
                //var reader = cmd.ExecuteReader();
                //while (reader.Read())
                //{

                //}
                //reader.Close();
            }

            conn.Close();
            Report.WriteLine("ok");
        }



        [Test]
        public static void T_PrepareStatement_UnsignedFlags()
        {
            //test the unsigned flags of mysql prepare stmt protocol
            var connStr = GetMySqlConnString();
            var conn = new MySqlConnection(connStr);
            conn.Open();
            {
                string sql = "drop table if exists test001";
                var cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();

            }

            {
                string sql = "create table test001(col_id  int(10) unsigned not null auto_increment, col1 tinyint," +
                "col2 tinyint unsigned, primary key(col_id) )";
                var cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }

            {
                string sql = "insert into test001(col1,col2) values(?col1,?col2)";
                var cmd = new MySqlCommand(sql, conn);
                cmd.Prepare();
                cmd.SetOnErrorHandler(cmd2 =>
                {
                    //expect out of range
                });
                for (int i = 0; i < 2; ++i)
                {
                    var pars = cmd.Parameters;
                    pars.AddWithValue("?col1", 255);
                    pars.AddWithValue("?col2", 255);
                    cmd.ExecuteNonQuery();

                    if (cmd.HasError)
                    {
                        break;
                    }
                }
                cmd.ClosePrepare();
            }



            {
                string sql = "select col1,col2 from test001";
                var cmd = new MySqlCommand(sql, conn);
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var v1 = reader.GetInt8(0);
                    var v2 = reader.GetUInt8(1);
                }
                reader.Close();
            }
            conn.Close();
            Report.WriteLine("ok");
        }


    }
}