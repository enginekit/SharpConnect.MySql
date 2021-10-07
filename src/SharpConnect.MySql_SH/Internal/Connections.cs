//LICENSE: MIT
//Copyright(c) 2012 Felix Geisendörfer(felix @debuggable.com) and contributors 
//MIT, 2015-present, brezza92, EngineKit and contributors

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in
//all copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//THE SOFTWARE.

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using SharpConnect.Internal;
namespace SharpConnect.MySql.Internal
{


    enum ConnectionState
    {
        Disconnected,
        Connected
    }
    enum WorkingState
    {
        Rest,
        Sending,
        Receiving,
        Error,
        Disconnected,
    }

    /// <summary>
    /// core connection session
    /// </summary>
    class Connection : IDisposable
    {

        /// <summary>
        /// mysql's max content length per packet
        /// </summary>
        internal const int MAX_PACKET_CONTENT_LENGTH = (1 << 24) - 1;
        public ConnectionConfig _config;
        WorkingState _workingState;
        //---------------------------------
        //core socket connection, send/recv io
        Socket _socket;
        SocketAsyncEventArgs _recvSendArgs;
        readonly RecvIO _recvIO;
        readonly SendIO _sendIO;
        readonly int _recvBufferSize;
        readonly int _sendBufferSize;
        Action<MySqlResult> _whenRecvData;
        Action _whenSendCompleted;
        //---------------------------------

        MySqlStreamWriter _writer;
        MySqlParserMx _mysqlParserMx;//know how to parse mysql data
        //---------------------------------
        //after open connection
        bool _isProtocol41;
        public uint _threadId;
        bool _isDisposed;

        EventHandler<SocketAsyncEventArgs> _recvSendArgsCompleted;


        public Connection(ConnectionConfig userConfig)
        {
            _config = userConfig;
            _recvBufferSize = userConfig.recvBufferSize;
            _sendBufferSize = userConfig.sendBufferSize;
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _writer = new MySqlStreamWriter(_config.GetEncoding());

            //------------------
            //we share recvSendArgs between recvIO and sendIO
            //similar to simple http 
            //it is simple, (NOT  duplex like web socket)             
            //------------------
            _recvSendArgs = new SocketAsyncEventArgs();
            _recvSendArgs.SetBuffer(new byte[_recvBufferSize + _sendBufferSize], 0, _recvBufferSize + _sendBufferSize);
            _recvIO = new RecvIO(_recvSendArgs, _recvSendArgs.Offset, _recvBufferSize, HandleReceive);
            _sendIO = new SendIO(_recvSendArgs, _recvSendArgs.Offset + _recvBufferSize, _sendBufferSize, HandleSend);
            //------------------

            //common(shared) event listener***
            _recvSendArgsCompleted = (object sender, SocketAsyncEventArgs e) =>
             {
                 if (_isDisposed)
                 {
                     //it should not occur
                     return;
                 }

                 switch (e.LastOperation)
                 {
                     case SocketAsyncOperation.Receive:
                         _recvIO.ProcessReceivedData();
                         break;
                     case SocketAsyncOperation.Send:
                         _sendIO.ProcessWaitingData();
                         break;
                     default:
                         throw new ArgumentException("The last operation completed on the socket was not a receive or send");
                 }
             };
            _recvSendArgs.Completed += _recvSendArgsCompleted;
            //------------------
            _recvSendArgs.AcceptSocket = _socket;
            _mysqlParserMx = new MySqlParserMx(_config);
        }
        /// <summary>
        /// (approximate) maximum waiting time for some locking operation,set this before open connection
        /// </summary>
        public int LockWaitingMilliseconds { get; set; }

        /// <summary>
        /// ensure that we have large buffer for request sending output
        /// </summary>
        /// <param name="reqSize"></param>
        /// <returns></returns>
        public bool EnsureSendIOBufferSize(int reqSize)
        {
            if (reqSize < _sendBufferSize)
            {
                return true;//ok
            }
            else
            {
                return false;
            }
        }
        public void Dispose()
        {
            _isDisposed = true;
            if (_recvSendArgs != null)
            {
                if (_recvSendArgsCompleted != null)
                {
                    //unsubscibe
                    _recvSendArgs.Completed -= _recvSendArgsCompleted;
                }

                _recvSendArgsCompleted = null;
                _recvSendArgs.Dispose();
                _recvSendArgs = null;
            }
            if (_socket != null)
            {
                _socket = null;
            }
        }
        /// <summary>
        /// get low-level _socket.Connected state
        /// </summary>
        public ConnectionState State => _socket.Connected ? ConnectionState.Connected : ConnectionState.Disconnected;

