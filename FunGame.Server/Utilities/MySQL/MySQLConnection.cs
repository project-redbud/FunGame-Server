using Milimoe.FunGame.Core.Model;
using MySql.Data.MySqlClient;

namespace Milimoe.FunGame.Server.Utility.DataUtility
{
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
                string connectionString = ConnectProperties.GetConnectPropertiesForMySQL();
                if (connectionString != null)
                {
                    string[] strings = connectionString.Split(";");
                    if (strings.Length > 1 && strings[0].Length > 14 && strings[1].Length > 8)
                    {
                        ServerHelper.WriteLine("Connect -> MySQL://" + strings[0][14..] + ":" + strings[1][8..]);
                    }
                    _Connection = new MySqlConnection(connectionString);
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
