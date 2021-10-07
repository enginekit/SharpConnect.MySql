//MIT, 2015-present, brezza92, EngineKit and contributors

using System;
using System.Collections.Generic;
using SharpConnect.MySql;
using SharpConnect.MySql.SyncPatt;
namespace MySqlTest
{

    public class TestSet2 : MySqlTestSet
    {
        [Test]
        public static void T_CallToBlankStoreProc()
        {
            //DELIMITER $$

            //DROP PROCEDURE IF EXISTS `test`.`blank_call` $$
            //CREATE PROCEDURE `test`.`blank_call` ()
            //BEGIN

            //END $$

            //DELIMITER;

            var connStr = GetMySqlConnString();
            var conn2 = new MySqlConnection(connStr);
            conn2.Open();
            conn2.ChangeDB("test");

            var cmd3 = new MySqlCommand("call blank_call()", conn2);
            var reader3 = cmd3.ExecuteReader();
            while (reader3.Read())
            {

            }
            reader3.Close();
            //---------

            conn2.Close();
        }

        static void InsertLargeData(MySqlConnection conn2)
        {
            int size = 1024 * 1000 * 22;//ok
                                        //int size = 1024 * 1000 * 10;//ok

            //int size = 1024;
            byte[] buffer = new byte[size];
            byte dd = 0;
            for (int i = 0; i < size; ++i)
            {
                buffer[i] = dd;
                dd++;
                if (dd > 200)
                {
                    dd = 0;//reset;
                }
            }

            SqlStringTemplate sql_template = new SqlStringTemplate("insert into table2(data) values(?d)");
            var cmd2 = new MySqlCommand(sql_template, conn2);
            cmd2.Parameters.AddWithValue("?d", buffer);
            cmd2.ExecuteNonQuery();//
            if (!cmd2.HasError)
            {
                uint inser_id = cmd2.LastInsertedId;
            }
            else
            {
                //error
            }

        }
        [Test]
        public static void T_LargeData()
        {
            //create data file that large than 16 MB
            //CREATE TABLE `table2` (
            //  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
            //  `data` longblob NOT NULL,
            //  PRIMARY KEY(`id`)
            //) ENGINE = MyISAM DEFAULT CHARSET = utf8

            //int size = 1024 * 50 * 1;//ok
            //int size = 1024 * 100 * 1;//ok

            //#if DEBUG

            //#endif
            var connStr = GetMySqlConnString();
            var conn2 = new MySqlConnection(connStr);
            conn2.Open();
            conn2.ChangeDB("test");

            //InsertLargeData(conn2);
            //---------
            var cmd3 = new MySqlCommand("select data from table2 where id=30", conn2);
            //var cmd3 = new MySqlCommand("select file from table3 limit 1", conn2);
            var reader3 = cmd3.ExecuteReader();
            while (reader3.Read())
            {
                byte[] data_buffer = reader3.GetBuffer(0);

                int len = data_buffer.Length;
            }
            reader3.Close();
            //---------

            conn2.Close();

        }

