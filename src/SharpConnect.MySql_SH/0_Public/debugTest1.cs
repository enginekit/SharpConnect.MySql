using System;
using System.IO;
using SharpConnect.MySql.Internal;
using SharpConnect.MySql.SyncPatt;
namespace SharpConnect.MySql
{
#if DEBUG 
    public static class dbugInternal
    {
        public static void Test1()
        {
            //Test1_Select();
            Test1_Insert();
            //Test1_Update();
            //Test1_Delete();
        }

        public static void Test1_OldVersionTest()
        {
            //string filename;
            //filename = "TestMe.png";//216,362 bytes
            //filename = "Colorful.jpg";//885,264 bytes
            //filename = "TestJpg.jpg";//2,066 bytes
            byte[] buffer = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            //buffer = new byte[500500]; 
            //BinaryReader binaryReader = new BinaryReader(stReader);

            var ss = new System.Diagnostics.Stopwatch();
            ss.Start();
            string sql;
            string sql2;
            //please note the 
            //field or column binding is extension, must start with ??

            //sql = "INSERT INTO ??t1 (??c1, ??c2) VALUES (?n1 , ?buffer1)";
            sql = "INSERT INTO ??t1 SET ??c2 = ?buffer1";
            //sql = "select * from ??t1 where ??c1 > ?n1 and ?c1 < ?n2";
            //sql = "select * from ??t1 where ??c1 = 4579";


            //sql = "select 1+?n3 as test1";
            //sql = "select concat(?s1,?s2,?s1,?s2,?s1,?s2,?s1,?s2,?s1,?s2) as test1";
            //sql = "select concat(?s1,?s2,?s1,?s2) as test1";
            //sql = "SELECT Orders.OrderID, Customers.CustomerName, Orders.OrderDate"
            //    + " FROM Orders INNER JOIN Customers"
            //    + " ON Orders.CustomerID = Customers.CustomerID;";
            //sql2 = "select * from ?t1 where ?c1 > ?n1 and ?c1 < ?n2";
            //sql = "INSERT INTO ?t1 ( ?c2, ?c3) VALUES ( ?s1, ?s2)";
            //sql = "DELETE FROM ?t1 WHERE ?c1=?n1";
            //sql = "UPDATE ?t1 SET ?c2=?s1 WHERE ?c1=?n1";

            //CommandParameters cmdValues = new CommandParameters();
            //sql = "select ?n1+?n2 as test1";

            int testN1 = 4520;
            int testN2 = 4530;
            sql = "select * from ??t1 where ??c1 > ?n1 and ??c1 < ?n2";
            //sql = "select * from ?t1 where ?c1 = ?n2";
            //sql = "select ?n1+?n2 as test1";
            CommandParams cmd2Values = new CommandParams();
            cmd2Values.SetSqlPart("??t1", "saveimage");
            cmd2Values.SetSqlPart("??c1", "idsaveImage");
            //cmd2Values.AddField("c2", "saveImagecol");

            cmd2Values.AddWithValue("?n1", testN1);
            cmd2Values.AddWithValue("?n2", testN2);
            //cmd2Values.AddValue("n3", 29.5);

            //cmd2Values.AddValue("s1", "foo");
            //cmd2Values.AddValue("s2", "bar");
            //cmd2Values.AddValue("buffer1", buffer);

            ConnectionConfig config = new ConnectionConfig("localhost", "root", "root");
            config.database = "test";
            //MySqlConnection sqlConn = new MySqlConnection(config.host, config.user, config.password, config.database);
            //sqlConn.UseConnectionPool = true;
            //sqlConn.Open();
            //MySqlCommand command = new MySqlCommand(sql, sqlConn);
            //command.Parameters.AddTable("t1", "saveimage");
            //command.Parameters.AddField("c1", "idsaveImage");
            //command.Parameters.AddValue("n1", testN1);
            //command.Parameters.AddValue("n2", testN2);

            //var reader = command.ExecuteReader();
            //reader.Read();
            //Connection connection = sqlConn.Conn;/*ConnectionPool.GetConnection(new MySqlConnectionString(config.host, config.user, config.password, config.database));*/
            Connection connection = new Connection(config);
            if (connection == null)
            {
                connection = new Connection(config);
                connection.IsStoredInConnPool = false;
                connection.Connect();
            }

            int count = 3;


            int fCase = 1;
            for (int i = 0; i < count; i++)
            {
                int j = 0;
                //query = connection.CreateQuery(sql, cmdValues);
                //query = connection.CreateQuery(cmd2Values);
                //query.ExecutePrepareQuery(cmd2Values);
                var query = new Query(connection, sql, cmd2Values);
                query.SetResultListener(tableResult =>
                {
                    if (query.Error != null)
                    {
                        Console.WriteLine("Error : " + query.Error.message);
                    }
                    else if (query.OkPacket != null)
                    {
                        Console.WriteLine("i : " + i + ", OkPacket : [affectedRow] >> " + query.OkPacket.affectedRows);
                        Console.WriteLine("i : " + i + ", OkPacket : [insertId] >> " + query.OkPacket.insertId);
                    }
                    else
                    {
                        var thead = tableResult.tableHeader;

                        int col_idsaveImage = thead.GetFieldIndex("idsaveImage");
                        int col_saveImageCol = thead.GetFieldIndex("saveImagecol");
                        int col_test = thead.GetFieldIndex("test1");
                        //if (col_idsaveImage < 0 || col_saveImageCol < 0)
                        //{
                        //    throw new Exception();
                        //}
                        Console.WriteLine("Result : ");
                        //while (query.ReadRow())
                        //{
                        //    if (col_test == 0)
                        //    {
                        //        Console.WriteLine("Result of " + "test1 : >> " + query.Cells[col_test] + " <<");
                        //    }
                        //    else
                        //    {
                        //        Console.WriteLine("Id : " + query.Cells[col_idsaveImage]);
                        //        Console.WriteLine("Buffer size : " + query.Cells[col_saveImageCol].myBuffer.Length);
                        //    }
                        //    //Console.WriteLine(query.GetFieldData("myusercol1"));
                        //    if (++j > 3)
                        //    {
                        //        break;
                        //    }
                        //}
                    }
                });
                testN1 += 10;
                testN2 += 10;
                cmd2Values.AddWithValue("?n1", testN1);
                cmd2Values.AddWithValue("?n2", testN2);
                query.Execute(true); //*** 
                query.Close();
                connection.Disconnect();
                connection = new Connection(config);
                connection.Connect();
                //j = 0;
                //query = connection.CreateQuery(sql2, prepare);
                //query.ExecuteQuery();
                //if (query.loadError != null)
                //{
                //    Console.WriteLine("Error : " + query.loadError.message);
                //}
                //else
                //{
                //    while (query.ReadRow() && j < 3)
                //    {
                //        Console.WriteLine(query.GetFieldData("idsaveImage"));
                //        Console.WriteLine(query.GetFieldData("saveImagecol"));
                //        //Console.WriteLine(query.GetFieldData("myusercol1"));
                //        j++;
                //    }
                //}
                //query.Close();
            }

            ss.Stop();
            long avg = ss.ElapsedMilliseconds / count;
            Console.WriteLine("Counting : " + count + " rounds. \r\nAverage Time : " + avg + " ms");
            connection.Disconnect();
        }

