//LICENSE: MIT
//Copyright(c) 2012 Felix Geisendörfer(felix @debuggable.com) and contributors 
//Copyright(c) 2013 Andrey Sidorov(sidorares @yandex.ru) and contributors
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
using System.Collections.Generic;
namespace SharpConnect.MySql.Internal
{

    enum QueryExecState
    {
        Rest,
        Prepare,
        Exec,
        Closing,
        Closed,
        Terminated,
    }
    enum QueryUseMode
    {
        ExecNonQuery,//default
        ExecReader,
        Prepare,
    }
    class Query
    {
#if DEBUG
        public bool typeCast;
#endif
        public bool _nestTables;
        CommandParams _cmdParams;
        Connection _conn;
        bool _prepareStatementMode;
        QueryUseMode _queryUsedMode;

        MySqlStreamWriter _writer;
        SqlStringTemplate _sqlStrTemplate;
        PreparedContext _prepareContext;
        MySqlParserMx _sqlParserMx;
        QueryExecState _execState = QueryExecState.Rest;
        Action<MySqlTableResult> _tableResultListener;
        Action<MySqlErrorResult> _errorListener;
        //------------
        //we create query for each command and not reuse it
        //------------

        internal Query(Connection conn, string sql, CommandParams cmdParams)
            : this(conn, new SqlStringTemplate(sql), cmdParams)
        {
        }
        internal Query(Connection conn, SqlStringTemplate sql, CommandParams cmdParams)
        {
            //***a query uses conn resource such as parser,writer
            //so 1 query=> 1 connection
            if (sql == null)
            {
                throw new Exception("Sql command can not null.");
            }

            Query bindingQuery = conn.BindingQuery;
            if (bindingQuery != null)
            {
                //check if binding query can be close 
                if (!bindingQuery.LateClose())
                {
                    //can't use this conn
                    throw new Exception("connection is in used");
                }
            }
            //--------------------------------------------------------------
            conn.BindingQuery = this;
            //--------------------------------------------------------------
            _conn = conn;
            _cmdParams = cmdParams;
            //--------------------------------------------------------------

            _nestTables = false;
            _sqlParserMx = conn.MySqlParserMx;
            _writer = conn.PacketWriter;
            //_receiveBuffer = null;
            _sqlStrTemplate = sql;
        }
        bool LateClose()
        {
            if (_execState == QueryExecState.Closed) { return true; }

            if (_queryUsedMode == QueryUseMode.ExecNonQuery || _queryUsedMode == QueryUseMode.Prepare)
            {
                this.Close();
                //_conn.BindingQuery = null;
                //_conn = null;
                return true;
            }
            return false;
        }

        public bool WaitingTerminated => (_conn == null) ? false : _conn.WaitingTerminated;
        public int LockWaitingMilliseconds => _conn.LockWaitingMilliseconds;
        public ErrPacket Error { get; private set; }
        public OkPacket OkPacket { get; private set; }
        public void SetResultListener(Action<MySqlTableResult> tableResultListener)
        {
            _tableResultListener = tableResultListener;
        }
        public void SetErrorListener(Action<MySqlErrorResult> errorListener)
        {
            _errorListener = errorListener;
        }

        /// <summary>
        /// +/- blocking
        /// </summary>
        /// <param name="nextAction"></param>
        public void Prepare(Action nextAction = null)
        {
            _execState = QueryExecState.Prepare;
            _queryUsedMode = QueryUseMode.Prepare;

            if (_cmdParams == null)
            {
                throw new Exception("Sql cmdParams can not null.");
            }
            //-------------------
            //blocking method***
            //wait until execute finish 
            //-------------------
            //prepare sql query             
            _sqlParserMx.UsePrepareResponseParser();
            _prepareContext = null;
            //-------------------------------------------------------------
            _writer.Reset();
            ComPrepareStatementPacket.Write(
                _writer,
                _sqlStrTemplate.BindValues(_cmdParams, true));
            //-------------------------------------------------------------
            if (nextAction != null)
            {
                //not block here
                SendAndRecv_A(_writer.ToArray(), nextAction);
            }
            else
            {
                //blocking
                _conn.InitWait();
                //send and recive, after revice the unwait
                SendAndRecv_A(_writer.ToArray(), _conn.UnWait);
                //now wait 
                if (!_conn.Wait())
                {
                    _execState = QueryExecState.Terminated;
                }
            }
        }


