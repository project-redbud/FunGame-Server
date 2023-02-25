using MySql.Data.MySqlClient;
using Milimoe.FunGame.Core.Api.Utility;

namespace Milimoe.FunGame.Server.Utility.DataUtility
{
    public class MySQLConnection
    {
        public static MySqlConnection? Connection = null;

        public static string Name { get; set; } = "";
        public static string DataSource { get; set; } = "";
        public static string Port { get; set; } = "";
        public static string DataBase { get; set; } = "";
        public static string User { get; set; } = "";
        public static string Password { get; set; } = "";
        public static string GetConnection { get; set; } = "";

        public static bool Connect(out MySqlConnection? conn)
        {
            try
            {
                GetConnection = GetConnectProperties();
                if (GetConnection != null)
                {
                    string[] DataSetting = GetConnection.Split(";");
                    if (DataSetting.Length > 1 && DataSetting[0].Length > 14 && DataSetting[1].Length > 8)
                    {
                        ServerHelper.WriteLine("Connect -> MySQL://" + DataSetting[0][14..] + ":" + DataSetting[1][8..]);
                    }
                    Connection = new MySqlConnection(GetConnection);
                    Connection.Open();
                    if (Connection.State == System.Data.ConnectionState.Open)
                    {
                        ServerHelper.WriteLine("Connected: MySQL服务器连接成功");
                        conn = Connection;
                        return true;
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
            conn = Connection;
            return false;
        }

        public static void Close()
        {
            if (Connection != null && Connection.State == System.Data.ConnectionState.Open)
            {
                Connection.Close();
            }
            Connection = null;
        }

        private static string GetConnectProperties()
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
            else ServerHelper.Error(new Exception("找不到配置文件。"));
            return "";
        }
    }
}
