//MIT, 2015-present, brezza92, EngineKit and contributors

using System;
using System.Collections.Generic;
using SharpConnect.MySql.Internal;
using System.Text;

namespace SharpConnect.MySql
{
    namespace SyncPatt
    {
        public static partial class MySqlSyncPattExtension
        {
            public static void Close(this MySqlDataReader reader)
            {
                reader.InternalClose();
            }
        }
    }
    namespace AsyncPatt
    {
        public static partial class MySqlAsyncPattExtension
        {
            public static void Close(this MySqlDataReader reader, Action nextAction)
            {
                reader.InternalClose(nextAction);
            }
        }
    }

    public interface IStringConverter
    {
        string ReadConv(string input);
        string ReadConv(byte[] input);
        //-----------------------------------
        byte[] WriteConv(string input);
    }

    public class QueryParsingConfig
    {
        public bool UseLocalTimeZone;
        /// <summary>
        /// use string as datetime, not convert to datetime value
        /// </summary>
        public bool DateStrings;
        public string TimeZone;
        public bool SupportBigNumbers;
        public bool BigNumberStrings;
    }


    public abstract class MySqlDataReader
    {

        MySqlSubTable _currentSubTable;
        DataRowPacket[] _rows;
        int _currentRowIndex;
        int _subTableRowCount;
        bool _isEmptyTable = true; //default
        MyStructData[] _cells;
        BufferReader _bufferReader = new BufferReader();
        StringBuilder _tmpStringBuilder = new StringBuilder();
        IStringConverter _strConverter = s_utf8StrConv; //default
        bool _isBinaryProtocol;//isPrepare (binary) or text protocol

        QueryParsingConfig _queryParsingConf = s_defaultConf;
        Dictionary<string, int> _fieldMaps = null;
        Dictionary<string, int> _all_UPPER_CASE_fieldMaps = null;

        /// <summary>
        /// internal read may be blocked.
        /// </summary>
        /// <returns></returns>
        protected internal virtual bool InternalRead()
        {
            if (_currentRowIndex < _subTableRowCount)
            {
                SetCurrentRow(_currentRowIndex);
                _currentRowIndex++;
                return true;
            }
            else
            {
                return false;
            }
        }

        public virtual void SetCurrentRowIndex(int index)
        {
            _currentRowIndex = index;
            if (index < _rows.Length)
            {
                SetCurrentRow(index);
            }
        }

        public MySqlSubTable CurrentSubTable => _currentSubTable;

        public bool IsLastTable => _currentSubTable.IsLastTable;

        public int FieldCount => _currentSubTable.FieldCount;


        public IStringConverter StringConverter
        {
            get => _strConverter;
            set
            {
                if (value == null)
                {
                    //use default
                    _strConverter = s_utf8StrConv;
                }
                else
                {
                    _strConverter = value;
                }
            }
        }

        /// <summary>
        /// get field name of specific column index
        /// </summary>
        /// <param name="colIndex"></param>
        /// <returns></returns>
        public string GetName(int colIndex) => _currentSubTable.GetFieldDefinition(colIndex).Name;

        public bool HasRows => !_isEmptyTable && _subTableRowCount > 0;
        //---------------------------------------------

        internal bool StopReadingNextRow { get; set; } //for async read state

        internal virtual void InternalClose(Action nextAction = null) { }

        internal bool IsEmptyTable => _isEmptyTable;