        public WorkingState WorkingState
        {
            get => _workingState;
            private set => _workingState = value;
        }
        internal MySqlParserMx MySqlParserMx => _mysqlParserMx;

        void UnBindSocket(bool keepAlive)
        {
            //TODO: review here ***
            //eg. server shutdown etc
            //throw new NotImplementedException();


        }

        readonly object _recvLock = new object();

        void HandleReceive(RecvEventCode recvEventCode)
        {
            switch (recvEventCode)
            {
                default: throw new NotSupportedException();
                case RecvEventCode.SocketError:
                    {
                        UnBindSocket(true);

                        _workingState = WorkingState.Error;
                        UnWait();//release current waiting when error

                    }
                    break;
                case RecvEventCode.NoMoreReceiveData:
                    {
                    }
                    break;
                case RecvEventCode.HasSomeData:
                    {
                        //process some data
                        //there some data to process  
                        //parse the data    
#if DEBUG
                        if (dbugPleaseBreak)
                        {
                        }
#endif

                        lock (_recvLock)
                        {
                            bool needMoreData = false;
                            needMoreData = _mysqlParserMx.ParseData(_recvIO);
                            //please note that: result packet may not ready in first round
                            //but parser mx may 'release' some part of the result (eg. large table)
                            MySqlResult result = _mysqlParserMx.ParseResult; //'release' result 
                                                                             //---------------------------------------------------------------
                            if (result != null)
                            {
                                //if we has some 'release' result from parser mx
                                if (needMoreData)
                                {
                                    //this is 'partial result'
                                    if (_whenRecvData == null)
                                    {
                                        //?
                                    }
                                    //-------------------------
                                    //partial release data here
                                    //before recv next
                                    //because we want to 'sync'
                                    //the series of result
                                    _whenRecvData(result);
                                    //-------------------------
                                }
                                else
                                {
                                    //when recv complete***
                                    Action<MySqlResult> tmpWhenRecvData = _whenRecvData;
                                    //delete recv handle **before** invoke it, and
                                    //reset state to 'rest' state
                                    _whenRecvData = null;
                                    _workingState = WorkingState.Rest;
                                    tmpWhenRecvData(result);
                                }
                            }

                            //--------------------------
                            if (needMoreData)
                            {
                                //so if it need more data then start receive next
                                _recvIO.StartReceive();//*** 
                            }
                            else
                            {

                            }
                            //--------------------------
                        }

                    }
                    break;
            }
        }
        void HandleSend(SendIOEventCode sendEventCode)
        {
            //throw new NotImplementedException();
            switch (sendEventCode)
            {
                case SendIOEventCode.SocketError:
                    {
                        UnBindSocket(true);
                        _workingState = WorkingState.Error;
                        UnWait();//release current waiting when error
                    }
                    break;
                case SendIOEventCode.SendComplete:
                    {
                        //save when send complete here
                        Action tmpWhenSendComplete = _whenSendCompleted;

#if DEBUG
                        if (_sendIO.dbugHasSomeEnqueueData())
                        {

                        }
                        else
                        {

                        }
#endif
                        _whenSendCompleted = null; //clear handler
                        _workingState = WorkingState.Rest;
                        tmpWhenSendComplete?.Invoke();

                    }
                    break;
            }
        }
        //----------------------------------------------------------------


        //TODO: review here
        int _globalWaiting = 0;
        readonly object _connLocker = new object();

        
        public void InitWait()
        {
            if (Interlocked.CompareExchange(ref _globalWaiting, 1, 0) == 1)
            {
                throw new Exception("we are waiting for something...");
            }
        }


        const int EACH_ROUND = 250;//250 ms


        internal bool WaitingTerminated { get; private set; }