        /// <summary>
        ///+/- blocking 
        /// </summary>
        /// <param name="nextAction"></param>
        public void Execute(bool execReader, Action nextAction = null)
        {
            _execState = QueryExecState.Exec;
            _queryUsedMode = execReader ? QueryUseMode.ExecReader : QueryUseMode.ExecNonQuery;
            //-------------------
            //blocking method***
            //wait until execute finish 
            //-------------------  
            if (nextAction != null)
            {
                //not block here
                if (_prepareContext != null)
                {
                    ExecutePrepareQuery_A(nextAction);
                }
                else
                {
                    if (!execReader)
                    {
                        //execute non query, 
                        ExecuteNonPrepare_A(() =>
                        {
                            Close(nextAction);
                        });
                    }
                    else
                    {
                        ExecuteNonPrepare_A(nextAction);
                    }

                }
            }
            else
            {
                //block
                _conn.InitWait();
                if (_prepareContext != null)
                {
                    ExecutePrepareQuery_A(_conn.UnWait);
                }
                else
                {
                    ExecuteNonPrepare_A(_conn.UnWait);
                }
                if (!_conn.Wait())
                {
                    //TODO: handle wait-timeout
                    _execState = QueryExecState.Terminated;
                }
                else
                {
                    if (!execReader && _prepareContext == null)
                    {
                        //execute non query
                        Close(null);
                    }
                }
            }
        }

        /// <summary>
        ///  +/- blocking
        /// </summary>
        /// <param name="nextAction"></param>
        public void Close(Action nextAction = null)
        {

            //-------------------------------------------------
            switch (_execState)
            {
                //can close twice without error
                case QueryExecState.Terminated:
                    return;//***
                case QueryExecState.Closed:
                    nextAction?.Invoke();
                    return; //***
                case QueryExecState.Closing:
#if DEBUG
                    throw new Exception("conn is closing ...");
#else
                    return;
#endif
            }
            //first ***
            _execState = QueryExecState.Closing;
            //------------------------------------------------- 
            if (_conn != null && _conn.WaitingTerminated)
            {
                //something err
                if (nextAction != null)
                {

                }
                return;
            }

            if (nextAction == null)
            {
                //blocking***
                _sqlParserMx.UseFlushMode(true);
                //wait where   

                //TODO: review here *** tight loop
                while (!_recvComplete)
                {
                    //wait 
                    if (_conn.WaitingTerminated)
                    {
                        return;//don't do the rest
                    }

                    //TODO: review here *** tight loop
                    System.Threading.Thread.Sleep(0);
                };


                _sqlParserMx.UseFlushMode(false); //switch back// 
                //blocking 
                if (_prepareContext != null)
                {
                    _conn.InitWait();
                    ClosePrepareStmt_A(_conn.UnWait);
                    if (!_conn.Wait())
                    {
                        //handle wait timeout
                        _execState = QueryExecState.Terminated;
                        _conn.BindingQuery = null;//release
                        _conn = null;
                        return;
                    }
                }
                _execState = QueryExecState.Closed;
                _conn.BindingQuery = null;//release
                _conn = null;
            }
            else
            {

                //non blocking***
                if (!_recvComplete)
                {
                    _sqlParserMx.UseFlushMode(true);

                    MonitorWhenRecvComplete(() =>
                    {
                        _sqlParserMx.UseFlushMode(false);
                        if (_prepareContext != null)
                        {
                            ClosePrepareStmt_A(() =>
                            {
                                _execState = QueryExecState.Closed;
                                _conn.BindingQuery = null;//release
                                _conn = null;
                                nextAction();
                            });
                        }
                        else
                        {
                            _execState = QueryExecState.Closed;
                            _conn.BindingQuery = null;//release
                            _conn = null;
                            nextAction();
                        }

                    });
                }
                else
                {
                    if (_prepareContext != null)
                    {
                        ClosePrepareStmt_A(() =>
                        {
                            _execState = QueryExecState.Closed;
                            _conn.BindingQuery = null;//release
                            _conn = null;
                            nextAction();
                        });
                    }
                    else
                    {
                        _execState = QueryExecState.Closed;
                        _conn.BindingQuery = null;//release
                        _conn = null;
                        nextAction();
                    }
                }
            }
        }



