﻿//LICENSE: MIT
//Copyright(c) 2012 Felix Geisendörfer(felix @debuggable.com) and contributors 
//MIT, 2015-2016, brezza92, EngineKit and contributors

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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using SharpConnect.Internal;
namespace SharpConnect.MySql.Internal
{
    static class dbugConsole
    {
        [System.Diagnostics.Conditional("DEBUG")]
        public static void WriteLine(string str)
        {
            //Console.WriteLine(str);
        }
    }

    enum ConnectionState
    {
        Disconnected,
        Connected
    }

    /// <summary>
    /// core connection session
    /// </summary>
    class Connection
    {
        public ConnectionConfig config;

        //---------------------------------
        //core socket connection, send/recv io
        Socket socket;
        readonly SocketAsyncEventArgs recvSendArgs;
        readonly RecvIO recvIO;
        readonly SendIO sendIO;
        readonly int recvBufferSize;
        readonly int sendBufferSize;
        Action<MySqlResult> whenRecvComplete;
        Action whenSendCompleted;
        //---------------------------------
        //packet parser mx (read data),         
        MySqlParserMx _mysqlParserMx;
        PacketWriter _writer;

        //---------------------------------
        //after open connection
        bool isProtocol41;
        public uint threadId;

        //TODO: review how to clear remaining buffer again
        byte[] _tmpForClearRecvBuffer; //for clear buffer      


        public Connection(ConnectionConfig userConfig)
        {
            config = userConfig;
            recvBufferSize = userConfig.recvBufferSize;
            sendBufferSize = userConfig.sendBufferSize;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //
            switch ((CharSets)config.charsetNumber)
            {
                case CharSets.UTF8_GENERAL_CI:
                    //_parser = new PacketParser(Encoding.UTF8);
                    _writer = new PacketWriter(Encoding.UTF8);
                    break;
                case CharSets.ASCII:
                    //_parser = new PacketParser(Encoding.ASCII);
                    _writer = new PacketWriter(Encoding.ASCII);
                    break;
                default:
                    throw new NotImplementedException();
            }

            //------------------
            recvSendArgs = new SocketAsyncEventArgs();
            recvSendArgs.SetBuffer(new byte[recvBufferSize + sendBufferSize], 0, recvBufferSize + sendBufferSize);
            recvIO = new RecvIO(recvSendArgs, recvSendArgs.Offset, recvBufferSize, HandleReceive);
            sendIO = new SendIO(recvSendArgs, recvSendArgs.Offset + recvBufferSize, sendBufferSize, HandleSend);
            //------------------
            _mysqlParserMx = new MySqlParserMx(_writer);
            //common(shared) event listener***
            recvSendArgs.Completed += (object sender, SocketAsyncEventArgs e) =>
            {
                switch (e.LastOperation)
                {
                    case SocketAsyncOperation.Receive:
                        recvIO.ProcessReceivedData();
                        break;
                    case SocketAsyncOperation.Send:
                        sendIO.ProcessWaitingData();
                        break;
                    default:
                        throw new ArgumentException("The last operation completed on the socket was not a receive or send");
                }
            };
            //------------------
            recvSendArgs.AcceptSocket = socket;
        }
        public ConnectionState State
        {
            get
            {
                return socket.Connected ? ConnectionState.Connected : ConnectionState.Disconnected;
            }
        }
        /// <summary>
        /// mysql parser mx
        /// </summary>
        internal MySqlParserMx ParserMx { get { return _mysqlParserMx; } }


