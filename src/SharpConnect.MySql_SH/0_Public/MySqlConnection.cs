//MIT, 2015-present, brezza92, EngineKit and contributors

using System;
using SharpConnect.MySql.Internal;
namespace SharpConnect.MySql
{
    namespace SyncPatt
    {
        public static partial class MySqlSyncPattExtension
        {
            public static void Open(this MySqlConnection conn)
            {
                conn.InternalOpen();
            }
            public static void Close(this MySqlConnection conn)
            {
                conn.InternalClose(null);
            }
            public static bool Ping(this MySqlConnection conn)
            {
                conn.InternalPing();
                return conn.LatestPingResult;
            }
            public static void ChangeDB(this MySqlConnection conn, string newDbName)
            {
                conn.InternalChangeDB(newDbName);
            }
            public static void ResetConnection(this MySqlConnection conn)
            {
                conn.InternalResetConnection();
            }
            public static void Dispose(this MySqlConnection conn)
            {
                //?
            }

            /// <summary>
            /// utils call sql stmt "set names ..."
            /// </summary>
            /// <param name="conn"></param>
            /// <param name="charsets"></param>
            public static void SetNames(this MySqlConnection conn, MySqlCharacterSetName charsets)
            {
                //utils
                string charsetNames = MySqlSetNamesUtils.GetCharacterSetName(charsets);
                if (charsetNames != null)
                {
                    MySqlCommand cmd = new MySqlCommand("set names " + charsetNames, conn);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
    namespace AsyncPatt
    {
        public static partial class MySqlAsyncPattExtension
        {

            public static void Open(this MySqlConnection conn, Action onComplete)
            {
                conn.InternalOpen(onComplete);
            }
            public static void Ping(this MySqlConnection conn, Action onComplete)
            {
                conn.InternalPing(onComplete);
            }
            public static void ChangeDB(this MySqlConnection conn, string newDbName, Action onComplete)
            {
                conn.InternalChangeDB(newDbName, onComplete);
            }
            public static void ResetConnection(this MySqlConnection conn, Action onComplete)
            {
                conn.InternalResetConnection(onComplete);
            }
            public static void Close(this MySqlConnection conn, Action onComplete)
            {
                conn.InternalClose(onComplete);
            }

            public static void Stop(this MySqlDataReader reader)
            {
                reader.StopReadingNextRow = true;
            }

        }
    }


    public enum MySqlCharacterSetName
    {
        Latin1 = 8,
        Latin2 = 9,
        Ascii = 11,
        Tis620 = 18,
        Utf8 = 33,
    }

    static class MySqlSetNamesUtils
    {

        public static string GetCharacterSetName(MySqlCharacterSetName charset)
        {
            //see more https://dev.mysql.com/doc/refman/8.0/en/charset-connection.html#charset-connection-system-variables
            //SELECT CHARACTER_SET_NAME, COLLATION_NAME, ID
            //     FROM INFORMATION_SCHEMA.COLLATIONS
            //     WHERE IS_DEFAULT = 'yes'
            //ORDER BY ID;
            //big5 big5_chinese_ci 1
            //dec8 dec8_swedish_ci 3
            //cp850 cp850_general_ci    4
            //hp8 hp8_english_ci  6
            //koi8r koi8r_general_ci    7
            //latin1 latin1_swedish_ci   8
            //latin2 latin2_general_ci   9
            //swe7 swe7_swedish_ci 10
            //ascii ascii_general_ci    11
            //ujis ujis_japanese_ci    12
            //sjis sjis_japanese_ci    13
            //hebrew hebrew_general_ci   16
            //tis620 tis620_thai_ci  18
            //euckr euckr_korean_ci 19
            //koi8u koi8u_general_ci    22
            //gb2312 gb2312_chinese_ci   24
            //greek greek_general_ci    25
            //cp1250 cp1250_general_ci   26
            //gbk gbk_chinese_ci  28
            //latin5 latin5_turkish_ci   30
            //armscii8 armscii8_general_ci 32
            //utf8 utf8_general_ci 33
            //ucs2 ucs2_general_ci 35
            //cp866 cp866_general_ci    36
            //keybcs2 keybcs2_general_ci  37
            //macce macce_general_ci    38
            //macroman macroman_general_ci 39
            //cp852 cp852_general_ci    40
            //latin7 latin7_general_ci   41
            //utf8mb4 utf8mb4_general_ci  45
            //cp1251 cp1251_general_ci   51
            //utf16 utf16_general_ci    54
            //utf16le utf16le_general_ci  56
            //cp1256 cp1256_general_ci   57
            //cp1257 cp1257_general_ci   59
            //utf32 utf32_general_ci    60
            //binary binary  63
            //geostd8 geostd8_general_ci  92
            //cp932 cp932_japanese_ci   95
            //eucjpms eucjpms_japanese_ci 97
            //gb18030
            switch (charset)
            {
                //TODO add more support here
                default: return null;
                case MySqlCharacterSetName.Latin1: return "latin1";
                case MySqlCharacterSetName.Latin2: return "latin2";
                case MySqlCharacterSetName.Ascii: return "ascii";
                case MySqlCharacterSetName.Tis620: return "tis620";
                case MySqlCharacterSetName.Utf8: return "utf8";
            }
        }
    }

    public class MySqlConnectionString
    {
        int _signature;
        public MySqlConnectionString(string h, string u, string p, string d)
            : this(h, u, p, d, 3306)
        {
        }
        public MySqlConnectionString(string h, string u, string p, string d, int portNumber)
        {
            Host = h;
            Username = u;
            Password = p;
            Database = d;
            PortNumber = portNumber;//default mysql port  
        }
        public string Host { get; private set; }
        public string Username { get; private set; }
        public string Password { get; private set; }
        public string Database { get; private set; }
        public int PortNumber { get; private set; }
        internal int ConnSignature
        {
            get
            {
                return (_signature != 0) ? _signature : _signature = string.Concat(Host, Username, Database, PortNumber).GetHashCode();
            }
        }
        public static MySqlConnectionString Parse(string connString)
        {
            string[] key_values = connString.Split(';');
            int j = key_values.Length;
            string server = null;
            string uid = null;
            string pwd = null;
            string database = null;
            int portNumber = 3306; //default port
            for (int i = 0; i < j; ++i)
            {
                if (key_values[i] == "")
                {
                    continue;
                }
                string[] key_value = key_values[i].Split('=');
                string key = key_value[0].Trim().ToLower();
                string value = key_value[1].Trim();

                switch (key)
                {
                    case "server":
                        {
                            server = value;
                        }
                        break;
                    case "uid":
                        {
                            uid = value;
                        }
                        break;
                    case "pwd":
                        {
                            pwd = value;
                        }
                        break;
                    case "database":
                        {
                            database = value;
                        }
                        break;
                    case "port":
                        {
                            int.TryParse(value, out portNumber);
                        }
                        break;
                    default:
                        throw new Exception("unknown key?");
                }
            }
            return new MySqlConnectionString(server, uid, pwd, database, portNumber);
        }
    }

    public enum ConnectionState
    {
        Unknown,
        Open,
        Closing,
        Closed,
    }
    public class MySqlConnection
    {
        MySqlConnectionString _connStr;
        /// <summary>
        /// internal MySql connection
        /// </summary>
        Connection _conn;
        public MySqlConnection(MySqlConnectionString connStr)
        {
            _connStr = connStr;
            LockWaitingMilliseconds = 1000 * 5; //TODO: review this default valut
        }
        public MySqlConnection(string host, string uid, string psw, string db)
            : this(new MySqlConnectionString(host, uid, psw, db))
        {
        }
        public MySqlConnection(string connStr)
            : this(MySqlConnectionString.Parse(connStr))
        {
        }
        public bool UseConnectionPool { get; set; }
        public bool FromConnectionPool { get; private set; }


        /// <summary>
        /// (approximate) maximum waiting time for some locking operation,set this before open connection
        /// </summary>
        public int LockWaitingMilliseconds { get; set; }

        public ConnectionState State
        {
            get
            {
                switch (_conn.State)
                {
                    case Internal.ConnectionState.Connected:
                        return ConnectionState.Open;
                    case Internal.ConnectionState.Disconnected:
                        return ConnectionState.Closed;
                    default:
                        throw new NotSupportedException();
                }
            }
        }



        /// <summary>
        /// eg. server closed 
        /// </summary>
        internal bool HasConnectionError => _conn.WorkingState == WorkingState.Error;

        internal void InternalOpen(Action onComplete = null)
        {
            this.FromConnectionPool = false;//reset
            //get connection from pool
            if (UseConnectionPool)
            {
                _conn = ConnectionPool.GetConnection(_connStr);
                if (_conn != null)
                {
                    _conn.LockWaitingMilliseconds = LockWaitingMilliseconds;
                    FromConnectionPool = true;
                    onComplete?.Invoke();
                }
                else
                {
                    //create new                     
                    _conn = new Connection(
                        new ConnectionConfig(
                            _connStr.Host,
                            _connStr.Username,
                            _connStr.Password,
                            _connStr.Database)
                        { port = _connStr.PortNumber });

                    _conn.LockWaitingMilliseconds = LockWaitingMilliseconds;
                    _conn.Connect(onComplete);
                }
            }
            else
            {
                //new connection
                _conn = new Connection(
                    new ConnectionConfig(
                        _connStr.Host,
                        _connStr.Username,
                        _connStr.Password,
                        _connStr.Database)
                    { port = _connStr.PortNumber });

                _conn.LockWaitingMilliseconds = LockWaitingMilliseconds;
                _conn.Connect(onComplete);
            }
        }

        internal bool LatestPingResult => _conn.LatestCallIsOk;

        internal void InternalPing(Action onComplete = null)
        {
            _conn.Ping(onComplete);
        }
#if DEBUG
        internal void dbugSimulateSocketErr()
        {
            _conn.dbugMakeSocketClose();
        }

#endif
        internal void InternalResetConnection(Action onComplete = null)
        {
            _conn.ResetConnection(onComplete);
        }
        internal void InternalChangeDB(string newDbName, Action onComplete = null)
        {
            _conn.ChangeDB(newDbName, onComplete);
        }
        internal void InternalClose(Action onComplete = null)
        {
            if (UseConnectionPool)
            {
                if (!_conn.GetLatestSocketCheckError())
                {
                    _conn.ForceReleaseBindingQuery();
                    ConnectionPool.ReleaseConnection(_connStr, _conn);                    
                }
                onComplete?.Invoke();
            }
            else
            {
                if (onComplete != null)
                {
                    //not block
                    _conn.Disconnect(() =>
                    {
                        _conn.Dispose();
                        _conn = null;
                        onComplete();
                    });
                }
                else
                {
                    _conn.Disconnect(); //block
                    _conn.Dispose();
                    _conn = null;
                }

            }
        }
        internal Connection Conn => _conn;
        public IStringConverter StringConv { get; set; }
#if DEBUG
        public bool dbugPleaseBreak
        {
            get { return _conn.dbugPleaseBreak; }
            set { _conn.dbugPleaseBreak = value; }
        }
        public void dbugMakeSocketError()
        {
            _conn.dbugMakeSocketClose();
        }
#endif
    }

    public static class MySqlConnectionExtension
    {

        public static void HardKill(this MySqlConnection tobeKillConn)
        {
            //TODO : review here ?
            //we use another connection to kill current th

            Connection internalConn = tobeKillConn.Conn;
            string realSql = "KILL " + internalConn._threadId;
            //sql = "FLUSH QUERY CACHE;";             
            Connection killConn = new Connection(internalConn._config);
            killConn.Connect();
            var q = new Query(killConn, realSql, null);
            q.Execute(false); //wait  
            q.Close();
            killConn.Disconnect();
        }

    }


}