        void ExecuteNonPrepare_A(Action nextAction)
        {

            //abstract mysql packet
            //----------------------------------------
            // (4 bytes header)| ( user content)
            //----------------------------------------

            // 4 bytes header => 3 bytes for user content len (so max => (1 << 24) - 1 or 0xFF, 0xFF, 0xFF
            //                => 1 byte for packet number, so this value is 0-255  


            _sqlParserMx.UseResultParser();
            _writer.Reset();//*** packet number is reset to 0

            //---------------
            //https://dev.mysql.com/doc/internals/en/mysql-packet.html
            //If a MySQL client or server wants to send data, it:
            //Splits the data into packets of size (2^24)−1 bytes
            //Prepends to each chunk a packet header
            byte[] buffer = _writer.GetEncodeBytes(_sqlStrTemplate.BindValues(_cmdParams, false).ToCharArray());
            //---------------

            int totalLen = buffer.Length;
            //packet count 
            int packetCount = ((totalLen + Connection.MAX_PACKET_CONTENT_LENGTH) / Connection.MAX_PACKET_CONTENT_LENGTH);

#if DEBUG
            if (packetCount >= 255)
            {
                throw new NotSupportedException();
            }
#endif

            if (packetCount <= 1)
            {

                //--------------------------------------------
                //check SendIO buffer is large enough or not
                //if not, handle this by asking for more buffer
                //or throw error back to user
                //--------------------------------------------
                if (!_conn.EnsureSendIOBufferSize(totalLen))
                {
                    //how to handle exception
                    //TODO: review here,
                    //we should not throw exception on working thread
                    throw new NotSupportedException();
                }
                //--------------------------------------------
                _writer.ReserveHeader();
                _writer.WriteByte((byte)Command.QUERY);
                //check if we can write data in 1 packet or not 
                _writer.WriteBinaryString(buffer);
                //var header = new PacketHeader(_writer.OnlyPacketContentLength, _writer.IncrementPacketNumber());
                _writer.WriteHeader(_writer.OnlyPacketContentLength, _writer.IncrementPacketNumber());
                //
                SendAndRecv_A(_writer.ToArray(), nextAction);
            }
            else
            {
                //we need to split to multiple packet 
                //and we need max sendIO buffer 
                //--------------------------------------------
                //check SendIO buffer is large enough or not
                //if not, handle this by asking for more buffer
                //or throw error back to user
                //--------------------------------------------

                if (!_conn.EnsureSendIOBufferSize(Connection.MAX_PACKET_CONTENT_LENGTH))
                {
                    //how to handle exception
                    //TODO: review here,
                    //we should not throw exception on working thread
                    throw new NotSupportedException();
                }

                //--------------------------------------------
                int currentPacketContentSize = Connection.MAX_PACKET_CONTENT_LENGTH;
                int pos = 0;


                for (int packet_num = 0; packet_num < packetCount; ++packet_num)
                {
                    //write each packet to stream
                    _writer.ReserveHeader();
                    if (packet_num == 0)
                    {
                        //first packet
                        _writer.WriteByte((byte)Command.QUERY);
                        _writer.WriteBinaryString(buffer, pos, currentPacketContentSize - 1);//remove 1 query cmd
                        pos += (currentPacketContentSize - 1);
                    }
                    else if (packet_num == packetCount - 1)
                    {
                        //last packet
                        currentPacketContentSize = totalLen - pos;
                        _writer.WriteBinaryString(buffer, pos, currentPacketContentSize);
                    }
                    else
                    {
                        //in between
                        _writer.WriteBinaryString(buffer, pos, currentPacketContentSize);
                        pos += (currentPacketContentSize);
                    }

                    //--------------
                    //check if we can write data in 1 packet or not                   
                    //var header = new PacketHeader((uint)currentPacketContentSize, (byte)packet_num);//*** 
                    _writer.WriteHeader((uint)currentPacketContentSize, (byte)packet_num);//*** 
                    _conn.EnqueueOutputData(_writer.ToArray()); //enqueue output data
                    _writer.Reset();
                }

                _conn.StartSend(() =>
                {
                    //when send complete then start recv result
                    RecvPacket_A(nextAction);
                });
            }
        }
        void ExecutePrepareQuery_A(Action nextAction)
        {
            //make sure that when Finished is called
            //when this complete
            if (_prepareContext == null)
            {
                ExecuteNonPrepare_A(nextAction);
                return;
            }
            if (_prepareContext.statementId == 0)
            {
                throw new Exception("exec Prepare() first");
            }
            //------------------------------------------------ 
            ResetPrepareStmt_A(() =>
            {
                //The server will send a OK_Packet if the statement could be reset, a ERR_Packet if not.
                //--------------------------------------------  
                _prepareStatementMode = true;
                _sqlParserMx.UseResultParser(true);
                //-------------------------------------------- 
                _writer.Reset();
                //fill prepared values 
                ComExecPrepareStmtPacket.Write(_writer,
                _prepareContext.statementId,
                _prepareContext.PrepareBoundData(_cmdParams));
                //-------------------------------------------- 
                SendAndRecv_A(_writer.ToArray(), nextAction);
            });
        }
        void ClosePrepareStmt_A(Action nextAction)
        {
            //for prepare only
            if (_execState == QueryExecState.Terminated)
            {
                return;
            }

            if (_prepareContext != null)
            {
                _sqlParserMx.UseResultParser();
                _writer.Reset();
                _execState = QueryExecState.Closed;
                //
                ComStmtClosePacket.Write(_writer, _prepareContext.statementId);
                //when close, not wait for recv
                SendPacket_A(_writer.ToArray(), nextAction);
            }
        }

