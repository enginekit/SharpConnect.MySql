//MIT, 2016-present, brezza92, EngineKit and contributors 
#if NET20
using System;
using System.Collections.Generic;
using SharpConnect.MySql;
using SharpConnect.MySql.SyncPatt;
using SharpConnect.MySql.Information;
using System.Text;

namespace MySqlTest
{
    public class Test_MySqlUtils : MySqlTestSet
    {
        [Test]
        public static void T_CreateDatabaseInfo_Manual()
        {
            //show all database
            var connStr = GetMySqlConnString();
            var conn = new MySqlConnection(connStr);
            conn.Open();
            //see MySql doc's => 3.4 Getting Information About Databases and Tables 
            var cmd = new MySqlCommand("show databases", conn);
            var reader = cmd.ExecuteReader();
            List<string> dbNames = new List<string>();
            while (reader.Read())
            {
                dbNames.Add(reader.GetString(0));
            }
            reader.Close();
            //----------
            //then show each table
            //and show how each table is create 

            foreach (string dbname in dbNames)
            {
                cmd = new MySqlCommand("use " + dbname, conn);
                cmd.ExecuteNonQuery();
                //-----------------------------------
                //To find out what tables the default database contains
                cmd = new MySqlCommand("show tables", conn);
                List<string> tableNames = new List<string>();
                var tableNameReader = cmd.ExecuteReader();
                while (tableNameReader.Read())
                {
                    tableNames.Add(tableNameReader.GetString(0));
                }
                tableNameReader.Close();
                //-----------------------------------
                //describe each table 
                foreach (string tableName in tableNames)
                {
                    cmd = new MySqlCommand("describe " + tableName, conn);
                    var descReader = cmd.ExecuteReader();
                    //columns
                    //Field..
                    //Type
                    //Null
                    //Key
                    //Default,
                    //Extra 
                    int field_col = descReader.GetOrdinal("Field"),
                    type_col = descReader.GetOrdinal("Type"),
                    null_col = descReader.GetOrdinal("Null"),
                    key_col = descReader.GetOrdinal("Key"),
                    default_col = descReader.GetOrdinal("Default"),
                    extra_col = descReader.GetOrdinal("Extra");
                    //---------------------
                    while (descReader.Read())
                    {
                        string fieldName = descReader.GetString(field_col);
                        string typeName = descReader.GetString(type_col);
                        string nullOrNot = descReader.GetString(null_col);
                        string key = descReader.GetString(key_col);
                        string defaultValue = descReader.GetString(default_col);
                        string extraInfo = descReader.GetString(extra_col);
                    }
                    descReader.Close();
                }
            }
            conn.Close();
        }
        [Test]
        public static void T_CreateDatabaseInfo_Utils()
        {
            var connStr = GetMySqlConnString();
            var conn = new MySqlConnection(connStr);
            conn.Open();

            MySqlDbServerInfo serverInfo = new MySqlDbServerInfo("test");
            serverInfo.ReloadDatabaseList(conn);
            foreach (MySqlDatabaseInfo db in serverInfo.Databases.Values)
            {
                db.ReloadTableList(conn, true);
                db.ReloadStoreFuncList(conn, true);
                db.ReloadStoreProcList(conn, true);

                foreach (MySqlTableInfo tbl in db.Tables)
                {
                    //we can find more detail from 'show create table ...' sql
                    string createTableSql = tbl.GetShowCreateTableSql(conn);

                }
                foreach (MySqlStoreProcInfo storeProc in db.StoreProcs)
                {
                    string createdBySql = storeProc.Sql;
                }
                foreach (MySqlStoreFuncInfo storeFunc in db.StoreFuncs)
                {
                    string createdBySql = storeFunc.Sql;
                }
            }
            //-------------------- 

            conn.Close();
        }

       
    }
}
#endif