        void UnBindSocket(bool keepAlive)
        {
            throw new NotImplementedException();
        }
        void HandleReceive(RecvEventCode recvEventCode)
        {
            switch (recvEventCode)
            {
                default: throw new NotSupportedException();
                case RecvEventCode.SocketError:
                    {
                        UnBindSocket(true);
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

                        _mysqlParserMx.ParseData(recvIO);
                        //please note that: result packet may not ready in first round
                        if (_mysqlParserMx.ResultPacket != null)
                        {
                            if (whenRecvComplete != null)
                            {
                                whenRecvComplete(_mysqlParserMx.ResultPacket);
                            }
                        }
                        else
                        {
                            //no result packet in this round
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
                    }
                    break;
                case SendIOEventCode.SendComplete:
                    {
                        if (whenSendCompleted != null)
                        {
                            whenSendCompleted();
                        }
                    }
                    break;
            }
        }


        internal void StartReceive(Action<MySqlResult> whenCompleteAction)
        {
            this.whenRecvComplete = whenCompleteAction;
            recvIO.StartReceive();
        }

        public void Connect(Action nextAction = null)
        {
            if (State == ConnectionState.Connected)
            {
                throw new NotSupportedException("already connected");
            }

            var endpoint = new IPEndPoint(IPAddress.Parse(config.host), config.port);
            socket.Connect(endpoint); //start listen after connect***
            //1. 
            _mysqlParserMx.CurrentPacketParser = new MySqlConnectionPacketParser();
            bool connectionIsCompleted = false;
            StartReceive(mysql_result =>
            {
                //when complete1
                //create handshake packet and send back
                var handShakeResult = mysql_result as MySqlHandshakeResult;
                if (handShakeResult == null)
                {
                    //error
                    throw new Exception("err1");
                }
                var handshake_packet = handShakeResult.packet;
                this.threadId = handshake_packet.threadId;
                byte[] token = MakeToken(config.password,
                   GetScrollbleBuffer(handshake_packet.scrambleBuff1, handshake_packet.scrambleBuff2));
                _writer.IncrementPacketNumber();
                //----------------------------
                //send authen packet to the server
                var authPacket = new ClientAuthenticationPacket();
                authPacket.SetValues(config.user, token, config.database, isProtocol41 = handshake_packet.protocol41);
                authPacket.WritePacket(_writer);
                byte[] sendBuff = _writer.ToArray();

                StartSendData(sendBuff, 0, sendBuff.Length, () =>
                {
                    //------------------------------------
                    //switch to result packet parser 
                    _mysqlParserMx.CurrentPacketParser = new ResultPacketParser(this.config, isProtocol41);
                    //------------------------------------

                    StartReceive(mysql_result2 =>
                    {
                        var ok = mysql_result2 as MySqlOk;
                        if (ok != null)
                        {
                            ConnectedSuccess = true;
                        }
                        else
                        {
                            //TODO: review here
                            //error 
                            ConnectedSuccess = false;
                        }
                        //ok
                        _writer.Reset();
                        //set max allow of the server ***
                        //todo set max allow packet***
                        connectionIsCompleted = true;
                        if (nextAction != null)
                        {
                            nextAction();
                        }
                    });

                });
            });
            if (nextAction == null)
            {
                //exec as sync
                //so wait until complete
                //-------------------------------
                while (!connectionIsCompleted) ;  //tight loop,*** wait, or use thread sleep
                //-------------------------------
            }
        }

        public bool ConnectedSuccess
        {
            get;
            private set;
        }


        //blocking***
        public void Disconnect()
        {
            _writer.Reset();
            ComQuitPacket quitPacket = new ComQuitPacket();
            quitPacket.WritePacket(_writer);
            int send = socket.Send(_writer.ToArray());
            socket.Disconnect(true);
        }
        public bool IsStoredInConnPool { get; set; }
        public bool IsInUsed { get; set; }

        internal PacketWriter PacketWriter
        {
            get { return _writer; }
        }
        internal bool IsProtocol41 { get { return this.isProtocol41; } }

        internal void ClearRemainingInputBuffer()
        {
            //TODO: review here again

            int lastReceive = 0;
            long allReceive = 0;
            int i = 0;
            if (socket.Available > 0)
            {
                if (_tmpForClearRecvBuffer == null)
                {
                    _tmpForClearRecvBuffer = new byte[300000];//in test case socket recieve lower than 300,000 bytes
                }

                while (socket.Available > 0)
                {
                    lastReceive = socket.Receive(_tmpForClearRecvBuffer);
                    allReceive += lastReceive;
                    i++;
                    //TODO: review here again
                    dbugConsole.WriteLine("i : " + i + ", lastReceive : " + lastReceive);
                    Thread.Sleep(100);
                }
                dbugConsole.WriteLine("All Receive bytes : " + allReceive);
            }
        }
        public void StartSendData(byte[] sendBuffer, int start, int len, Action whenSendComplete)
        {
            this.whenSendCompleted = whenSendComplete;
            sendIO.EnqueueOutputData(sendBuffer, len);
            sendIO.StartSendAsync();
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
            var sha = new System.Security.Cryptography.SHA1Managed();
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
    }

    class ConnectionConfig
    {
        public string host;
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

        public int recvBufferSize = 265000; //TODO: review here
        public int sendBufferSize = 51200;

        public ConnectionConfig()
        {
            SetDefault();
        }

        public ConnectionConfig(string username, string password)
        {
            SetDefault();
            this.user = username;
            this.password = password;
        }
        public ConnectionConfig(string host, string username, string password, string database)
        {
            SetDefault();
            this.user = username;
            this.password = password;
            this.host = host;
            this.database = database;
        }
        void SetDefault()
        {
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
            typeCast = true;
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
            charsetNumber = (int)CharSets.UTF8_GENERAL_CI;
            //this.charsetNumber = (options.charset)
            //  ? ConnectionConfig.getCharsetNumber(options.charset)
            //  : options.charsetNumber||Charsets.UTF8_GENERAL_CI;

            //// Set the client flags
            //var defaultFlags = ConnectionConfig.getDefaultFlags(options);
            //this.clientFlags = ConnectionConfig.mergeFlags(defaultFlags, options.flags)
        }

        public void SetConfig(string host, int port, string username, string password, string database)
        {
            this.host = host;
            this.port = port;
            this.user = username;
            this.password = password;
            this.database = database;
        }
    }
}