        void ResetPrepareStmt_A(Action nextAction)
        {
            if (_prepareStatementMode && _prepareContext != null)
            {
                _sqlParserMx.UseResultParser();
                _writer.Reset();
                ComStmtResetPacket.Write(_writer, _prepareContext.statementId);
                SendAndRecv_A(_writer.ToArray(), nextAction);
            }
            else
            {
                nextAction();
            }
        }

        //----------------------------------------------
        bool _assignRecvCompleteHandler;
        bool _recvComplete = true;
        Action _whenRecvComplete;
        void RecvComplete()
        {
            //_recvComplete used by multithread
            _recvComplete = true;
            //need to store to local var
            if (_assignRecvCompleteHandler)
            {
                _assignRecvCompleteHandler = false;
                Action tmpRecvComplete = _whenRecvComplete;
                _whenRecvComplete = null; //clear 
                tmpRecvComplete();
            }
        }
        void MonitorWhenRecvComplete(Action whenRecvComplete)
        {
            if (_recvComplete)
            {
                //already complete
                whenRecvComplete(); //just call
            }
            else
            {
                //store to local var
                _whenRecvComplete = whenRecvComplete;
                _assignRecvCompleteHandler = true;
                //after assign check again
                //assignRecvCompleteHandler may changed by another thread
                if (_recvComplete && _assignRecvCompleteHandler)
                {
                    _assignRecvCompleteHandler = false;
                    _whenRecvComplete = null;
                    whenRecvComplete();
                }
            }
        }
        void RecvPacket_A(Action whenRecv)
        {
            _recvComplete = false;
            bool isFirstRecv = true;
            //before start recv
            _conn.StartReceive(result =>
            {
                if (result == null)
                {
                    throw new NotSupportedException();
                }
                else
                {
                    switch (result.Kind)
                    {
                        default: throw new NotSupportedException();//unknown
                        case MySqlResultKind.Ok:
                            {
                                OkPacket = (result as MySqlOkResult).okpacket;
                                RecvComplete();
                            }
                            break;
                        case MySqlResultKind.Error:
                            {
                                MySqlErrorResult error = result as MySqlErrorResult;
                                Error = error.errPacket;
                                RecvComplete();
                                if (_errorListener != null)
                                {
                                    _errorListener(error);
                                }
                                else
                                {
                                    //ERROR
                                    throw new MySqlExecException(error);
                                }
                            }
                            break;
                        case MySqlResultKind.PrepareResponse:
                            {
                                //The server will send a OK_Packet if the statement could be reset, a ERR_Packet if not.
                                //on prepare
                                MySqlPrepareResponseResult response = result as MySqlPrepareResponseResult;
                                _prepareContext = new PreparedContext(
                                    response.okPacket.statement_id,
                                    _sqlStrTemplate,
                                    response.tableHeader);
                                RecvComplete();
                            }
                            break;
                        case MySqlResultKind.TableResult:
                            {
                                //support partial table mode 
                                //last sub table is not partial table  
                                //and must notify reader first***
                                //before call  RecvComplete();

                                //-----------------------------------------  
                                MySqlTableResult tableResult = result as MySqlTableResult;
                                //***
                                _recvComplete = !tableResult.HasFollower;
                                //the _tableResultListener may modifid by other state (Close)
                                //if don't lock we need to store it to local var
                                _tableResultListener?.Invoke(tableResult);
                                //----------------------------------------- 
                                if (!tableResult.HasFollower)
                                {
                                    RecvComplete();
                                }
                            }
                            break;
                        case MySqlResultKind.MultiTableResult:
                            {
                                MySqlMultiTableResult multiTables = result as MySqlMultiTableResult;
                                List<MySqlTableResult> subTables = multiTables.subTables;
                                int j = subTables.Count;
                                for (int i = 0; i < j; ++i)
                                {
                                    MySqlTableResult table = subTables[i];
                                    if (i < j - 1)
                                    {
                                        //not the last one
                                        //(the last one may complete or not complete)
                                        table.HasFollower = true;

                                        //the _tableResultListener may modifid by other state (Close)
                                        //if don't lock we need to store it to local var
                                        _tableResultListener?.Invoke(table);
                                    }
                                    else
                                    {
                                        //this is the last one in the series
                                        //support partial table mode 
                                        //last sub table is not partial table  
                                        //and must notify reader first***
                                        //before call  RecvComplete();

                                        _recvComplete = !table.HasFollower;

                                        //the _tableResultListener may modifid by other state (Close)
                                        //if don't lock we need to store it to local var
                                        _tableResultListener?.Invoke(table);

                                        if (!table.HasFollower)
                                        {
                                            RecvComplete();
                                        }
                                    }
                                }

                            }
                            break;
                    }
                }
                //-----------------
                //exec once
                if (isFirstRecv)
                {
                    isFirstRecv = false;
                    whenRecv();
                    whenRecv = null;
                }
                //-----------------
            });
        }
        void SendPacket_A(byte[] packetBuffer, Action nextAction)
        {
            //send all to 
            _conn.StartSend(packetBuffer, 0, packetBuffer.Length, nextAction);
        }
        void SendAndRecv_A(byte[] packetBuffer, Action onRecv)
        {
            _conn.StartSend(packetBuffer, 0, packetBuffer.Length, () =>
            {
                RecvPacket_A(onRecv);
            });
        }
    }