        internal void SetCurrentSubTable(MySqlSubTable currentSubTable)
        {
            _currentSubTable = currentSubTable;
            _fieldMaps = null;

            if (!currentSubTable.IsEmpty)
            {
                _isEmptyTable = false;
                _rows = currentSubTable.GetMySqlTableResult().rows;
                _isBinaryProtocol = currentSubTable.IsBinaryProtocol;
                _subTableRowCount = _rows.Length;
                //buffer for each row 
                _cells = new MyStructData[currentSubTable.FieldCount];
            }
            else
            {
                _isEmptyTable = true;
                _isBinaryProtocol = false;
                _rows = null;
                _subTableRowCount = 0;

            }
            _currentRowIndex = 0;
        }
        internal void SetCurrentRow(int currentIndex)
        {
            //this for internal use
            DataRowPacket currentRow = _rows[currentIndex];
            //expand this row to buffer ***
            _bufferReader.SetSource(currentRow._rowDataBuffer);

            if (_isBinaryProtocol)
            {
                //read each cell , binary protocol ***
                //1. skip start packet byte [00]
                _bufferReader.Position = 1;
                //2. read null-bitmap, length:(column-count+7+2)/8
                //A Binary Protocol Resultset Row is made up of the NULL bitmap containing as many bits as we have columns in the resultset +2 and the values for columns that are not NULL in the Binary Protocol Value format.
                //see: https://dev.mysql.com/doc/internals/en/binary-protocol-resultset-row.html#packet-ProtocolBinary::ResultsetRow

                int columnCount = _currentSubTable.FieldCount;
                int nullBmpLen = (columnCount + 7 + 2) / 8;
                byte[] nullBitmap = _bufferReader.ReadBytes(nullBmpLen);

                for (int i = 0; i < columnCount; ++i)
                {
                    //check if this cell is null (1) or not (0)
                    int logicalBitPos = i + 2;
                    byte nullBmpByte = nullBitmap[logicalBitPos / 8];
                    int shift = logicalBitPos % 8;
                    if (((nullBmpByte >> shift) & 1) == 0)
                    {
                        //not null
                        _cells[i] = ReadCurrentRowBinaryProtocol(_currentSubTable.GetFieldDefinition(i));
                    }
                }

            }
            else
            {
                //read each cell , read as text protocol
                int columnCount = _currentSubTable.FieldCount;
                for (int i = 0; i < columnCount; ++i)
                {
                    _cells[i] = ReadCurrentRowTextProtocol(_currentSubTable.GetFieldDefinition(i));
                }
            }
        }
        MyStructData ReadCurrentRowBinaryProtocol(MySqlFieldDefinition f)
        {
            string numberString = null;
            MySqlDataType fieldType = (MySqlDataType)f.FieldType;
            MyStructData myData = new MyStructData();
            BufferReader r = _bufferReader;
            switch (fieldType)
            {
                case MySqlDataType.TIMESTAMP://
                case MySqlDataType.DATE://
                case MySqlDataType.DATETIME://
                case MySqlDataType.NEWDATE://
                    r.ReadLengthCodedDateTime(out myData.myDateTime);
                    myData.type = fieldType;
                    return myData;
                case MySqlDataType.TINY://length = 1;
                    myData.myInt32 = r.U1();
                    myData.type = fieldType;
                    return myData;
                case MySqlDataType.SHORT://length = 2;
                case MySqlDataType.YEAR://length = 2;
                    myData.myInt32 = (int)r.U2();
                    myData.type = fieldType;
                    return myData;
                case MySqlDataType.INT24:
                case MySqlDataType.LONG://length = 4;
                    myData.myInt32 = (int)r.U4();
                    myData.type = fieldType;
                    return myData;
                case MySqlDataType.FLOAT:
                    myData.myDouble = r.ReadFloat();
                    myData.type = fieldType;
                    return myData;
                case MySqlDataType.DOUBLE:
                    myData.myDouble = r.ReadDouble();
                    myData.type = fieldType;
                    return myData;
                case MySqlDataType.DECIMAL:
                case MySqlDataType.NEWDECIMAL:
                    {
                        QueryParsingConfig config = _queryParsingConf;
                        myData.myString = numberString = r.ReadLengthCodedString(this.StringConverter);
                        if (numberString == null || (f.IsZeroFill && numberString[0] == '0'))
                        {
                            myData.type = MySqlDataType.NULL;
                        }
                        else if (config.SupportBigNumbers &&
                            (config.BigNumberStrings || (Convert.ToInt64(numberString) > Packet.IEEE_754_BINARY_64_PRECISION)))
                        {
                            //store as string ?
                            //TODO: review here  again
                            myData.myString = numberString;
                            myData.type = fieldType;
                            throw new NotSupportedException();
                        }
                        else if (fieldType == MySqlDataType.LONGLONG)
                        {
                            myData.myInt64 = Convert.ToInt64(numberString);
                            myData.type = fieldType;
                        }
                        else//decimal
                        {
                            myData.myDecimal = Convert.ToDecimal(numberString);
                            myData.type = fieldType;
                        }
                        return myData;
                    }
                case MySqlDataType.LONGLONG:
                    myData.myInt64 = r.ReadInt64();
                    myData.type = fieldType;
                    return myData;
                case MySqlDataType.VARCHAR:
                    myData.myString = r.ReadLengthCodedString(this.StringConverter);
                    myData.type = fieldType;
                    return myData;
                case MySqlDataType.BIT:
                    myData.myBuffer = r.ReadLengthCodedBuffer();
                    myData.type = fieldType;
                    return myData;
                case MySqlDataType.STRING:
                case MySqlDataType.VAR_STRING:
                case MySqlDataType.TINY_BLOB:
                case MySqlDataType.MEDIUM_BLOB:
                case MySqlDataType.LONG_BLOB:
                case MySqlDataType.BLOB:
                    if (f.MarkedAsBinary)
                        myData.myBuffer = r.ReadLengthCodedBuffer();
                    else
                        myData.myString = r.ReadLengthCodedString(this.StringConverter);
                    myData.type = fieldType;
                    return myData;
                case MySqlDataType.GEOMETRY:
                    throw new NotSupportedException();
                default:
                    myData.myBuffer = r.ReadLengthCodedBuffer();
                    myData.type = MySqlDataType.NULL;
                    return myData;
            }
        }
        MyStructData ReadCurrentRowTextProtocol(MySqlFieldDefinition f)
        {

            BufferReader r = _bufferReader;
            MyStructData data = new MyStructData();
            MySqlDataType type = (MySqlDataType)f.FieldType;
            string numberString = null;
            switch (type)
            {

                case MySqlDataType.TIMESTAMP:
                case MySqlDataType.DATE:
                case MySqlDataType.DATETIME:
                case MySqlDataType.NEWDATE:
                    {

                        QueryParsingConfig qparsingConfig = _queryParsingConf;
                        _tmpStringBuilder.Length = 0;//clear 
                        data.myString = r.ReadLengthCodedString(this.StringConverter);
                        data.type = type;
                        if (data.myString == null)
                        {
                            data.type = MySqlDataType.NULL;
                            return data;
                        }

                        if (qparsingConfig.DateStrings)
                        {
                            //return datetime as string
                            return data;
                        }

                        //handle datetime
                        //TODO: review other invalid datetime eg 0000-00-00 00:00, 0000-00-00 00:00:00
                        if (data.myString.StartsWith("0000-00-00"))
                        {
                            data.myDateTime = DateTime.MinValue;//?                             
                            data.type = type;
                            return data;
                        }

                        //-------------------------------------------------------------
                        //    var originalString = dateString;
                        //    if (field.type === Types.DATE) {
                        //      dateString += ' 00:00:00';
                        //    }
                        _tmpStringBuilder.Append(data.myString);
                        //string originalString = dateString;
                        if (type == MySqlDataType.DATE)
                        {
                            _tmpStringBuilder.Append(" 00:00:00");
                        }
                        //    if (timeZone !== 'local') {
                        //      dateString += ' ' + timeZone;
                        //    }

                        if (!qparsingConfig.UseLocalTimeZone)
                        {
                            _tmpStringBuilder.Append(' ' + qparsingConfig.TimeZone);
                        }

                        if (!DateTime.TryParse(_tmpStringBuilder.ToString(),
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None,
                            out data.myDateTime))
                        {
                            //warning....
                            data.myDateTime = DateTime.MinValue;
                        }

                        //data.myDateTime = DateTime.Parse(tmpStringBuilder.ToString(),
                        //    System.Globalization.CultureInfo.InvariantCulture);
                        data.type = type;
                        _tmpStringBuilder.Length = 0;//clear 
                    }
                    return data;
                case MySqlDataType.TINY:
                case MySqlDataType.SHORT:
                case MySqlDataType.LONG:
                case MySqlDataType.INT24:
                case MySqlDataType.YEAR:

                    //TODO: review here,                    
                    data.myString = numberString = r.ReadLengthCodedString(this.StringConverter);
                    if (numberString == null ||
                        (f.IsZeroFill && numberString[0] == '0') ||
                        numberString.Length == 0)
                    {
                        data.type = MySqlDataType.NULL;
                    }
                    else
                    {
                        data.myInt32 = Convert.ToInt32(numberString);
                        data.type = type;
                    }
                    return data;
                case MySqlDataType.FLOAT:
                case MySqlDataType.DOUBLE:
                    data.myString = numberString = r.ReadLengthCodedString(this.StringConverter);
                    if (numberString == null || (f.IsZeroFill && numberString[0] == '0'))
                    {
                        data.type = MySqlDataType.NULL;
                    }
                    else
                    {
                        data.myDouble = Convert.ToDouble(numberString);
                        data.type = type;
                    }
                    return data;
                //    return (numberString === null || (field.zeroFill && numberString[0] == "0"))
                //      ? numberString : Number(numberString);
                case MySqlDataType.DECIMAL:
                case MySqlDataType.NEWDECIMAL:
                case MySqlDataType.LONGLONG:
                    //    numberString = parser.parseLengthCodedString();
                    //    return (numberString === null || (field.zeroFill && numberString[0] == "0"))
                    //      ? numberString
                    //      : ((supportBigNumbers && (bigNumberStrings || (Number(numberString) > IEEE_754_BINARY_64_PRECISION)))
                    //        ? numberString
                    //        : Number(numberString));

                    QueryParsingConfig config = _queryParsingConf;
                    data.myString = numberString = r.ReadLengthCodedString(this.StringConverter);
                    if (numberString == null || (f.IsZeroFill && numberString[0] == '0'))
                    {
                        data.type = MySqlDataType.NULL;
                    }
                    else if (config.SupportBigNumbers &&
                        (config.BigNumberStrings || (Convert.ToInt64(numberString) > Packet.IEEE_754_BINARY_64_PRECISION)))
                    {
                        //store as string ?
                        //TODO: review here  again
                        data.myString = numberString;
                        data.type = type;
                        throw new NotSupportedException();
                    }
                    else if (type == MySqlDataType.LONGLONG)
                    {
                        data.myInt64 = Convert.ToInt64(numberString);
                        data.type = type;
                    }
                    else//decimal
                    {
                        data.myDecimal = Convert.ToDecimal(numberString);
                        data.type = type;
                    }
                    return data;
                case MySqlDataType.BIT:

                    data.myBuffer = r.ReadLengthCodedBuffer();
                    data.type = type;
                    return data;
                //    return parser.parseLengthCodedBuffer();
                case MySqlDataType.STRING:
                case MySqlDataType.VAR_STRING:
                    {
                        //expect data type
                        if (f.MarkedAsBinary)
                        {
                            data.myString = r.ReadLengthCodedString(this.StringConverter);
                            data.type = (data.myBuffer != null) ? type : MySqlDataType.NULL;
                        }
                        else
                        {
                            data.myString = r.ReadLengthCodedString(this.StringConverter);
                            data.type = (data.myString != null) ? type : MySqlDataType.NULL;
                        }
                        return data;
                    }
                case MySqlDataType.TINY_BLOB:
                case MySqlDataType.MEDIUM_BLOB:
                case MySqlDataType.LONG_BLOB:
                case MySqlDataType.BLOB:
                    if (f.MarkedAsBinary)
                    {
                        data.myBuffer = r.ReadLengthCodedBuffer();
                        data.type = (data.myBuffer != null) ? type : MySqlDataType.NULL;
                    }
                    else
                    {
                        data.myString = r.ReadLengthCodedString(this.StringConverter);
                        data.type = (data.myString != null) ? type : MySqlDataType.NULL;
                    }
                    return data;
                //    return (field.charsetNr === Charsets.BINARY)
                //      ? parser.parseLengthCodedBuffer()
                //      : parser.parseLengthCodedString();
                case MySqlDataType.GEOMETRY:
                    //TODO: unfinished
                    data.type = MySqlDataType.GEOMETRY;
                    return data;
                default:
                    data.myString = r.ReadLengthCodedString(this.StringConverter);
                    data.type = type;
                    return data;
            }
        }

