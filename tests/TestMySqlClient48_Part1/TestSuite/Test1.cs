//MIT, 2015-present, brezza92, EngineKit and contributors

using System;
using System.Collections.Generic;
using SharpConnect.MySql;
using SharpConnect.MySql.SyncPatt;

namespace MySqlTest
{
    public class TestSet1 : MySqlTestSet
    {
        [Test]
        public static void T_OpenAndClose()
        {
            int n = 100;

            var connStr = GetMySqlConnString();
            Test(n, TimeUnit.Ticks, out long total, out long avg, () =>
            {
                var conn = new MySqlConnection(connStr);
                conn.Open();
                //connList.Add(conn);
                conn.Close();
            });
            Report.WriteLine("avg:" + avg);
        }
        [Test]
        public static void T_OpenNotClose()
        {
            int n = 100;

            var connStr = GetMySqlConnString();
            List<MySqlConnection> connList = new List<MySqlConnection>();
            Test(n, TimeUnit.Ticks, out long total, out long avg, () =>
            {
                var conn = new MySqlConnection(connStr);
                conn.Open();
                connList.Add(conn);
            });
            Report.WriteLine("avg:" + avg);
            //clear
            foreach (var conn in connList)
            {
                conn.Close();
            }
            connList.Clear();
        }
        [Test]
        public static void T_OpenAndCloseWithConnectionPool()
        {
            int n = 100;
            var connStr = GetMySqlConnString();
            Test(n, TimeUnit.Ticks, out long total, out long avg, () =>
            {
                var conn = new MySqlConnection(connStr);
                conn.UseConnectionPool = true;
                conn.Open();
                conn.Close();
            });
            Report.WriteLine("avg:" + avg);
        }


        [Test]
        public static void T_Select_WithError()
        {
            int n = 100;

            var connStr = GetMySqlConnString();
            var conn = new MySqlConnection(connStr);
            conn.Open();
            //Test(n, TimeUnit.Ticks, out total, out avg, () =>
            //{
            try
            {
                var cmd = new MySqlCommand("select sysdatex()", conn);
                var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    var dtm = reader.GetDateTime(0);
                }
                reader.Close();
            }
            catch (Exception ex)
            {

            }

