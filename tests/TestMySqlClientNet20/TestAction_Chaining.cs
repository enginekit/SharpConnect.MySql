//MIT, 2015-present, brezza92, EngineKit and contributors

using System;
using System.Collections.Generic;
using SharpConnect.MySql;
using SharpConnect.MySql.AsyncPatt;

namespace MySqlTest
{
    public class TestSet15 : MySqlTestSet
    {
        [Test]
        public static void T_OpenAndClose()
        {
            var connStr = GetMySqlConnString();
            var conn = new MySqlConnection(connStr);
            conn.Open(() =>
            {
                var cmd = new MySqlCommand("select sysdate()", conn);
                cmd.ExecuteReader(reader =>
                {
                    var dtm = reader.GetDateTime(0);

                    reader.Close(() =>
                    {
                        //conn.Close(() =>
                        //{

                        //});
                    });
                });
            });
        }
    }
}