        //---------------------------------------------
        //TODO: check match type and check index here 
        public sbyte GetInt8(int colIndex) => (sbyte)_cells[colIndex].myInt32;

        public sbyte GetInt8(string colName) => GetInt8(GetOrdinal(colName));

        //TODO: check match type and check index here
        public byte GetUInt8(int colIndex) => (byte)_cells[colIndex].myInt32;

        public byte GetUInt8(string colName) => GetUInt8(GetOrdinal(colName));


        //TODO: check match type and check index here
        public short GetInt16(int colIndex) => (short)_cells[colIndex].myInt32;

        public short GetInt16(string colName) => GetInt16(GetOrdinal(colName));
        //TODO: check match type and check index here
        public ushort GetUInt16(int colIndex) => (ushort)_cells[colIndex].myInt32;

        public ushort GetUInt16(string colName) => GetUInt16(GetOrdinal(colName));

        //TODO: check match type and check index here             
        public int GetInt32(int colIndex) => _cells[colIndex].myInt32;

        public int ConvertToInt32(int colIndex)
        {
            if (_cells[colIndex].type == MySqlDataType.DECIMAL ||
               _cells[colIndex].type == MySqlDataType.NEWDECIMAL)
            {
                //parse from string 
                //to double -> to int
                string numAsString = _cells[colIndex].myString;
                if (double.TryParse(numAsString, out double result))
                {
                    return (int)result;
                }
                return 0;
            }
            return _cells[colIndex].myInt32;
        }
        public int GetInt32(string colName) => GetInt32(GetOrdinal(colName));

