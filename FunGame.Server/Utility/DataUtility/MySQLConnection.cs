using MySql.Data.MySqlClient;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Library.Server;

namespace Milimoe.FunGame.Server.Utility.DataUtility
{
    public class ConnectProperties
    {
        public static string Name { get; set; } = "";
        public static string DataSource { get; set; } = "";
        public static string Port { get; set; } = "";
        public static string DataBase { get; set; } = "";
        public static string User { get; set; } = "";
        public static string Password { get; set; } = "";

        /// <summary>
        /// 读取MySQL服务器配置文件
        /// </summary>
        /// <returns></returns>
        public static string GetConnectProperties()
        {
            if (Name == "" && DataSource == "" && Port == "" && DataBase == "" && User == "" && Password == "")
            {
                if (INIHelper.ExistINIFile())
                {
                    DataSource = INIHelper.ReadINI("MySQL", "DBServer");
                    Port = INIHelper.ReadINI("MySQL", "DBPort");
                    DataBase = INIHelper.ReadINI("MySQL", "DBName");
                    User = INIHelper.ReadINI("MySQL", "DBUser");
                    Password = INIHelper.ReadINI("MySQL", "DBPassword");
                    return "data source = " + DataSource + "; port = " + Port + "; database = " + DataBase + "; user = " + User + "; password = " + Password + "; charset = utf8mb4;";
                }
                else ServerHelper.Error(new MySQLConfigException());
            }
            return "data source = " + DataSource + "; port = " + Port + "; database = " + DataBase + "; user = " + User + "; password = " + Password + "; charset = utf8mb4;";
        }
    }

    public class MySQLConnection
    {
        public MySqlConnection? Connection
        {
            get
            {
                return _Connection;
            }
        }
        public SQLServerInfo ServerInfo
        {
            get
            {
                return _ServerInfo ?? SQLServerInfo.Create();
            }
        }

        private MySqlConnection? _Connection;
        private SQLServerInfo? _ServerInfo;

        /// <summary>
        /// 创建SQL连接
        /// </summary>
        /// <param name="serverInfo"></param>
        public MySQLConnection(out SQLServerInfo? serverInfo)
        {
            _Connection = Connect(out serverInfo);
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public void Close()
        {
            if (_Connection != null && _Connection.State == System.Data.ConnectionState.Open)
            {
                _Connection.Close();
            }
            _Connection = null;
        }

        /// <summary>
        /// 连接MySQL服务器
        /// </summary>
        /// <param name="serverInfo">服务器信息</param>
        /// <returns>连接对象</returns>
        /// <exception cref="MySQLConfigException">MySQL服务启动失败：无法找到MySQL配置文件</exception>
        private MySqlConnection? Connect(out SQLServerInfo? serverInfo)
        {
            try
            {
                string _GetConnection = ConnectProperties.GetConnectProperties();
                if (_GetConnection != null)
                {
                    string[] DataSetting = _GetConnection.Split(";");
                    if (DataSetting.Length > 1 && DataSetting[0].Length > 14 && DataSetting[1].Length > 8)
                    {
                        ServerHelper.WriteLine("Connect -> MySQL://" + DataSetting[0][14..] + ":" + DataSetting[1][8..]);
                    }
                    _Connection = new MySqlConnection(_GetConnection);
                    _Connection.Open();
                    if (_Connection.State == System.Data.ConnectionState.Open)
                    {
                        _ServerInfo = SQLServerInfo.Create(ConnectProperties.Name, ConnectProperties.DataSource, ConnectProperties.Port, ConnectProperties.DataBase, ConnectProperties.User, ConnectProperties.Password);
                        ServerHelper.WriteLine("Connected: MySQL服务器连接成功");
                    }
                }
                else
                {
                    throw new MySQLConfigException();
                }
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
            }

            serverInfo = _ServerInfo;
            return _Connection;
        }
    }
}
