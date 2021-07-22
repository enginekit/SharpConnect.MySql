//MIT, 2016-present, brezza92, EngineKit and contributors

using System;
using System.Collections.Generic;

namespace SharpConnect.MySql.Information
{
    public class MySqlDbServerInfo
    {
        public MySqlDbServerInfo() { }
        public MySqlDbServerInfo(string name)
        {
            this.Name = name;
        }
        /// <summary>
        /// name of this server
        /// </summary>
        public string Name { get; private set; }
        public Dictionary<string, MySqlDatabaseInfo> Databases { get; set; }
#if DEBUG
        public override string ToString()
        {
            return "server:" + Name;
        }
#endif
    }

    public class MySqlDatabaseInfo
    {
        public MySqlDatabaseInfo() { }
        public MySqlDatabaseInfo(string name)
        {
            this.Name = name;
        }
        public string Name { get; set; }
        public List<MySqlTableInfo> Tables { get; set; }
        public List<MySqlStoreProcInfo> StoreProcs { get; set; }
        public List<MySqlStoreFuncInfo> StoreFuncs { get; set; }
        public MySqlDbServerInfo OwnerDbServer { get; set; }
        public string Sql { get; set; }//?
#if DEBUG
        public override string ToString()
        {
            return "db:" + Name;
        }
#endif
    }


    public class MySqlTableInfo
    {
        public MySqlTableInfo() { }
        public MySqlTableInfo(string name)
        {
            this.Name = name;
        }
        public string Name { get; set; }
        public List<MySqlColumnInfo> Columns { get; set; }
        public MySqlDatabaseInfo OwnerDatabase { get; set; }
        public string Sql { get; set; }
#if DEBUG
        public override string ToString()
        {
            return "table:" + Name;
        }
#endif
    }

    public class MySqlColumnInfo
    {
        public MySqlColumnInfo()
        {
        }
        public string FieldTypeName { get; set; }
        public string Name { get; set; }
        public bool Nullable { get; set; }
        public string Key { get; set; }
        public string DefaultValue { get; set; }
        public string ExtraInfo { get; set; }
#if DEBUG
        public override string ToString()
        {
            return "col:" + Name;
        }
#endif
    }

    public class MySqlStoreProcInfo
    {
        public MySqlStoreProcInfo() { }
        public MySqlStoreProcInfo(string name)
        {
            this.Name = name;
        }
        public MySqlDatabaseInfo OwnerDatabase { get; set; }
        public string Name { get; set; }
        public string Sql { get; set; }
#if DEBUG
        public override string ToString()
        {
            return "proc:" + Name;
        }
#endif
    }
    public class MySqlStoreFuncInfo
    {
        public MySqlStoreFuncInfo() { }
        public MySqlStoreFuncInfo(string name)
        {
            this.Name = name;
        }
        public MySqlDatabaseInfo OwnerDatabase { get; set; }
        public string Name { get; set; }
        public string Sql { get; set; }
#if DEBUG
        public override string ToString()
        {
            return "func:" + Name;
        }
#endif
    }

}