        internal bool Wait()
        {
            //ref: http://www.albahari.com/threading/part4.aspx#_Signaling_with_Wait_and_Pulse
            //--------------------------------
            WaitingTerminated = false;//reset
            lock (_connLocker)
            {
                int tryCount = 0;
                int lim_count = LockWaitingMilliseconds / EACH_ROUND;
                while (_globalWaiting == 1)
                {
                    if (tryCount > (lim_count))
                    {
                        _globalWaiting = 0;
                        WaitingTerminated = true;
                        UnWait();
                        return false;
                    }
                    Monitor.Wait(_connLocker, EACH_ROUND);
                    tryCount++;
                }
            }
            return true;
        }

        internal void UnWait()
        {

            //ref: http://www.albahari.com/threading/part4.aspx#_Signaling_with_Wait_and_Pulse
            lock (_connLocker)                 // Let's now wake up the thread
            {                             
                Interlocked.Exchange(ref _globalWaiting, 0);//set to false
                Monitor.Pulse(_connLocker);
            }
        }



        /// <summary>
        /// open connection, +/- blocking
        /// </summary>
        /// <param name="nextAction"></param>
        public void Connect(Action nextAction = null)
        {
            if (State == ConnectionState.Connected)
            {
                throw new NotSupportedException("already connected");
            }
            _mysqlParserMx.UseConnectionParser();
            _workingState = WorkingState.Rest;
            //--------------
            var endpoint = new IPEndPoint(IPAddress.Parse(_config.host), _config.port);
            _socket.Connect(endpoint);
            _workingState = WorkingState.Rest;
            //--------------
            //**start listen after connect
            InitWait();
            StartReceive(mysql_result =>
            {
                //when complete1
                //create handshake packet and send back
                var handshakeResult = mysql_result as MySqlHandshakeResult;
                if (handshakeResult == null)
                {
                    //error
                    throw new Exception("err1");
                }
                HandshakePacket handshake_packet = handshakeResult.packet;
                _threadId = handshake_packet.threadId;

                // https://dev.mysql.com/doc/internals/en/sha256.html

                byte[] token = string.IsNullOrEmpty(_config.password) ?
                              /*1*/  new byte[0] :   //Empty passwords are not hashed, but sent as empty string. 

                              /* or 2*/  MakeToken(_config.password, GetScrollbleBuffer(
                                             handshake_packet.scrambleBuff1,
                                             handshake_packet.scrambleBuff2));

                _writer.IncrementPacketNumber();
                //----------------------------
                //send authen packet to the server
                var authPacket = new ClientAuthenticationPacket(new PacketHeader());
                authPacket.SetValues(_config.user, token, _config.database, _isProtocol41 = handshake_packet.protocol41);
                authPacket.WritePacket(_writer);
                byte[] sendBuff = _writer.ToArray();
                _writer.Reset();
                //------------------------------------
                //switch to result packet parser  
                _mysqlParserMx.SetProtocol41(_isProtocol41);
                _mysqlParserMx.UseResultParser();
                //------------------------------------
                _latestCallIsOk = false;
                StartSend(sendBuff, 0, sendBuff.Length, () =>
                {
                    StartReceive(mysql_result2 =>
                    {
                        if (mysql_result2 is MySqlOkResult)
                        {
                            _latestCallIsOk = true;
                            _workingState = WorkingState.Rest;
                        }
                        else
                        {
                            //TODO: review here
                            //error  
                            _workingState = WorkingState.Error;
                        }
                        //set max allow of the server ***
                        //todo set max allow packet***
                        UnWait();

                        nextAction?.Invoke();

                    });
                });
            });
            if (nextAction == null)
            {
                //block ....
                if (!Wait())
                {
                    //TODO: handle wait timeout

                }
            }
            else
            {
                UnWait();
            }
        }


        bool _latestCallIsOk;
        bool _latestSocketCheckError;
        internal bool LatestCallIsOk => _latestCallIsOk;

        /// <summary>
        /// ping server, +/- blocking
        /// </summary>
        /// <param name="nextAction"></param>
        public void Ping(Action nextAction = null)
        {
            //ping server
            if (State == ConnectionState.Disconnected)
            {
                _latestSocketCheckError = true;//socket error
                _latestCallIsOk = false;
                return;
                //throw new NotSupportedException("open connection first");
            }


            _writer.Reset();

            ComPingPacket pingPacket = new ComPingPacket(new PacketHeader());
            pingPacket.WritePacket(_writer);
            byte[] data = _writer.ToArray();
            InitWait();

            _latestCallIsOk = false;

            StartSend(data, 0, data.Length, () =>
            {
                StartReceive(mysql_result2 =>
                {
                    if (mysql_result2 is MySqlOkResult)
                    {
                        _workingState = WorkingState.Rest;
                        _latestCallIsOk = true;
                    }
                    else
                    {
                        //TODO: review here
                        //error  
                        _workingState = WorkingState.Error;
                    }
                    //set max allow of the server ***
                    //todo set max allow packet***
                    UnWait();
                    nextAction?.Invoke();
                });

            });
            if (nextAction == null)
            {
                if (!Wait()) //block
                {
                    //TODO: handle wait-timeout
                }
            }
            else
            {
                UnWait();
            }
        }