            //});
            //Report.WriteLine("avg:" + avg);
            conn.Close();
        }
        [Test]
        public static void T_Select_sysdate()
        {
            int n = 100;

            var connStr = GetMySqlConnString();
            var conn = new MySqlConnection(connStr);
            conn.Open();
            Test(n, TimeUnit.Ticks, out long total, out long avg, () =>
            {
                try
                {
                    var cmd = new MySqlCommand("select sysdate()", conn);
                    var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        var dtm = reader.GetDateTime(0);
                    }
                    reader.Close();
                }
                catch (Exception ex)
                {

                }

            });
            Report.WriteLine("avg:" + avg);
            conn.Close();
        }
        [Test]
        public static void T_Select_ExecuteScalar()
        {
            int n = 100;

            var connStr = GetMySqlConnString();
            var conn = new MySqlConnection(connStr);
            conn.Open();
            Test(n, TimeUnit.Ticks, out long total, out long avg, () =>
            {
                var cmd = new MySqlCommand("select sysdate()", conn);
                object result = cmd.ExecuteScalar();
            });
            Report.WriteLine("avg:" + avg);
            conn.Close();
        }
        [Test]
        public static void T_CreateTable()
        {
            var connStr = GetMySqlConnString();
            var conn = new MySqlConnection(connStr);
            conn.Open();
            var cmd = new MySqlCommand("create table user_info2(uid int(10),u_name varchar(45));", conn);
            cmd.ExecuteNonQuery();
            Report.WriteLine("ok");
            conn.Close();
        }

        [Test]
        public static void T_DropCreateInsert()
        {
            var connStr = GetMySqlConnString();
            var conn = new MySqlConnection(connStr);
            conn.Open();
            //1. drop table
            {
                var cmd = new MySqlCommand("drop table if exists user_info2", conn);
                cmd.ExecuteNonQuery();
            }
            //2. create new one
            {
                var cmd = new MySqlCommand("create table user_info2(uid int(10),u_name varchar(45));", conn);
                cmd.ExecuteNonQuery();
            }
            //3. add some data
            {
                var cmd = new MySqlCommand("insert into user_info2(uid, u_name) values(?uid, 'abc')", conn);
                cmd.Parameters.AddWithValue("?uid", 10);
                cmd.ExecuteNonQuery();
            }

            Report.WriteLine("ok");
            conn.Close();
        }

        [Test]
        public static void T_StringEscape()
        {
            var connStr = GetMySqlConnString();
            var conn = new MySqlConnection(connStr);
            conn.Open();
            //1. drop table
            {
                var cmd = new MySqlCommand("drop table if exists user_info2", conn);
                cmd.ExecuteNonQuery();
            }
            //2. create new one
            {
                var cmd = new MySqlCommand("create table user_info2(uid int(10),u_name varchar(45));", conn);
                cmd.ExecuteNonQuery();
            }
            //3. add some data
            {
                var cmd = new MySqlCommand("insert into user_info2(uid, u_name) values(?uid, '?????')", conn);
                cmd.Parameters.AddWithValue("?uid", 10);
                cmd.ExecuteNonQuery();
            }

            Report.WriteLine("ok");
            conn.Close();
        }

        [Test]
        public static void T_DateTimeData()
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
                string sql = "create table test001(col_id  int(10) unsigned not null auto_increment, myname varchar(20), col1 datetime," +
                "col2 date,col3 time,col4 timestamp, primary key(col_id) )";
                var cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }

            {
                string sql = "insert into test001(myname,col1,col2,col3,col4) values(?myname,?col1,?col2,?col3,?col4)";
                var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("?myname", "OKOK!");
                cmd.Parameters.AddWithValue("?col1", DateTime.Now);
                cmd.Parameters.AddWithValue("?col2", DateTime.Now);
                cmd.Parameters.AddWithValue("?col3", DateTime.Now);
                cmd.Parameters.AddWithValue("?col4", DateTime.Now);
                cmd.ExecuteNonQuery();
            }
            conn.Close();
            Report.WriteLine("ok");
        }

        [Test]
        public static void T_StringData1()
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
                string sql = "create table test001(col_id  int(10) unsigned not null auto_increment, myname varchar(20), col1 char(2)," +
                "col2 varchar(10), primary key(col_id) )";
                var cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }

            {
                string sql = "insert into test001(myname,col1,col2) values(?myname,?col1,?col2)";
                var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("?myname", "OKOK!");
                cmd.Parameters.AddWithValue("?col1", "OK"); //2
                cmd.Parameters.AddWithValue("?col2", "1000");
                cmd.ExecuteNonQuery();
            }


            conn.Close();
            Report.WriteLine("ok");
        }
        [Test]
        public static void T_StringData2_DataIsTooLong()
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
                string sql = "create table test001(col_id  int(10) unsigned not null auto_increment, myname varchar(20), col1 char(2)," +
                "col2 varchar(10), primary key(col_id) )";
                var cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }

            {
                string sql = "insert into test001(myname,col1,col2) values(?myname,?col1,?col2)";
                var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("?myname", "OKOK!");
                cmd.Parameters.AddWithValue("?col1", "OK1"); //width =2 ,so  in MySQL 5.6 strict mode, err-> data is too long for column
                cmd.Parameters.AddWithValue("?col2", "1000");
                cmd.ExecuteNonQuery();
            }
            conn.Close();
            Report.WriteLine("ok");
        }

        [Test]
        public static void T_StringData3()
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
                string sql = "create table test001(col_id  int(10) unsigned not null auto_increment, myname varchar(20), col1 char(2)," +
                "col2 varchar(10), primary key(col_id) )";
                var cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }

            {
                string sql = "insert into test001(myname,col1,col2) values(?myname,?col1,?col2)";
                var cmd = new MySqlCommand(sql, conn);
                cmd.SetOnErrorHandler(err =>
                {

                });
                cmd.Prepare();
                cmd.Parameters.AddWithValue("?myname", "OKOK!");
                cmd.Parameters.AddWithValue("?col1", "OK"); //width =2 ,so  in MySQL 5.6 strict mode, err-> data is too long for column
                cmd.Parameters.AddWithValue("?col2", "1000");
                cmd.ExecuteNonQuery();
            }
            conn.Close();
            Report.WriteLine("ok");
        }
        [Test]
        public static void T_NullData()
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
                string sql = "create table test001(col_id  int(10) unsigned not null auto_increment, myname varchar(20),mydtm datetime, col1 char(2)," +
                "col2 varchar(10), primary key(col_id) )";
                var cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }

            {
                string sql = "insert into test001(myname) values(?myname)";
                var cmd = new MySqlCommand(sql, conn);
                cmd.Prepare();
                cmd.Parameters.AddWithValue("?myname", "\"a"); //empty string

                //cmd.Parameters.AddWithValue("?col1", "OK1"); //width =2 ,so  in MySQL 5.6 strict mode, err-> data is too long for column
                //cmd.Parameters.AddWithValue("?col2", "1000");
                cmd.ExecuteNonQuery();
                //ppctx.Close(); 
            }

            {
                string sql = "select col2,myname,mydtm from test001";
                var cmd = new MySqlCommand(sql, conn);
                cmd.Prepare();
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string f0 = reader.GetString(0);
                    string f1 = reader.GetString("myname");
                    DateTime dtm = reader.GetDateTime(2);
                }
                reader.Close();
            }



            conn.Close();
            Report.WriteLine("ok");
        }

        [Test]
        public static void T_NumRange()
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
                string sql = "create table test001(col_id  int(10) unsigned not null auto_increment," +
                    "num1 int,num2 int unsigned, " +
                    "num3 smallint, num4 smallint unsigned, " +
                    "num5 bigint, num6 bigint unsigned, " +
                    "num7 tinyint, num8 tinyint unsigned, " +
                    "num9 decimal(32,2), num10 decimal(32,2) unsigned," +
                " primary key(col_id) )";
                var cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }

            {
                string sql = "insert into test001(num1,num2,num3,num4,num5,num6,num7,num8,num9,num10) values(?num1,?num2,?num3,?num4,?num5,?num6,?num7,?num8,?num9,?num10)";
                var cmd = new MySqlCommand(sql, conn);
                cmd.Prepare();
                var pars = cmd.Parameters;
                //ok
                pars.AddWithValue("?num1", -10);
                pars.AddWithValue("?num2", 20);
                pars.AddWithValue("?num3", -10);
                pars.AddWithValue("?num4", 20);
                pars.AddWithValue("?num5", -10);
                pars.AddWithValue("?num6", 20);
                pars.AddWithValue("?num7", -10);
                pars.AddWithValue("?num8", 20);
                pars.AddWithValue("?num9", -10);
                pars.AddWithValue("?num10", 20);
                cmd.ExecuteNonQuery();
                //---------------------------

                pars.ClearDataValues();
                pars.AddWithValue("?num1", -10);
                pars.AddWithValue("?num2", 20);
                pars.AddWithValue("?num3", (short)-10);
                pars.AddWithValue("?num4", (ushort)20);
                pars.AddWithValue("?num5", (long)-10);
                pars.AddWithValue("?num6", (ulong)20);
                pars.AddWithValue("?num7", (sbyte)-10);
                pars.AddWithValue("?num8", (byte)20);
                pars.AddWithValue("?num9", (decimal)-10);
                pars.AddWithValue("?num10", (decimal)20);
                cmd.ExecuteNonQuery();
                //---------------------------
                pars.ClearDataValues();
                pars.AddWithValue("?num1", int.MinValue);
                pars.AddWithValue("?num2", uint.MaxValue);
                pars.AddWithValue("?num3", short.MinValue);
                pars.AddWithValue("?num4", ushort.MaxValue);
                pars.AddWithValue("?num5", long.MinValue);
                pars.AddWithValue("?num6", ulong.MaxValue);
                pars.AddWithValue("?num7", sbyte.MinValue);
                pars.AddWithValue("?num8", byte.MaxValue);
                pars.AddWithValue("?num9", decimal.MinValue);
                pars.AddWithValue("?num10", decimal.MaxValue);
                cmd.ExecuteNonQuery();
                //---------------------------
                //expected errors ...
                //---------------------------
                //pars.ClearDataValues();
                //pars.AddWithValue("?num1", -10); //ok -unsigned
                //pars.AddWithValue("?num2", -20); //err -no record insert
                //cmd.ExecuteNonQuery();
                //---------------------------
                //pars.ClearDataValues();
                //pars.AddWithValue("?num1", int.MinValue); //ok -unsigned
                //pars.AddWithValue("?num2", uint.MaxValue); //err -no record insert
                //cmd.ExecuteNonQuery();
                ////---------------------------
            }


            conn.Close();
            Report.WriteLine("ok");
        }

        [Test]
        public static void T_FloatingRange()
        {
            MySqlConnectionString connStr = GetMySqlConnString();
            var conn = new MySqlConnection(connStr);
            conn.Open();
            {
                string sql = "drop table if exists test002";
                var cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }

            {
                //num3 decimal(65,30) is max range is possible
                //num3 decimal if not define range defualt is decimal(10,0)
                string sql = "create table test002(col_id int(10) unsigned not null auto_increment," +
                    "num1 float, num2 double, num3 decimal(65,30), primary key(col_id) )";
                var cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }

            {
                string sql = "insert into test002 (num1, num2, num3) values (?num1, ?num2, ?num3)";
                var cmd = new MySqlCommand(sql, conn);
                cmd.Prepare();
                var pars = cmd.Parameters;
                pars.AddWithValue("?num1", 10.15);
                pars.AddWithValue("?num2", -101.5);
                pars.AddWithValue("?num3", 1015.00);
                cmd.ExecuteNonQuery();
                pars.ClearDataValues();
                pars.AddWithValue("?num1", 10.15d);
                pars.AddWithValue("?num2", -101.5f);
                pars.AddWithValue("?num3", (decimal)1015.00);
                cmd.ExecuteNonQuery();
                pars.ClearDataValues();
                pars.AddWithValue("?num1", float.MaxValue);
                pars.AddWithValue("?num2", double.MaxValue);
                //decimal of C# have the number of digits to the right of the decimal point less or equal 5 digits
                pars.AddWithValue("?num3", decimal.MaxValue);
                cmd.ExecuteNonQuery();
                pars.ClearDataValues();
                pars.AddWithValue("?num1", float.MinValue);
                pars.AddWithValue("?num2", double.MinValue);
                pars.AddWithValue("?num3", decimal.MinValue);
                cmd.ExecuteNonQuery();
            }

            conn.Close();
            Report.WriteLine("ok");
        }
    }
}