        public static void Test1_Select()
        {
            ConnectionConfig config = new ConnectionConfig("localhost", "root", "root");
            config.database = "test";
            MySqlConnection sqlConn = new MySqlConnection(config.host, config.user, config.password, config.database);
            sqlConn.UseConnectionPool = true;
            sqlConn.InternalOpen();
            string sql = "select * from ??t1 where ??c1 > ?n1 and ??c1 < ?n2";
            int testN1 = 4510;
            int testN2 = 4520;
            MySqlCommand command = new MySqlCommand(sql, sqlConn);
            command.Parameters.SetSqlPart("??t1", "saveimage");
            command.Parameters.SetSqlPart("??c1", "idsaveImage");
            command.Parameters.AddWithValue("?n1", testN1);
            command.Parameters.AddWithValue("?n2", testN2);
            var reader = command.ExecuteReader();
            int count = 0;
            while (reader.Read())
            {
                Console.WriteLine("Id : " + reader.GetInt32(0));
                Console.WriteLine("Buffer size : " + reader.GetBuffer(1).Length);
                ++count;
                if (count >= 3)
                {
                    break;
                }
            }
            reader.Close();
        }

        public static void Test1_Insert()
        {
            //string filename = "TestMe.png";//216,362 bytes 
            byte[] buffer = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

            ConnectionConfig config = new ConnectionConfig("localhost", "root", "root");
            config.database = "test";
            MySqlConnection sqlConn = new MySqlConnection(config.host, config.user, config.password, config.database);
            sqlConn.UseConnectionPool = true;
            sqlConn.InternalOpen();
            string sql = "INSERT INTO ??t1 SET ??c2 = ?buffer1";
            MySqlCommand command = new MySqlCommand(sql, sqlConn);
            command.Parameters.SetSqlPart("??t1", "saveimage");
            command.Parameters.SetSqlPart("??c2", "saveImagecol");
            command.Parameters.AddWithValue("?buffer1", buffer);
            command.ExecuteNonQuery();
            sqlConn.InternalClose();
        }