        /// <summary>
        /// change default db
        /// </summary>
        /// <param name="newDbName"></param>
        /// <param name="nextAction"></param>
        public void ChangeDB(string newDbName, Action nextAction = null)
        {
            if (State == ConnectionState.Disconnected)
            {
                _latestCallIsOk = false;
                return;
                //throw new NotSupportedException("open connection first");
            }

            _writer.Reset();
            ComInitDB initDb = new ComInitDB(new PacketHeader(), newDbName);
            initDb.WritePacket(_writer);
            byte[] data = _writer.ToArray();
            InitWait();

            _latestCallIsOk = false;
            StartSend(data, 0, data.Length, () =>
            {
                StartReceive(mysql_result2 =>
                {
                    if (mysql_result2 is MySqlOkResult)
                    {
                        _latestCallIsOk = true;
                        _workingState = WorkingState.Rest;
                    }
                    else
                    {
                        //TODO: review here
                        //error  
                        _workingState = WorkingState.Error;
                    }
                    //set max allow of the server ***
                    //todo set max allow packet***
                    UnWait();

                    nextAction?.Invoke();
                });

            });
            if (nextAction == null)
            {
                Wait(); //block
            }
            else
            {
                UnWait();
            }
        }
        /// <summary>
        /// reset connection
        /// </summary>
        /// <param name="nextAction"></param>
        public void ResetConnection(Action nextAction = null)
        {
            //https://dev.mysql.com/doc/internals/en/com-reset-connection.html
            //COM_RESET_CONNECTION:

            //Resets the session state; more lightweight than COM_CHANGE_USER because it does not close and reopen the connection, and does not re-authenticate

            //Payload

            //1   [1f] COM_RESET_CONNECTION

            //Returns
            //  a ERR_Packet

            //  a OK_Packet

            _writer.Reset();
            ComResetConnectionPacket pingPacket = new ComResetConnectionPacket(new PacketHeader());
            pingPacket.WritePacket(_writer);
            byte[] data = _writer.ToArray();
            InitWait();

            _latestCallIsOk = false;
            StartSend(data, 0, data.Length, () =>
            {
                StartReceive(mysql_result2 =>
                {
                    if (mysql_result2 is MySqlOkResult)
                    {
                        _latestCallIsOk = true;
                        _workingState = WorkingState.Rest;
                    }
                    else
                    {
                        //TODO: review here
                        //error  
                        _workingState = WorkingState.Error;
                    }
                    //set max allow of the server ***
                    //todo set max allow packet***
                    UnWait();

                    nextAction?.Invoke();
                });
            });
            //------------------------
            if (nextAction == null)
            {
                if (!Wait()) //block
                {
                    //TODO: handle wait timeout
                }
            }
            else
            {
                UnWait();
            }

        }

        internal bool GetLatestSocketCheckError() => _latestSocketCheckError;

        /// <summary>
        /// close conncetion, +/- blocking
        /// </summary>
        /// <param name="nextAction"></param>
        public void Disconnect(Action nextAction = null)
        {
            if (WaitingTerminated)
            {
                return;
            }

            if (!_latestSocketCheckError)
            {
                //found error on the connection soket
                _writer.Reset();
                ComQuitPacket quitPacket = new ComQuitPacket(new PacketHeader());
                quitPacket.WritePacket(_writer);
                byte[] data = _writer.ToArray();
                //-------------------------------------
                InitWait();
                StartSend(data, 0, data.Length, () =>
                {
                    _socket.Shutdown(SocketShutdown.Both);
                    _workingState = WorkingState.Disconnected;
                    UnWait();
                    nextAction?.Invoke();
                });
            }



            if (nextAction == null)
            {
                if (!Wait())//block
                {
                    //TODO: handle wait-timeout
                }
            }
            else
            {
                UnWait();
            }
        }
        //---------------------------------------------------------------
        public bool IsStoredInConnPool { get; set; }
        public Query BindingQuery { get; set; }
        public void ForceReleaseBindingQuery(Action nextAction = null)
        {
            //force release binding query
            if (BindingQuery != null)
            {
                BindingQuery.Close(nextAction);
                this.BindingQuery = null;
            }
        }

