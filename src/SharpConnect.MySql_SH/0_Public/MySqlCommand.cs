//MIT, 2015-present, brezza92, EngineKit and contributors

using System;
using SharpConnect.MySql.Internal;
namespace SharpConnect.MySql
{
    namespace SyncPatt
    {
        public static partial class MySqlSyncPattExtension
        {

            public static void Prepare(this MySqlCommand cmd)
            {
                cmd.InternalPrepare();
            }
            public static void ClosePrepare(this MySqlCommand cmd)
            {
                cmd.InternalClosePrepare();
            }
            public static MySqlDataReader ExecuteReader(this MySqlCommand cmd)
            {
                return cmd.InternalExecuteReader();
            }
            public static bool Read(this MySqlDataReader reader)
            {
                return reader.InternalRead();
            }
            public static object ExecuteScalar(this MySqlCommand cmd)
            {
                object result = null;
                MySqlDataReader reader = cmd.InternalExecuteReader();
                if (reader.Read())
                {
                    result = reader.GetValue(0);
                }
                reader.Close();
                return result;
            }

            public static uint ExecuteNonQuery(this MySqlCommand cmd)
            {
                cmd.InternalExecuteNonQuery();
                if (cmd.HasSocketConnectionError)
                {
                    cmd.HasError = true;
                    if (cmd.ErrorMsg != null)
                    {
                        cmd.ErrorMsg += ";Socket Conn ERR";
                    }
                }
                if (cmd.HasError) //has some command error
                {
                    //do error routing 
                    if (cmd.HasErrorHandler)
                    {
                        cmd.InternalInvokeErrorHandler();
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }

                return cmd.AffectedRows;
            }
        }
    }
    namespace AsyncPatt
    {
        public static partial class MySqlAsyncPattExtension
        {

            public static void Prepare(this MySqlCommand cmd, Action nextAction)
            {
                cmd.InternalPrepare(nextAction);
            }
            public static void ClosePrepare(this MySqlCommand cmd, Action nextAction)
            {
                cmd.InternalClosePrepare(nextAction);
            }
            public static void ExecuteReader(this MySqlCommand cmd, Action<MySqlDataReader> eachRow)
            {
                cmd.InternalExecuteReader(reader =>
                {
                    //reader is ready  
                    while (reader.InternalRead())
                    {
                        eachRow(reader);
                        if (reader.StopReadingNextRow)
                        {
                            break;
                        }
                    }
                    //---
                    //close the reader
                    reader.Close(() => { });
                });
            }

            public static void ExecuteSubTableReader(this MySqlCommand cmd, Action<MySqlDataReader> readerReady)
            {
                cmd.InternalExecuteSubTableReader(readerReady);
            }
            /// <summary>
            /// non-blocking, read only single value
            /// </summary>
            /// <param name="nextAction"></param>
            /// <returns></returns>
            public static void ExecuteScalar<T>(this MySqlCommand cmd, Action<T> resultReady)
            {
                cmd.InternalExecuteSubTableReader(reader =>
                {
                    object result = reader.GetValue(0);
                    //call user result ready***
                    resultReady((T)result);
                    //
                });
            }
            /// <summary>
            ///non-blocking, execute non query
            /// </summary>
            /// <param name="nextAction"></param>
            public static void ExecuteNonQuery(this MySqlCommand cmd, Action nextAction)
            {
                cmd.InternalExecuteNonQuery(nextAction);
            }
        }
    }

    public class MySqlCommand : IDisposable
    {
        Query _query;
        bool _isPreparedStmt;
        SqlStringTemplate _sqlStringTemplate;

        public MySqlCommand(string sql)
            : this(new SqlStringTemplate(sql), new CommandParams(), null)
        {
        }
        public MySqlCommand(string sql, MySqlConnection conn)
            : this(new SqlStringTemplate(sql), new CommandParams(), conn)
        {
        }
        public MySqlCommand(string sql, CommandParams cmds, MySqlConnection conn)
            : this(new SqlStringTemplate(sql), cmds, conn)
        {

        }
        public MySqlCommand(SqlStringTemplate sql, MySqlConnection conn)
            : this(sql, new CommandParams(), conn)
        {
        }

        public MySqlCommand(SqlStringTemplate sql, CommandParams cmds, MySqlConnection conn)
        {
            _sqlStringTemplate = sql;
            Parameters = cmds;
            Connection = conn;
            if (conn != null)
            {
                this.StringConverter = conn.StringConv;
            }
        }

        public CommandParams Parameters { get; set; }
        public string CommandText => _sqlStringTemplate.UserRawSql;

        MySqlConnection _conn;
        public MySqlConnection Connection
        {
            get => _conn;
            set
            {
                _conn = value;
                if (this.StringConverter == null && value != null)
                {
                    this.StringConverter = value.StringConv;
                }
            }
        }


        IStringConverter _stringConv;
        public IStringConverter StringConverter
        {
            get => _stringConv;
            set
            {
                _stringConv = value;
                Parameters.StringConv = value;
            }
        }