        public static void Test1_Update()
        {
            ConnectionConfig config = new ConnectionConfig("localhost", "root", "root");
            config.database = "test";
            MySqlConnection sqlConn = new MySqlConnection(config.host, config.user, config.password, config.database);
            sqlConn.UseConnectionPool = true;
            sqlConn.InternalOpen();
            string sql = "select * from ??t1 where ??c1 > ?n1 and ??c1 < ?n2";
            int testN1 = 4510;
            int testN2 = 4520;
            MySqlCommand command = new MySqlCommand(sql, sqlConn);
            command.Parameters.SetSqlPart("??t1", "saveimage");
            command.Parameters.SetSqlPart("??c1", "idsaveImage");
            command.Parameters.AddWithValue("?n1", testN1);
            command.Parameters.AddWithValue("?n2", testN2);
        }

        public static void Test1_Delete()
        {
            ConnectionConfig config = new ConnectionConfig("localhost", "root", "root");
            config.database = "test";
            MySqlConnection sqlConn = new MySqlConnection(config.host, config.user, config.password, config.database);
            sqlConn.UseConnectionPool = true;
            sqlConn.InternalOpen();
            string sql = "select * from ??t1 where ??c1 > ?n1 and ??c1 < ?n2";
            int testN1 = 4510;
            int testN2 = 4520;
            MySqlCommand command = new MySqlCommand(sql, sqlConn);
            command.Parameters.SetSqlPart("??t1", "saveimage");
            command.Parameters.SetSqlPart("??c1", "idsaveImage");
            command.Parameters.AddWithValue("?n1", testN1);
            command.Parameters.AddWithValue("?n2", testN2);
            sqlConn.InternalClose();
        }