        internal MySqlStreamWriter PacketWriter => _writer;

        internal bool IsProtocol41 => _isProtocol41;

        public void StartSend(byte[] sendBuffer, int start, int len, Action whenSendCompleted)
        {
            //must be in opened state
            if (_workingState != WorkingState.Rest)
            {
                throw new Exception("sending error: state is not= opened");
            }
            //--------------------------------------------------------------
#if DEBUG
            if (_whenSendCompleted != null)
            {
                //must be null 
                throw new Exception("sending something?...");
            }
#endif
            _whenSendCompleted = whenSendCompleted;
            _workingState = WorkingState.Sending;
            _sendIO.EnqueueOutputData(sendBuffer, len);
            _sendIO.StartSendAsync();
        }
        public void EnqueueOutputData(byte[] sendBuffer)
        {
            //must be in opened state
            if (_workingState != WorkingState.Rest)
            {
                throw new Exception("sending error: state is not= opened");
            }
            _sendIO.EnqueueOutputData(sendBuffer, sendBuffer.Length);
        }
        public void StartSend(Action whenSendCompleted)
        {
            //must be in opened state
            if (_workingState != WorkingState.Rest)
            {
                throw new Exception("sending error: state is not= opened");
            }
            //--------------------------------------------------------------
#if DEBUG
            if (_whenSendCompleted != null)
            {
                //must be null 
                throw new Exception("sending something?...");
            }
#endif
            _whenSendCompleted = whenSendCompleted;
            _sendIO.StartSendAsync();
        }
        public void StartReceive(Action<MySqlResult> whenCompleteAction)
        {

            //must be in opened state
            if (_workingState != WorkingState.Rest)
            {
                throw new Exception("sending error: state is not= opened");
            }
            //--------------------------------------------------------------
#if DEBUG
            if (_whenRecvData != null)
            {
                //must be null 
                throw new Exception("receving something?...");
            }
#endif
            _whenRecvData = whenCompleteAction;
            _workingState = WorkingState.Receiving;
            _recvIO.StartReceive();
        }

        static byte[] GetScrollbleBuffer(byte[] part1, byte[] part2)
        {
            return ConcatBuffer(part1, part2);
        }

        static byte[] MakeToken(string password, byte[] scramble)
        {
            // password must be in binary format, not utf8
            //var stage1 = sha1((new Buffer(password, "utf8")).toString("binary"));
            //var stage2 = sha1(stage1);
            //var stage3 = sha1(scramble.toString('binary') + stage2);
            //return xor(stage3, stage1);
            var buff1 = Encoding.UTF8.GetBytes(password.ToCharArray());
            var sha = System.Security.Cryptography.SHA1.Create();
            // This is one implementation of the abstract class SHA1.
            //scramble = new byte[] { 52, 78, 110, 96, 117, 75, 85, 75, 87, 83, 121, 44, 106, 82, 62, 123, 113, 73, 84, 77 };
            byte[] stage1 = sha.ComputeHash(buff1);
            byte[] stage2 = sha.ComputeHash(stage1);
            //merge scramble and stage2 again
            byte[] combineFor3 = ConcatBuffer(scramble, stage2);
            byte[] stage3 = sha.ComputeHash(combineFor3);
            return xor(stage3, stage1);
        }

        static byte[] ConcatBuffer(byte[] a, byte[] b)
        {
            byte[] combine = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, combine, 0, a.Length);
            Buffer.BlockCopy(b, 0, combine, a.Length, b.Length);
            return combine;
        }

