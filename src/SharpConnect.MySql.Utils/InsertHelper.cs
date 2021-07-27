//MIT, 2016-present, brezza92, EngineKit and contributors

using System;
using System.Collections.Generic;
using System.Text;
using SharpConnect.MySql;
using SharpConnect.MySql.Information;
namespace SharpConnect.MySql.Utils
{
    public class MySqlInsertQuery
    {
        string _targetTableName;
        Dictionary<string, object> _values = new Dictionary<string, object>();
        Encoding _defaultEnc;

        public MySqlInsertQuery(string targetTableName)
        {
            _targetTableName = targetTableName;
            _defaultEnc = Encoding.UTF8;
        }
        public Encoding DefaultStringEncoding
        {
            get => _defaultEnc;
            set => _defaultEnc = value;
        }
        public void AddValue(string columnName, object value)
        {
            if (value is string v)
            {                
                _values.Add(columnName, _defaultEnc.GetBytes(v));
            }
            else
            {
                _values.Add(columnName, value);
            }
        }
        public void AddValue(string columnName, string value)
        {
            _values.Add(columnName, _defaultEnc.GetBytes((string)value));
        }
        string BuildQueryString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("insert into ");
            sb.Append(_targetTableName);
            sb.Append('(');
            int lim = _values.Count - 1;
            int i = 0;

            foreach (string k in _values.Keys)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                sb.Append(k);
                i++;
            }
            sb.Append(") values(");
            i = 0;
            //-----------------------------
            foreach (string k in _values.Keys)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                sb.Append('?');
                sb.Append(k);

                i++;
            }
            sb.Append(')');


            if (!string.IsNullOrEmpty(this.OnDuplicateKeyStr))
            {
                sb.Append(" on duplicate key ");
                sb.Append(this.OnDuplicateKeyStr);
            }
            return sb.ToString();

        }
        public string OnDuplicateKeyStr { get; set; }
        public MySqlCommand BuildCommand()
        {
            MySqlCommand cmd = new MySqlCommand(BuildQueryString());
            var pars = cmd.Parameters;
            foreach (KeyValuePair<string, object> kp in _values)
            {
                pars.AddWithValue("?" + kp.Key, kp.Value);
            }
            return cmd;
        }
    }

}