        //TODO: check match type and check index here
        public uint GetUInt32(int colIndex) => _cells[colIndex].myUInt32;

        public uint GetUInt32(string colName) => GetUInt32(GetOrdinal(colName));

        //TODO: check match type and check index here
        public long GetLong(int colIndex) => _cells[colIndex].myInt64;

        public long GetLong(string colName) => GetLong(GetOrdinal(colName));

        //TODO: check match type and check index here
        public ulong GetULong(int colIndex) => _cells[colIndex].myUInt64;

        public ulong GetULong(string colName) => GetULong(GetOrdinal(colName));

        //TODO: check match type and index here
        public decimal GetDecimal(int colIndex) => _cells[colIndex].myDecimal;
        public decimal GetDecimal(string colName) => GetDecimal(GetOrdinal(colName));

        //TODO: check match type and index here
        public float GetFloat(int colIndex) => (float)(_cells[colIndex].myDouble);
        public float GetFloat(string colName) => (float)(_cells[GetOrdinal(colName)].myDouble);

        //TODO: check match type and index here
        public double GetDouble(int colIndex) => _cells[colIndex].myDouble;

        //TODO: check match type and index here
        public double GetDouble(string colName) => GetDouble(GetOrdinal(colName));



        public string GetString(int colIndex)
        {
            //TODO: check match type and index here
            if (!(_cells[colIndex].myObj is string))
            {
                if (_cells[colIndex].myObj is byte[])
                {
                    return System.Text.Encoding.UTF8.GetString(_cells[colIndex].myBuffer);
                }
                else
                {
                    return null;
                }
            }
            return _cells[colIndex].myString;
        }
        public string GetString(string colName) => GetString(GetOrdinal(colName));

        //public string GetString(int colIndex, Encoding enc)
        //{
        //    //TODO: check match type and index here
        //    return cells[colIndex].myString;
        //}
        //public string GetString(string colName, Encoding enc)
        //{
        //    return GetString(GetOrdinal(colName));
        //}
        //public string GetString(int colIndex, IStringConverter strConv)
        //{
        //    //TODO: check match type and index here
        //    return cells[colIndex].myString;
        //}
        //public string GetString(string colName, IStringConverter strConv)
        //{
        //    return GetString(GetOrdinal(colName));
        //}