        [Test]
        public static void T_LongData()
        {
            var connStr = GetMySqlConnString();
            {

                var conn2 = new MySqlConnection(connStr);
                conn2.Open();
                conn2.ChangeDB("test");
                SqlStringTemplate sql_template = new SqlStringTemplate("insert into table1(name) values(?i)");
                for (int i = 0; i < 100000; ++i)
                {
                    var cmd2 = new MySqlCommand(sql_template, conn2);
                    cmd2.Parameters.AddWithValue("?i", i);
                    cmd2.ExecuteNonQuery();//
                }
                conn2.Close();
            }
            //--------------
            var conn = new MySqlConnection(connStr);
            conn.Open();
            conn.ChangeDB("test");
            {
                var cmd = new MySqlCommand("select * from table1", conn);
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {


                }
                reader.Close();
            }
            conn.Close();
        }
        [Test]
        public static void T_WaitTableLock()
        {
            var connStr = GetMySqlConnString();
            var conn2 = new MySqlConnection(connStr);
            conn2.Open();
            conn2.ChangeDB("test");
            var cmd2 = new MySqlCommand("lock tables table1 write", conn2);
            cmd2.ExecuteNonQuery();//

            //--------------
            System.Threading.ThreadPool.QueueUserWorkItem(o =>
            {
                var conn = new MySqlConnection(connStr);
                conn.Open();
                conn.ChangeDB("test");
                {
                    var cmd = new MySqlCommand("select * from table1", conn);
                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {


                    }
                    reader.Close();
                }
                conn.Close();
            });


            //--------------
            int i = 0;
            while (i < 1000)
            {
                i++;
                System.Threading.Thread.Sleep(100);
            }

            var cmd3 = new MySqlCommand("unlock tables", conn2);
            cmd3.ExecuteNonQuery();//
        }
        [Test]
        public static void T_Ping()
        {

            {
                var connStr = GetMySqlConnString();
                var conn = new MySqlConnection(connStr);
                conn.Open();
                bool ping = conn.Ping();
                conn.Close();
            }

            {
                //ping, expect err (no connection)

                var connStr = GetMySqlConnString();
                var conn = new MySqlConnection(connStr);
                conn.Open();
#if DEBUG
                conn.dbugMakeSocketError();
#endif
                bool ping = conn.Ping();
                conn.Close();
            }
        }
        [Test]
        public static void T_ResetConnection()
        {
            var connStr = GetMySqlConnString();
            var conn = new MySqlConnection(connStr);
            conn.Open();

            {
                var cmd = new MySqlCommand(new SqlStringTemplate("set @x=20;", false), conn);
                cmd.ExecuteNonQuery();
            }
            {
                var cmd = new MySqlCommand(new SqlStringTemplate("set @y=@x+10;", false), conn);
                cmd.ExecuteNonQuery();
            }
            {
                var cmd = new MySqlCommand(new SqlStringTemplate("select @x,@y", false), conn);
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    int x = reader.GetInt32(0);
                    int y = reader.GetInt32(1);

                    //test values
                    if (x != 20 || y != x + 10)
                    {
                        throw new NotSupportedException();
                    }
                }
                reader.Close();
            }

            //
            conn.ResetConnection();

            {
                var cmd = new MySqlCommand(new SqlStringTemplate("select @x,@y", false), conn);
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    int x = reader.GetInt32(0);
                    int y = reader.GetInt32(1);

                    //test values after reset conn
                    if (x != 0 || y != 0)
                    {
                        throw new NotSupportedException();
                    }
                }
                reader.Close();
            }


            conn.Close();
        }
        [Test]
        public static void T_InsertAndSelect()
        {
            try
            {
                int n = 1;
                Test(n, TimeUnit.Ticks, out long total, out long avg, () =>
                {
                    var connStr = GetMySqlConnString();
                    var conn = new MySqlConnection(connStr);
                    conn.UseConnectionPool = true;
                    conn.Open();

                    DropTableIfExists(conn);
                    CreateTable(conn);
                    for (int i = 0; i < 100; ++i)
                    {
                        InsertData(conn);
                    }
                    SelectDataBack(conn);
                    SelectDataBack2(conn);//test select decimal back
                    conn.Close();
                });
                Report.WriteLine("avg:" + avg);
            }
            catch (Exception ex)
            {

            }

        }
        static void DropTableIfExists(MySqlConnection conn)
        {
            string sql = "drop table if exists test001";
            var cmd = new MySqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }

        static void CreateTable(MySqlConnection conn)
        {
            string sql = "create table test001(col_id  int(10) unsigned not null auto_increment, col1 int(10)," +
                "col2 char(2),col3 varchar(255),col4 datetime,col5 decimal(5,2), col6 decimal(16,8), primary key(col_id) )";
            var cmd = new MySqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }
        static void InsertData(MySqlConnection conn)
        {
            string sql = "insert into test001(col1,col2,col3,col4,col5,col6) values(10,'AA','123456789','0001-01-01',5.1,10.2857948)";
            var cmd = new MySqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
            uint lastInsertId = cmd.LastInsertedId;
        }
        static void SelectDataBack(MySqlConnection conn)
        {
            string sql = "select * from test001";
            var cmd = new MySqlCommand(sql, conn);
#if DEBUG
            conn.dbugPleaseBreak = true;
#endif
            var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                //test immediate close
                //reader.Close();
                object o0 = reader.GetValue(0);
                object o1 = reader.GetValue(1);
                object o2 = reader.GetValue("col3");
                object o3 = reader.GetValue("col4");
                object o4 = reader.GetValue("col5");
                object o5 = reader.GetValue("col6");
            }
            reader.Close();
        }
        static void SelectDataBack2(MySqlConnection conn)
        {
            string sql = "select 1.0/3.0;";
            var cmd = new MySqlCommand(sql, conn);
            var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                object value = reader.GetValue(0);
            }
            reader.Close();
        }
    }
}