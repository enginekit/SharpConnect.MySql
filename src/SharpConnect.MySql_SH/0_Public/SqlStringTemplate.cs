//MIT, 2015-present, brezza92, EngineKit and contributors 

using System;
using System.Collections.Generic;
using System.Text;
namespace SharpConnect.MySql.Internal
{
    //---------------------------------
    //value key => prefix with ? or @
    //special key (extension) prefix with ?? only ***

    enum ParseState
    {
        FIND_MARKER,
        COLLECT_MARKER_KEY,
        COLLECT_SP_MARKER_KEY, //(extension) special marker key for bind with table name, fieldname 
        STRING_ESCAPE,
    }

    enum SqlSectionKind
    {
        SqlText,
        ValueKey,
        SpecialKey,
    }

    class SqlSection
    {
        public readonly SqlSectionKind SectionKind;
        public readonly string Text;
        public SqlSection(string text, SqlSectionKind sectionKind)
        {
            this.Text = text;
            this.SectionKind = sectionKind;
        }
#if DEBUG
        public override string ToString()
        {
            return Text;
        }
#endif
    }

    class SqlBoundSection : SqlSection
    {
        public FieldPacket fieldInfo;
        public SqlBoundSection(string text)
            : base(text, SqlSectionKind.ValueKey)
        {
        }


#if DEBUG
        public override string ToString()
        {
            return Text;
        }
#endif
    }
}
namespace SharpConnect.MySql
{
    using SharpConnect.MySql.Internal;
    public class SqlStringTemplate
    {
        readonly List<SqlSection> _sqlSections = new List<SqlSection>(); //all sections 
        readonly List<SqlBoundSection> _valuesKeys = new List<SqlBoundSection>(); //only value keys        
        readonly List<SqlSection> _specialKeys = new List<SqlSection>();
        readonly string _userRawSql; //raw sql from user code
        public SqlStringTemplate(string rawSql, bool at_sign_is_binder = true)
        {
            _userRawSql = rawSql;
            //------------------------------
            //parse
            int length = rawSql.Length;
            ParseState state = ParseState.FIND_MARKER;
            StringBuilder stBuilder = new StringBuilder();
            //TODO: review parser state, escape ' or " or `

            char binderEscapeChar = '\0';
            char escapeChar = '\0';
            for (int i = 0; i < length; i++)
            {
                char ch = rawSql[i];
                switch (state)
                {
                    default:
                        //unknown state must throw exception, so we can see if something changed
                        throw new NotSupportedException();
                    case ParseState.FIND_MARKER:

                        if (ch == '@' && ((i + 1 < length) && rawSql[i + 1] == '\''))
                        {
                            //look ahead next char
                            //'user_a'@'%' is ok 
                        }
                        else
                        {
                            if (ch == '?' || (at_sign_is_binder && ch == '@'))
                            {
                                binderEscapeChar = ch;
                                //found begining point of new marker
                                if (stBuilder.Length > 0)
                                {
                                    _sqlSections.Add(new SqlSection(stBuilder.ToString(), SqlSectionKind.SqlText));
                                    stBuilder.Length = 0;
                                }
                                state = ParseState.COLLECT_MARKER_KEY;
                            }
                            else if (ch == '\'' || ch == '"' || ch == '`')
                            {
                                escapeChar = ch;
                                state = ParseState.STRING_ESCAPE;
                            }
                        }
                        stBuilder.Append(ch);
                        break;
                    case ParseState.COLLECT_MARKER_KEY:

                        if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '_')
                        {
                            stBuilder.Append(ch);
                        }
                        else if (ch == '?')
                        {
                            //this is special marker key
                            stBuilder.Append(ch);
                            if (binderEscapeChar == '?')
                            {
                                state = ParseState.COLLECT_SP_MARKER_KEY;
                            }
                            else
                            {
                                //eg ?@
                                //error
                                throw new NotSupportedException("syntax err!");
                            }
                        }
                        else if (at_sign_is_binder && ch == '@')
                        {
                            stBuilder.Append(ch);
                            if (binderEscapeChar == '@')
                            {
                                //@@ 
                                state = ParseState.FIND_MARKER; //goto normal text state
                            }
                            else
                            {
                                //eg @?
                                //eg ?@
                                //error
                                throw new NotSupportedException("syntax err!");
                            }
                        }
                        else
                        {
                            //value binding marking end here

                            if (stBuilder.Length > 0)
                            {
                                var valueSection = new SqlBoundSection(stBuilder.ToString());
                                _sqlSections.Add(valueSection);
                                _valuesKeys.Add(valueSection);
                                stBuilder.Length = 0;
                            }
                            state = ParseState.FIND_MARKER;
                            stBuilder.Append(ch);
                        }
                        break;
                    case ParseState.COLLECT_SP_MARKER_KEY:
                        if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '_')
                        {
                            stBuilder.Append(ch);
                        }
                        else
                        {
                            //special marker end here
                            if (stBuilder.Length > 0)
                            {
                                var specialSection = new SqlSection(stBuilder.ToString(), SqlSectionKind.SpecialKey);
                                _sqlSections.Add(specialSection);
                                _specialKeys.Add(specialSection);
                                stBuilder.Length = 0;
                            }
                            state = ParseState.FIND_MARKER;
                            stBuilder.Append(ch);
                        }
                        break;
                    case ParseState.STRING_ESCAPE:
                        {
                            if (ch == '\'' || ch == '"' || ch == '`')
                            {
                                if (escapeChar == ch)
                                {
                                    escapeChar = '\0';
                                    //go back to find marker state
                                    state = ParseState.FIND_MARKER;
                                }
                            }
                            stBuilder.Append(ch);
                        }
                        break;
                }//end swicth
            }//end for