        static byte[] xor(byte[] a, byte[] b)
        {
            int j = a.Length;
            var result = new byte[j];
            for (int i = 0; i < j; ++i)
            {
                result[i] = (byte)(a[i] ^ b[i]);
            }
            return result;
        }
#if DEBUG
        /// <summary>
        /// simulate socket close, for dbug Mode only
        /// </summary>
        internal void dbugMakeSocketClose()
        {
            _socket.Close();
        }
        public bool dbugPleaseBreak { get; set; }
#endif
    }

    class ConnectionConfig
    {
        public readonly string host;
        public int port;
        public string localAddress;//unknowed type
        public string socketPath;//unknowed type
        public string user;
        public string password;
        public string database;
        public int connectionTimeout;
        public bool insecureAuth;
        public bool supportBigNumbers;
        public bool bigNumberStrings;
        public bool dateStrings;
        public bool debug;
        public bool trace;
        public bool stringifyObjects;
        public string timezone;
        public string flags;
        public string queryFormat;

        public string pool;//unknowed type
        public string ssl;//string or bool

        public bool multipleStatements;
        public bool typeCast;
        public long maxPacketSize;
        public int charsetNumber;
        public int defaultFlags;
        public int clientFlags;

        //
        public int recvBufferSize = 265000; //TODO: review here
        public int sendBufferSize = (1 << 24) + 64;//TODO: review here

        public ConnectionConfig()
        {
            //set default
            //if (typeof options === 'string') {
            //  options = ConnectionConfig.parseUrl(options);
            //}
            host = "127.0.0.1";//this.host = options.host || 'localhost';
            port = 3306;//this.port = options.port || 3306;
            //this.localAddress       = options.localAddress;
            //this.socketPath         = options.socketPath;
            //this.user               = options.user || undefined;
            //this.password           = options.password || undefined;
            //this.database           = options.database;
            database = "";
            connectionTimeout = 10 * 1000;
            //this.connectTimeout     = (options.connectTimeout === undefined)
            //  ? (10 * 1000)
            //  : options.connectTimeout;
            insecureAuth = false;//this.insecureAuth = options.insecureAuth || false;
            supportBigNumbers = false;//this.supportBigNumbers = options.supportBigNumbers || false;
            bigNumberStrings = false;//this.bigNumberStrings = options.bigNumberStrings || false;
            dateStrings = false;//this.dateStrings = options.dateStrings || false;
            debug = false;//this.debug = options.debug || true;
            trace = false;//this.trace = options.trace !== false;
            stringifyObjects = false;//this.stringifyObjects = options.stringifyObjects || false;
            timezone = "local";//this.timezone = options.timezone || 'local';
            flags = "";//this.flags = options.flags || '';
            //this.queryFormat        = options.queryFormat;
            //this.pool               = options.pool || undefined;

            //this.ssl                = (typeof options.ssl === 'string')
            //  ? ConnectionConfig.getSSLProfile(options.ssl)
            //  : (options.ssl || false);
            multipleStatements = false;//this.multipleStatements = options.multipleStatements || false; 

            //this.typeCast = (options.typeCast === undefined)
            //  ? true
            //  : options.typeCast;

            //if (this.timezone[0] == " ") {
            //  // "+" is a url encoded char for space so it
            //  // gets translated to space when giving a
            //  // connection string..
            //  this.timezone = "+" + this.timezone.substr(1);
            //}

            //if (this.ssl) {
            //  // Default rejectUnauthorized to true
            //  this.ssl.rejectUnauthorized = this.ssl.rejectUnauthorized !== false;
            //}

            maxPacketSize = 0;//this.maxPacketSize = 0;
            charsetNumber = (int)CharSets.ASCII; //default?

            //this.charsetNumber = (options.charset)
            //  ? ConnectionConfig.getCharsetNumber(options.charset)
            //  : options.charsetNumber||Charsets.UTF8_GENERAL_CI;

            //// Set the client flags
            //var defaultFlags = ConnectionConfig.getDefaultFlags(options);
            //this.clientFlags = ConnectionConfig.mergeFlags(defaultFlags, options.flags)
        }


        public ConnectionConfig(string host, string username, string password, string database = "")
             : this()
        {

            if (host == "localhost")
            {
                host = "127.0.0.1";
            }
            this.user = username;
            this.password = password;
            this.host = host;
            this.database = database;
        }


        public System.Text.Encoding GetEncoding()
        {
            //
            switch ((CharSets)charsetNumber)
            {
                default: throw new NotImplementedException();
                case CharSets.UTF8_GENERAL_CI: return Encoding.UTF8;
                case CharSets.ASCII: return Encoding.ASCII;
            }
        }
    }
}