        static void TempHandshakeParse()
        {
            //MemoryStream ms = new MemoryStream(buffer);
            //BinaryReader reader = new BinaryReader(ms, Encoding.ASCII);
            //ms.Position = 4;
            //byte version = reader.ReadByte();
            //string serverVersion = reader.ReadNullTerminatedString();
            //uint thdId = reader.ReadUInt32();
            //byte[] scrambleBuffer1 = reader.ReadBytes(8);
            //byte filler1 = reader.ReadByte();
            //ushort serverCapabilities1 = reader.ReadUInt16();
            //byte serverLanguage = reader.ReadByte();
            //ushort serverStatus = reader.ReadUInt16();
            //bool protocal41 = (serverCapabilities1 & (1 << 9)) > 0;
            //byte[] scrambleBuff2 = null;
            //if (protocal41)
            //{
            //    ushort serverCapabilities2 = reader.ReadUInt16();
            //    byte scrambleLength = reader.ReadByte();
            //    byte[] filler2 = reader.ReadBytes(10);
            //    scrambleBuff2 = reader.ReadBytes(12);
            //    byte filler3 = reader.ReadByte();
            //}
            //else
            //{
            //    byte[] filer2 = reader.ReadBytes(13);
            //}
            ////----------------------------------------------------------------------
            ////then connect to the server
            //if (reader.BaseStream.Position == reader.BaseStream.Length)
            //{
            //    reader.Close();
            //    ms.Close();
            //    ms.Dispose();
            //    //end of the stream here
            //    return;
            //}
            ////-------------------------- 
            //string pluginData;// = reader.ReadNullTerminatedString();
            //if (reader.BaseStream.Position < count)
            //{
            //    pluginData = reader.ReadNullTerminatedString();
            //}

            ////-------------------------- 
            //reader.Close();
            //ms.Close();
            //ms.Dispose();
        }
        static void TempRawCode()
        {
            ////var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            //var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 3306);
            //socket.Connect(endpoint);
            ////socket.Connect("localhost", 3306);

            //byte packetNumber = 0;

            //byte[] buffer = new byte[512];
            //int count = socket.Receive(buffer);
            //HandshakePacket handshake = new HandshakePacket();
            //handshake.ParsePacket(buffer, count);
            //packetNumber++;
            //if (count < 512)
            //{
            //    ConnectionConfig config = new ConnectionConfig();
            //    config.user = "root";
            //    config.password = "root";
            //    config.database = "";

            //    byte[] token = MakeToken(config.password, GetScrollbleBuffer(handshake.scrambleBuff1, handshake.scrambleBuff2));

            //    //send data to server
            //    //System.IO.MemoryStream ms2 = new MemoryStream();
            //    ClientAuthenticationPacket authPacket = new ClientAuthenticationPacket();
            //    authPacket.SetValues(config.user, token, config.database, handshake.protocol41);
            //    authPacket.WritePacket(packetNumber++);

            //    byte[] arr = authPacket.ToArray();
            //    //ms2.Close();
            //    //ms2.Dispose();
            //    byte[] em = new byte[512];
            //    int a = socket.Send(arr);
            //    int re = socket.Receive(em);

            //    if (em[4] == 255)
            //    {
            //        ErrPacket errPacket = new ErrPacket();
            //        errPacket.ParsePacket(em, re);
            //    }
            //    else
            //    {
            //        Query query = new Query("select 1.1 + 1.1 as solution,'a'", null);

            //        OkPacket okPacket = new OkPacket(handshake.protocol41);
            //        okPacket.ParsePacket(em, re);

            //        ComQueryPacket queryPacket = new ComQueryPacket(query.sql);
            //        queryPacket.WritePacket(0);

            //        byte[] qr = queryPacket.ToArray();
            //        byte[] temp = new byte[512];
            //        int sent = socket.Send(qr);
            //        int recieve = socket.Receive(temp);

            //        if (temp[4] == 255)
            //        {
            //            ErrPacket errPacket = new ErrPacket();
            //            errPacket.ParsePacket(temp, recieve);
            //        }
            //        else
            //        {
            //            MemoryStream stream = new MemoryStream(temp,0,recieve);
            //            ResultSetHeaderPacket resultPacket = new ResultSetHeaderPacket();
            //            resultPacket.ParsePacket(stream);

            //            List<FieldPacket> fieldsList = new List<FieldPacket>();

            //            FieldPacket fieldPacket = new FieldPacket(handshake.protocol41);
            //            fieldPacket.ParsePacket(stream);

            //            FieldPacket fieldPacket2 = new FieldPacket(handshake.protocol41);
            //            fieldPacket2.ParsePacket(stream);

            //            fieldsList.Add(fieldPacket);
            //            fieldsList.Add(fieldPacket2);

            //            EofPacket fieldEof = new EofPacket(handshake.protocol41);//if temp[4]=0xfe then eof packet
            //            fieldEof.ParsePacket(stream);

            //            RowDataPacket rowData = new RowDataPacket(fieldsList, query.typeCast, query.nestTables, config);
            //            rowData.ParsePacket(stream);

            //            EofPacket rowDataEof = new EofPacket(handshake.protocol41);
            //            rowDataEof.ParsePacket(stream);
            //        }
            //    }
            //}
        }
    }
#endif
}