        /// <summary>
        /// copy part of buffer from specifc pos + readLen and write to specific dstIndex
        /// </summary>
        /// <param name="colIndex"></param>
        /// <param name="srcIndex">byteOffset of src</param>
        /// <param name="output">buffer to receive data</param>
        /// <param name="dstIndex">dst byte index</param>
        /// <param name="readLen">read len</param>
        /// <returns></returns>
        public long GetBytes(int colIndex, int srcIndex, byte[] output, int dstIndex, int readLen)
        {
            byte[] buffer = _cells[colIndex].myBuffer;

            if (srcIndex >= buffer.Length)
            {
                //no more data to read
                return 0;
            }
            else
            {
                if (srcIndex + readLen > buffer.Length)
                {
                    readLen = buffer.Length - srcIndex;
                }

                //check if we have avaliable dst space or not
                if (dstIndex < output.Length)
                {
                    if (dstIndex + readLen > output.Length)
                    {
                        //we can read some part of this 
                        //to output
                        readLen = output.Length - dstIndex;
                        Buffer.BlockCopy(buffer, srcIndex, output, dstIndex, readLen);
                        return readLen;
                    }
                    else
                    {
                        Buffer.BlockCopy(buffer, srcIndex, output, dstIndex, readLen);
                        return readLen;
                    }
                }
                else
                {
                    //dst index is output len
                    throw new NotSupportedException();
                }

            }

        }
        public int GetByteBufferLen(int colIndex)
        {
            byte[] buffer = _cells[colIndex].myBuffer;
            return (buffer != null) ? buffer.Length : 0;
        }
        //TODO: check match type and index here
        public byte[] GetBuffer(int colIndex) => _cells[colIndex].myBuffer;

        public byte[] GetBuffer(string colName) => GetBuffer(GetOrdinal(colName));

        public bool IsDBNull(int colIndex) => _cells[colIndex].type == MySqlDataType.NULL;

        public bool IsDBNull(string colName) => IsDBNull(GetOrdinal(colName));