            if (stBuilder.Length > 0)
            {
                switch (state)
                {
                    default:
                        throw new NotSupportedException();
                    case ParseState.FIND_MARKER:
                        _sqlSections.Add(new SqlSection(stBuilder.ToString(), SqlSectionKind.SqlText));
                        break;
                    case ParseState.COLLECT_MARKER_KEY:
                        var valueSection = new SqlBoundSection(stBuilder.ToString());
                        _sqlSections.Add(valueSection);
                        _valuesKeys.Add(valueSection);
                        break;
                    case ParseState.COLLECT_SP_MARKER_KEY:
                        var specialSection = new SqlSection(stBuilder.ToString(), SqlSectionKind.SpecialKey);
                        _sqlSections.Add(specialSection);
                        _specialKeys.Add(specialSection);
                        break;
                }
            }
        }

        internal List<SqlBoundSection> GetValueKeys() => _valuesKeys;

        internal string UserRawSql => _userRawSql;

        static void AppendOnlyTime(System.Text.StringBuilder stbuilder, DateTime dtm)
        {
            string dtm_str = dtm.ToString("s");
            //yyyy-mm-ddThh:mm:ss
#if DEBUG
            //string time_only = dtm_str.Substring(10 + 1);
#endif
            stbuilder.Append(dtm_str.Substring(10 + 1));
        }
        static void AppendOnlyDate(System.Text.StringBuilder stbuilder, DateTime dtm)
        {
            string dtm_str = dtm.ToString("s");
            //yyyy-mm-ddThh:mm:ss
#if DEBUG
            //string date_only = dtm_str.Substring(0, 10);
#endif
            stbuilder.Append(dtm_str.Substring(0, 10));
        }
        static void FormatAndAppendData(StringBuilder stbuilder, ref MyStructData data)
        {
            //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            //TODO: review here , data range
            //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            switch (data.type)
            {
                case MySqlDataType.BLOB:
                case MySqlDataType.LONG_BLOB:
                case MySqlDataType.MEDIUM_BLOB:
                case MySqlDataType.TINY_BLOB:
                    MySqlStringToHexUtils.ConvertByteArrayToHexWithMySqlPrefix(data.myBuffer, stbuilder);
                    break;
                case MySqlDataType.DATE:
                case MySqlDataType.NEWDATE:
                    stbuilder.Append('\'');
                    AppendOnlyDate(stbuilder, data.myDateTime);
                    stbuilder.Append('\'');
                    break;
                case MySqlDataType.DATETIME:
                    stbuilder.Append('\'');
                    //#if DEBUG
                    //string preview_v = data.myDateTime.ToString("s");
                    //#endif
                    stbuilder.Append(data.myDateTime.ToString("s"));
                    stbuilder.Append('\'');
                    break;
                case MySqlDataType.TIMESTAMP:
                case MySqlDataType.TIME:
                    //TODO: review here
                    stbuilder.Append('\'');
                    AppendOnlyTime(stbuilder, data.myDateTime);
                    stbuilder.Append('\'');
                    break;
                case MySqlDataType.STRING:
                case MySqlDataType.VARCHAR:
                case MySqlDataType.VAR_STRING:

                    stbuilder.Append('\'');
                    //TODO: check /escape string here ****
                    stbuilder.Append(data.myString);
                    stbuilder.Append('\'');
                    break;
                case MySqlDataType.BIT:
                    stbuilder.Append(Encoding.ASCII.GetString(new byte[] { (byte)data.myInt32 }));
                    break;
                case MySqlDataType.DOUBLE:
                    stbuilder.Append(data.myDouble.ToString());
                    break;
                case MySqlDataType.FLOAT:
                    stbuilder.Append(((float)data.myDouble).ToString());
                    break;
                case MySqlDataType.TINY:
                case MySqlDataType.SHORT:
                case MySqlDataType.LONG:
                case MySqlDataType.INT24:
                case MySqlDataType.YEAR:
                    stbuilder.Append(data.myInt32.ToString());
                    break;
                case MySqlDataType.LONGLONG:
                    stbuilder.Append(data.myInt64.ToString());
                    break;
                case MySqlDataType.DECIMAL:
                    stbuilder.Append(data.myDecimal.ToString());
                    break;
                default:
                    stbuilder.Append(data.myUInt64.ToString());
                    break;
            }
        }
        internal string BindValues(CommandParams cmdParams, bool forPrepareStmt)
        {
            StringBuilder strBuilder = new StringBuilder();
            int count = _sqlSections.Count;
            for (int i = 0; i < count; i++)
            {
                var sqlSection = _sqlSections[i];
                switch (sqlSection.SectionKind)
                {
                    default:
                        throw new NotSupportedException();
                    case SqlSectionKind.SqlText:
                        strBuilder.Append(sqlSection.Text);
                        break;
                    case SqlSectionKind.ValueKey:


                        if (forPrepareStmt)
                        {
                            strBuilder.Append('?');
                        }
                        else
                        {
                            //get bind data 
                            if (!cmdParams.TryGetData(sqlSection.Text, out MyStructData bindedData))
                            {
                                throw new Exception("Error : This key not assign." + sqlSection.Text);
                            }
                            else
                            {
                                //format data 
                                FormatAndAppendData(strBuilder, ref bindedData);
                            }
                        }
                        break;
                    case SqlSectionKind.SpecialKey:
                        if (cmdParams.TryGetSqlPart(sqlSection.Text, out string found))
                        {
                            strBuilder.Append(found);
                        }
                        else
                        {
                            throw new Exception("not found " + sqlSection.Text);
                        }
                        break;
                }
            }

            return strBuilder.ToString();
        }
    }

}