        /// <summary>
        /// sync/async prepare
        /// </summary>
        /// <param name="nextAction"></param>
        internal void InternalPrepare(Action nextAction = null)
        {
            //prepare sql command;
            _isPreparedStmt = true;
            _query = new Query(Connection.Conn, _sqlStringTemplate, Parameters);
            _query.SetErrorListener(err =>
            {
                HasError = true;
                ErrorMsg = err.Message;
            });
            _query.Prepare(nextAction);
        }
        internal void InternalClosePrepare(Action nextAction = null)
        {
            if (_isPreparedStmt)
            {
                _query.Close(nextAction);
            }
            else
            {

            }
        }
        /// <summary>
        /// sync execute reader
        /// </summary>
        /// <returns></returns>
        internal MySqlDataReader InternalExecuteReader()
        {
            if (!_isPreparedStmt)
            {
                _query = new Query(this.Connection.Conn, _sqlStringTemplate, Parameters);
            }

            var reader = new MySqlQueryDataReader(_query);
            reader.StringConverter = this.StringConverter;
            _query.Execute(true, null);
            reader.WaitUntilFirstDataArrive();

            //after execute in sync mode (this method)
            //reader will wait unit first result arrive            
            if (reader.HasError)
            {
                //throw exception
                reader.InternalClose();
                throw new MySqlExecException(reader.Error);
            }

            return reader;
        }

        /// <summary>
        /// async exec reader, notify the when reader is ready
        /// </summary>
        internal void InternalExecuteReader(Action<MySqlDataReader> readerReady)
        {
            if (!_isPreparedStmt)
            {
                _query = new Query(this.Connection.Conn, _sqlStringTemplate, Parameters);
            }
            var reader = new MySqlQueryDataReader(_query);
            reader.StringConverter = this.StringConverter;
            //in non bloking mode, set this
            reader.SetFirstDataArriveDelegate(dataReader =>
            {
                //data reader is ready
                //then start async read on each sub table
                readerReady(dataReader);
            });
            //after execute in asyn mode( this method)
            //reader just return, not block,
            //
            //and when the first data arrive,
            //in invoke dataReaderReader delegate
            _query.Execute(true, () => { });//send empty lambda for async  
        }
        /// <summary>
        /// async exec, on each sub table
        /// </summary>
        internal void InternalExecuteSubTableReader(Action<MySqlDataReader> onEachSubTable)
        {
            if (!_isPreparedStmt)
            {
                _query = new Query(this.Connection.Conn, _sqlStringTemplate, Parameters);
            }
            var reader = new MySqlQueryDataReader(_query);
            reader.StringConverter = this.StringConverter;
            //in non bloking mode, set this
            reader.SetFirstDataArriveDelegate(dataReader =>
            {
                //data reader is ready
                //then start async read on each sub table
                dataReader.ReadSubTable(subt =>
                {
                    //table is ready for read***
                    //just read single value 
                    MySqlDataReader subtReader = subt.CreateDataReader();
                    subtReader.StringConverter = this.StringConverter;
                    onEachSubTable(subtReader);

                    if (subt.IsLastTable)
                    {
                        //auto close reader 
                        dataReader.InternalClose(() => { });//send empty lambda for async  
                    }
                });
            });
            //after execute in asyn mode( this method)
            //reader just return, not block,
            //
            //and when the first data arrive,
            //in invoke dataReaderReader delegate
            _query.Execute(true, () => { });//send empty lambda for async  
        }

        /// <summary>
        /// sync/async execute non query
        /// </summary>
        /// <param name="nextAction"></param>
        internal void InternalExecuteNonQuery(Action nextAction = null)
        {
            if (!_isPreparedStmt)
            {
                _query = new Query(Connection.Conn, _sqlStringTemplate, Parameters);
                _query.SetErrorListener(err =>
                {
                    ErrorMsg = err.Message;
                    HasError = true;
                });
            }
            _query.Execute(false, nextAction);
        }

        public string ErrorMsg { get; internal set; }
        public bool HasError { get; internal set; }

        //after execute non query      
        public uint LastInsertedId => _query.OkPacket.InsertIdAsUInt32;

        //after execute non query
        public uint AffectedRows => (_query.OkPacket != null) ? _query.OkPacket.AffectedRowsAsUInt32 : 0;

        public void Dispose()
        {
            if (_query != null)
            {
                _query.Close();
                _query = null;
            }
        }


        internal bool HasSocketConnectionError
        {
            get
            {
                if (_conn != null && _conn.Conn.WorkingState == WorkingState.Error)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }



        //-------------
        //this is our extension
        MySqlCommandErrHandler _errHandler;
        public void SetOnErrorHandler(MySqlCommandErrHandler errHandler)
        {
            _errHandler = errHandler;
        }
        public delegate void MySqlCommandErrHandler(MySqlCommand cmd);
        internal bool HasErrorHandler => _errHandler != null;
        internal void InternalInvokeErrorHandler()
        {
            _errHandler?.Invoke(this);
        }
    }



}