        public DateTime GetDateTime(int colIndex)
        {
            //TODO: check match type and check index here
            //date time commin
            switch (_cells[colIndex].type)
            {
                case MySqlDataType.STRING:
                    return DateTime.Parse((string)_cells[colIndex].myString);
                case MySqlDataType.BLOB:
                    return DateTime.MinValue;
                case MySqlDataType.DATE:
                case MySqlDataType.DATETIME:
                    return _cells[colIndex].myDateTime;
                case MySqlDataType.DECIMAL:
                    {
                        //empty date-time
                        DateTime dtm = _cells[colIndex].myDateTime;
                        if (dtm == DateTime.MinValue)
                        {
                            return dtm;
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                default:
                    throw new NotSupportedException();
            }

        }
        public DateTime GetDateTime(string colName)
        {
            return GetDateTime(GetOrdinal(colName));
        }

        public object GetValue(int colIndex)
        {
            MyStructData data = _cells[colIndex];
            switch (data.type)
            {
                case MySqlDataType.BLOB:
                case MySqlDataType.LONG_BLOB:
                case MySqlDataType.MEDIUM_BLOB:
                case MySqlDataType.TINY_BLOB:
                    return data.myBuffer;

                case MySqlDataType.DATE:
                case MySqlDataType.NEWDATE:
                    return data.myDateTime;
                //stbuilder.Append('\'');
                //stbuilder.Append(data.myDateTime.ToString("yyyy-MM-dd"));
                //stbuilder.Append('\'');
                //break;
                case MySqlDataType.DATETIME:
                    //stbuilder.Append('\'');
                    //stbuilder.Append(data.myDateTime.ToString("yyyy-MM-dd hh:mm:ss"));
                    //stbuilder.Append('\'');
                    //break;
                    return data.myDateTime;
                case MySqlDataType.TIMESTAMP:
                case MySqlDataType.TIME:
                    ////TODO: review here
                    //stbuilder.Append('\'');
                    //stbuilder.Append(data.myDateTime.ToString("hh:mm:ss"));
                    //stbuilder.Append('\'');
                    //break;
                    return data.myDateTime;
                case MySqlDataType.STRING:
                case MySqlDataType.VARCHAR:
                case MySqlDataType.VAR_STRING:

                    //stbuilder.Append('\'');
                    ////TODO: check /escape string here ****
                    //stbuilder.Append(data.myString);
                    //stbuilder.Append('\'');
                    //break;
                    return data.myString;
                case MySqlDataType.BIT:
                    throw new NotSupportedException();
                // stbuilder.Append(Encoding.ASCII.GetString(new byte[] { (byte)data.myInt32 }));

                case MySqlDataType.DOUBLE:
                    return data.myDouble;
                //stbuilder.Append(data.myDouble.ToString());
                //break;
                case MySqlDataType.FLOAT:
                    return data.myDouble;//TODO: review here
                                         //stbuilder.Append(((float)data.myDouble).ToString());

                case MySqlDataType.TINY:
                case MySqlDataType.SHORT:
                case MySqlDataType.LONG:
                case MySqlDataType.INT24:
                case MySqlDataType.YEAR:
                    return data.myInt32;
                case MySqlDataType.LONGLONG:
                    return data.myInt64;

                case MySqlDataType.DECIMAL:
                case MySqlDataType.NEWDECIMAL:
                    return data.myDecimal;
                case MySqlDataType.NULL:
                    return null;
                default:
                    throw new NotSupportedException();
            }
        }

        public object GetValue(string colName) => GetValue(GetOrdinal(colName));


        //---------------------------------------------
        public int GetOrdinal(string colName)
        {
            if (_fieldMaps == null)
            {
                EvaluateFieldMap();
            }
            if (!_fieldMaps.TryGetValue(colName, out int foundIndex))
            {
                //try another chance, 
                if (_all_UPPER_CASE_fieldMaps == null)
                {
                    //init a new one, only when need
                    _all_UPPER_CASE_fieldMaps = new Dictionary<string, int>(_fieldMaps.Count);
                    foreach (var kv in _fieldMaps)
                    {
                        _all_UPPER_CASE_fieldMaps[kv.Key.ToUpper()] = kv.Value;
                    }
                }
                //try again
                if (!_all_UPPER_CASE_fieldMaps.TryGetValue(colName.ToUpper(), out foundIndex))
                {
                    throw new Exception("not found the colName " + colName);
                }
            }
            return foundIndex;
        }
        void EvaluateFieldMap()
        {

            _fieldMaps = new Dictionary<string, int>();
            if (_isEmptyTable) { return; }
            //-------------------------------------
            int j = _currentSubTable.FieldCount;
            for (int i = 0; i < j; ++i)
            {
                _fieldMaps.Add(_currentSubTable.GetFieldName(i), i);
            }

        }
        //--------------------------------
        static Utf8StringConverter s_utf8StrConv = new Utf8StringConverter();
        static QueryParsingConfig s_defaultConf;
        static MySqlDataReader()
        {
            s_defaultConf = new QueryParsingConfig();
            //{
            //    TimeZone = userConfig.timezone,
            //    UseLocalTimeZone = userConfig.timezone.Equals("local"),
            //    BigNumberStrings = userConfig.bigNumberStrings,
            //    DateStrings = userConfig.dateStrings,
            //    SupportBigNumbers = userConfig.supportBigNumbers
            //};
        }

    }

    public class Utf8StringConverter : IStringConverter
    {
        public string ReadConv(byte[] input)
        {
            return Encoding.UTF8.GetString(input);
        }

        public string ReadConv(string input)
        {
            throw new NotImplementedException();
        }
        public byte[] WriteConv(string writeStr)
        {
            return Encoding.UTF8.GetBytes(writeStr.ToCharArray());
        }
    }

    class MySqlExecException : Exception
    {
        public MySqlExecException(MySqlErrorResult err)
            : base(err.ToString())
        {
            this.Error = err;
        }
        public MySqlErrorResult Error { get; private set; }
        public override string ToString()
        {
            return Error.ToString();
        }
    }
    class MySqlQueryDataReader : MySqlDataReader
    {
        Query _query;
        Queue<MySqlTableResult> _subTables = new Queue<MySqlTableResult>();

        bool _emptySubTable = true;
        //-------------------------

        //int currentTableRowCount = 0;
        //int currentRowIndex = 0;
        //-------------------------
        bool _firstResultArrived;
        bool _tableResultIsNotComplete;
        object _tableResultCompleteLock = new object();
        Action<MySqlQueryDataReader> _onFirstDataArrived;
        MySqlErrorResult _errorResult = null;

        internal MySqlQueryDataReader(Query query)
        {

            _query = query;
            //set result listener for query object  before actual query.Read()
            query.SetErrorListener(err =>
            {
                lock (_tableResultCompleteLock)
                {
                    _firstResultArrived = true;
                    _tableResultIsNotComplete = false;

                    //ref: http://www.albahari.com/threading/part4.aspx#_Signaling_with_Wait_and_Pulse
                    System.Threading.Monitor.Pulse(_tableResultCompleteLock);
                }
                _errorResult = err;
            });
            query.SetResultListener(subtable =>
            {
                //we need the subtable must arrive in correct order *** 
                lock (_subTables)
                {
                    _subTables.Enqueue(subtable);
                }

                bool invokeFirstDataArrive = false;

                lock (_tableResultCompleteLock)
                {

                    invokeFirstDataArrive = !_firstResultArrived;
                    _firstResultArrived = true;
                    _tableResultIsNotComplete = subtable.HasFollower; //*** 

                    //ref: http://www.albahari.com/threading/part4.aspx#_Signaling_with_Wait_and_Pulse
                    System.Threading.Monitor.Pulse(_tableResultCompleteLock);
                }

                //---
                if (invokeFirstDataArrive && _onFirstDataArrived != null)
                {
                    _onFirstDataArrived(this);
                    _onFirstDataArrived = null;
                }

            });
        }
        public bool HasError => _errorResult != null;

        public MySqlErrorResult Error => _errorResult;


        public override void SetCurrentRowIndex(int index)
        {
            //set support in this mode
            throw new NotSupportedException();
        }

        const int EACH_ROUND = 20;//
        /// <summary>
        /// blocking, wait for first data arrive
        /// </summary>
        internal void WaitUntilFirstDataArrive()
        {
            int try_lim = _query.LockWaitingMilliseconds / EACH_ROUND;
            int n_tryCount = 0;

        TRY_AGAIN:
            //some time no result return from server
            //eg. call store procedure that not return any result
            //
            if (_query.OkPacket != null)
            {
                return;
            }

            //
            if (_query.WaitingTerminated)
            {
                return;
            }
            if (n_tryCount > 1)
            {
                System.Threading.Thread.Sleep(EACH_ROUND);
            }
            n_tryCount++;

            if (n_tryCount > try_lim)
            {
                //timeout
                return;
            }

            if (_emptySubTable)
            {
                //no current table 
                bool hasSomeSubTables = false;
                lock (_subTables)
                {
                    if (_subTables.Count > 0)
                    {
                        MySqlSubTable subt = new MySqlSubTable(_subTables.Dequeue());
                        SetCurrentSubTable(subt);
                        hasSomeSubTables = true;
                    }
                }
                if (!hasSomeSubTables && !_firstResultArrived)
                {
                    //wait for table is complete
                    //ref: http://www.albahari.com/threading/part4.aspx#_Signaling_with_Wait_and_Pulse
                    //--------------------------------
                    lock (_tableResultCompleteLock)
                    {
                        int tryCount = 0;
                        while (_tableResultIsNotComplete && !_firstResultArrived)
                        {
                            if (tryCount > 3)
                            {
                                throw new Exception("timeout!");
                            }
                            System.Threading.Monitor.Wait(_tableResultCompleteLock, 250);//wait within 250 ms lock
                            tryCount++;
                        }
                    }
                    //we are in isPartial table mode (not complete)
                    //so must wait until the table arrive ** 
                    goto TRY_AGAIN;
                }
            }
        }


        /// <summary>
        /// non blocking
        /// </summary>
        /// <param name="onFirstDataArrived"></param>
        internal void SetFirstDataArriveDelegate(Action<MySqlQueryDataReader> onFirstDataArrived)
        {
            _onFirstDataArrived = onFirstDataArrived;
        }



        /// <summary>
        /// async, read each sub table
        /// </summary>
        /// <param name="onEachSubTable"></param>
        internal void ReadSubTable(Action<MySqlSubTable> onEachSubTable)
        {
        TRY_AGAIN:

            if (this.IsEmptyTable)
            {
                //no current table  
                bool hasSomeSubTables = false;
                lock (_subTables)
                {
                    if (_subTables.Count > 0)
                    {
                        //1. get subtable
                        SetCurrentSubTable(new MySqlSubTable(_subTables.Dequeue()));
                        hasSomeSubTables = true;
                    }
                }
                if (!hasSomeSubTables)
                {
                    if (_tableResultIsNotComplete)
                    {
                        //we are in isPartial table mode (not complete)
                        //so must wait until the table arrive **
                        //------------------                    
                        CentralWaitingTasks.AddWaitingTask(new WaitingTask(() =>
                        {
                            if (_tableResultIsNotComplete)
                            {
                                //not complete, continue waiting
                                return false;
                            }
                            else
                            {
                                //try again 
                                ReadSubTable(onEachSubTable);
                                return true;
                            }
                        }));
                    }
                    else if (!_firstResultArrived)
                    {

                        CentralWaitingTasks.AddWaitingTask(new WaitingTask(() =>
                        {
                            if (!_firstResultArrived)
                            {
                                //not complete, continue waiting
                                return false;
                            }
                            else
                            {
                                //try again
                                ReadSubTable(onEachSubTable);
                                return true;
                            }
                        }));
                    }
                    else
                    {
                        //finish
                        return;
                    }
                }
                else
                {
                    //has some subtable
                    //so start clear here
                    onEachSubTable(CurrentSubTable);
                    //after invoke
                    SetCurrentSubTable(MySqlSubTable.Empty);
                    goto TRY_AGAIN;
                }
            }
        }


        /// <summary>
        /// async read row
        /// </summary>
        /// <param name="onEachRow"></param>
        public void Read(Action onEachRow)
        {
            ReadSubTable(subTable =>
            {
                //on each subtable
                MySqlDataReader tableReader = subTable.CreateDataReader();

                tableReader.StringConverter = this.StringConverter;

                int j = subTable.RowCount;
                for (int i = 0; i < j; ++i)
                {
                    tableReader.SetCurrentRowIndex(i);
                    onEachRow();
                }
                //if last one
                if (subTable.IsLastTable)
                {
                    //async close
                    this.InternalClose(() => { });
                }
            });
        }

        /// <summary>
        /// sync read row
        /// </summary>
        /// <returns></returns>
        protected internal override bool InternalRead()
        {

        TRY_AGAIN:
            if (IsEmptyTable)
            {
                //no current table 
                bool hasSomeSubTables = false;
                lock (_subTables)
                {
                    if (_subTables.Count > 0)
                    {
                        SetCurrentSubTable(new MySqlSubTable(_subTables.Dequeue()));
                        hasSomeSubTables = true;
                    }
                }

                if (!hasSomeSubTables)
                {

                    if (_tableResultIsNotComplete)
                    {
                        //we are in isPartial table mode (not complete)
                        //so must wait until the table arrive **
                        //------------------                    
                        //wait ***
                        //------------------ 
                        lock (_tableResultCompleteLock)
                        {
                            int zeroCount = 0;
                            while (_tableResultIsNotComplete)
                            {
                                int subTableCount = 0;
                                lock (_subTables)
                                {
                                    subTableCount = _subTables.Count;
                                }
                                //
                                if (subTableCount == 0)
                                {
                                    if (zeroCount > 3)
                                    {
                                        throw new Exception("timeout!");
                                        //zeroCount = 0;
                                    }
                                    zeroCount++;
                                    System.Threading.Monitor.Wait(_tableResultCompleteLock, 250); //wait within 250 ms lock
                                }
                                else
                                {
                                    break; //break from while
                                }
                            }
                        }
                        goto TRY_AGAIN;
                    }
                    else
                    {
                        //not in partial table mode
                        return false;
                    }
                }
            }
            //------------------------------------------------------------------
            if (base.InternalRead())
            {
                return true;
            }
            else
            {
                SetCurrentSubTable(MySqlSubTable.Empty);
                goto TRY_AGAIN;
            }
        }
        internal override void InternalClose(Action nextAction = null)
        {

            if (_query.WaitingTerminated)
            {
                return;
            }

            if (nextAction == null)
            {
                //block
                _query.Close();
            }
            else
            {
                //unblock
                _query.Close(() =>
                {
                    //after close  (just close relation with the connection)
                    //we can continue read cache data in this reader ***
                    //so we don't clear the cache data . 
                    //currentRowIndex = 0;
                    //currentTableRowCount = 0;
                    //currentSubTable = MySqlSubTable.Empty;
                    //subTables.Clear(); 
                    nextAction();
                });
            }
        }

    }



    enum ProperDataType
    {
        Unknown,
        Bool,
        Byte,
        Sbyte,
        /// <summary>
        /// 2 byte width charactor
        /// </summary>
        Char,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        //----------
        Float32,
        Double64,
        Decimal,
        DateTime,
        //----------
        String,
        Buffer,
        //----------
    }


    class MySubTableDataReader : MySqlDataReader
    {
        //for read on each subtable
        internal MySubTableDataReader(MySqlSubTable tableResult)
        {
            SetCurrentSubTable(tableResult);
        }
    }

    public struct MySqlSubTable
    {
        public static readonly MySqlSubTable Empty = new MySqlSubTable();
        readonly MySqlTableResult _tableResult;
        internal MySqlSubTable(MySqlTableResult tableResult)
        {
            _tableResult = tableResult;
        }
        internal bool IsBinaryProtocol => _tableResult.tableHeader.IsBinaryProtocol;
        public SubTableHeader Header
        {
            get
            {
                if (_tableResult == null)
                {
                    //empty subtable header
                    return new SubTableHeader();
                }
                else
                {

                    return new SubTableHeader(_tableResult.tableHeader);
                }
            }
        }

        public int RowCount => _tableResult.rows.Length;

        public bool HasRows => _tableResult.rows != null && _tableResult.rows.Length > 0;

        internal MySqlTableResult GetMySqlTableResult() => _tableResult;

        internal DataRowPacket GetRow(int index) => _tableResult.rows[index];

        public bool IsEmpty => _tableResult == null;

        public MySqlDataReader CreateDataReader() => new MySubTableDataReader(this);

        public int FieldCount => _tableResult.tableHeader.ColumnCount;

        public MySqlFieldDefinition GetFieldDefinition(int index) => new MySqlFieldDefinition(_tableResult.tableHeader.GetField(index));

        public string GetFieldName(int index) => _tableResult.tableHeader.GetField(index).name;

        public int GetFieldType(int index) => _tableResult.tableHeader.GetField(index).columnType;

        public MySqlFieldDefinition GetFieldDefinition(string fieldname)
        {
            int index = _tableResult.tableHeader.GetFieldIndex(fieldname);
            if (index > -1)
            {
                return GetFieldDefinition(index);
            }
            else
            {
                return new MySqlFieldDefinition();
            }
        }
        //----------------------------
        public bool IsLastTable => !_tableResult.HasFollower;


        //-------------------------------------------------------
        public static bool operator ==(MySqlSubTable sub1, MySqlSubTable sub2)
        {
            return sub1._tableResult == sub2._tableResult;
        }
        public static bool operator !=(MySqlSubTable sub1, MySqlSubTable sub2)
        {
            return sub1._tableResult != sub2._tableResult;
        }
        public override int GetHashCode() => _tableResult.GetHashCode();

        public override bool Equals(object obj)
        {
            if (obj is MySqlSubTable)
            {
                return ((MySqlSubTable)obj)._tableResult == _tableResult;
            }
            return false;
        }
    }


    public struct SubTableHeader
    {
        TableHeader _tableHeader;
        internal SubTableHeader(TableHeader tableHeader)
        {
            _tableHeader = tableHeader;
        }
        public static bool operator ==(SubTableHeader sub1, SubTableHeader sub2)
        {
            return sub1._tableHeader == sub2._tableHeader;
        }
        public static bool operator !=(SubTableHeader sub1, SubTableHeader sub2)
        {
            return sub1._tableHeader != sub2._tableHeader;
        }
        public override int GetHashCode()
        {
            return _tableHeader.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            if (obj is SubTableHeader)
            {
                return ((SubTableHeader)obj)._tableHeader == _tableHeader;
            }
            return false;
        }
    }

    public struct MySqlFieldDefinition
    {
        FieldPacket _fieldPacket;

        public static readonly MySqlFieldDefinition Empty = new MySqlFieldDefinition();

        internal MySqlFieldDefinition(FieldPacket fieldPacket)
        {
            _fieldPacket = fieldPacket;
        }
        public bool IsEmpty => _fieldPacket == null;

        public int FieldType => _fieldPacket.columnType;

        public string Name => _fieldPacket.name;

        public int FieldIndex => _fieldPacket.FieldIndex;

        internal bool MarkedAsBinary => _fieldPacket.charsetNr == (int)CharSets.BINARY;

        internal bool IsZeroFill => _fieldPacket.zeroFill;
    }

}