    class PreparedContext
    {
        public readonly uint statementId;
        TableHeader _tableHeader;
        SqlStringTemplate _sqlStringTemplate;
        MyStructData[] _preparedValues;
        List<SqlBoundSection> _keys;
        public PreparedContext(uint statementId, SqlStringTemplate sqlStringTemplate, TableHeader tableHeader)
        {
            this.statementId = statementId;
            _sqlStringTemplate = sqlStringTemplate;
            _keys = _sqlStringTemplate.GetValueKeys();
            //----------------------------------------------
            _tableHeader = tableHeader;
            int serverFieldCount = tableHeader.ColumnCount;
            //add field information to _keys
            List<FieldPacket> fields = tableHeader.GetFields();
            //----------------------------------------------
            int bindingCount = 0;
            for (int i = 0; i < serverFieldCount; ++i)
            {
                FieldPacket f = fields[i];
                if (f.name == "?") //
                {
                    //this is binding field
                    _keys[bindingCount].fieldInfo = f;
                    bindingCount++;
                }
            }
            //some field from server is not binding field
            //so we select only binding field
            if (bindingCount != _keys.Count)
            {
                throw new Exception("key num not matched!");
            }
            //-------------------------------------------------
            _preparedValues = new MyStructData[bindingCount]; //***
        }

        public MyStructData[] PrepareBoundData(CommandParams cmdParams)
        {
            //1. check proper type and 
            //2. check all values are in its range  
            //extract and arrange 

            int j = _keys.Count;
            for (int i = 0; i < j; ++i)
            {
                SqlBoundSection key = _keys[i];
                if (!cmdParams.TryGetData(key.Text, out _preparedValues[i]))
                {
                    //not found key 
                    throw new Exception("not found " + _keys[i].Text);
                }
                else
                {
                    //-------------------------------
                    //TODO: check here 
                    //all field type is 253 ?
                    //error
                    //------------------------------- 
                    //check
                    //FieldPacket fieldInfo = key.fieldInfo;
                    //switch ((MySqlDataType)fieldInfo.columnType)
                    //{
                    //    case MySqlDataType.VARCHAR:
                    //    case MySqlDataType.VAR_STRING:
                    //        {
                    //            //check length
                    //            if (_preparedValues[i].myString.Length > fieldInfo.maxLengthOfField)
                    //            {
                    //                //TODO: notify user how to handle this data
                    //                //before error
                    //            }
                    //        }
                    //        break;
                    //    case MySqlDataType.BLOB:
                    //    case MySqlDataType.LONG_BLOB:
                    //    case MySqlDataType.MEDIUM_BLOB:
                    //        {
                    //            if (_preparedValues[i].myString.Length > fieldInfo.maxLengthOfField)
                    //            {
                    //                //TODO: notify user how to handle this data
                    //                //before error
                    //            }
                    //        }
                    //        break;
                    //}

                }
            }

            return _preparedValues;
        }
    }



    class TableHeader
    {
        QueryParsingConfig _parsingConfig;
        List<FieldPacket> _fields;
        Dictionary<string, int> _fieldNamePosMap;

        public TableHeader(bool isBinaryProtocol)
        {
            _fields = new List<FieldPacket>();
            IsBinaryProtocol = isBinaryProtocol;
        }
        public bool IsBinaryProtocol { get; private set; }
        public void AddField(FieldPacket field)
        {
            _fields.Add(field);
        }
        public List<FieldPacket> GetFields()
        {
            return _fields;
        }
        public FieldPacket GetField(int index)
        {
            return _fields[index];
        }
        public int ColumnCount
        {
            get { return _fields.Count; }
        }


        public int GetFieldIndex(string fieldName)
        {
            if (_fieldNamePosMap == null)
            {
                ///build map index
                int j = _fields.Count;
                _fieldNamePosMap = new Dictionary<string, int>(j);
                for (int i = 0; i < j; ++i)
                {
                    _fieldNamePosMap.Add(_fields[i].name, i);
                }
            }
            int found;
            if (!_fieldNamePosMap.TryGetValue(fieldName, out found))
            {
                return -1;
            }
            return found;
        }


    }
}