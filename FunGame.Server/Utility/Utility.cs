using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Entity;
using Milimoe.FunGame.Server.Others;
using MySql.Data.MySqlClient;
using System.Collections;

namespace Milimoe.FunGame.Server.Utility
{
    public class DataHelper
    {
        private static MySqlConnection? msc = null;

        public static string Name { get; set; } = "";
        public static string DataSource { get; set; } = "";
        public static string Port { get; set; } = "";
        public static string DataBase { get; set; } = "";
        public static string User { get; set; } = "";
        public static string Password { get; set; } = "";
        public static string GetConnection { get; set; } = "";

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

        private static string GetConnectProperties(string datasource, string port, string database, string user, string password)
        {
            DataSource = datasource;
            Port = port;
            DataBase = database;
            User = user;
            Password = password;
            return "data source = " + DataSource + "; port = " + Port + "; database = " + DataBase + "; user = " + User + "; password = " + Password + "; charset = utf8mb4;";
        }

        public static bool Connect(object[]? objs = null)
        {
            try
            {
                if (objs != null && objs.Length == 5)
                {
                    GetConnection = GetConnectProperties((string)objs[0], (string)objs[1], (string)objs[2], (string)objs[3], (string)objs[4]);
                }
                else GetConnection = GetConnectProperties();
                if (GetConnection != null)
                {
                    string[] DataSetting = GetConnection.Split(";");
                    if (DataSetting.Length > 1 && DataSetting[0].Length > 14 && DataSetting[1].Length > 8)
                    {
                        ServerHelper.WriteLine("Connect -> MySQL://" + DataSetting[0][14..] + ":" + DataSetting[1][8..]);
                    }
                    msc = new MySqlConnection(GetConnection);
                    msc.Open();
                    if (msc.State == System.Data.ConnectionState.Open)
                    {
                        ServerHelper.WriteLine("Connected: MySQL服务器连接成功");
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
            return false;
        }

        public static void Close()
        {
            if (msc != null && msc.State == System.Data.ConnectionState.Open)
            {
                msc.Close();
            }
            msc = null;
        }
    }

    public class ServerHelper
    {
        public static string GetPrefix()
        {
            DateTime now = System.DateTime.Now;
            return now.AddMilliseconds(-now.Millisecond).ToString() + " " + Config.SERVER_NAME + "：";
        }

        public static void Error(Exception e)
        {
            Console.Write("\r" + GetPrefix() + e.Message + "\n" + e.StackTrace + "\n\r> ");
        }

        public static void WriteLine(string? msg)
        {
            Console.Write("\r" + GetPrefix() + msg + "\n\r> ");
        }

        public static void Type()
        {
            Console.Write("\r> ");
        }

        private static Hashtable GetServerSettingHashtable()
        {
            Hashtable settings = new();
            if (INIHelper.ExistINIFile())
            {
                settings.Add("Name", INIHelper.ReadINI("Server", "Name"));
                settings.Add("Password", INIHelper.ReadINI("Server", "Password"));
                settings.Add("Describe", INIHelper.ReadINI("Server", "Describe"));
                settings.Add("Notice", INIHelper.ReadINI("Server", "Notice"));
                settings.Add("Key", INIHelper.ReadINI("Server", "Key"));
                settings.Add("Status", Convert.ToInt32(INIHelper.ReadINI("Server", "Status")));
                settings.Add("Port", Convert.ToInt32(INIHelper.ReadINI("Socket", "Port")));
                settings.Add("MaxPlayer", Convert.ToInt32(INIHelper.ReadINI("Socket", "MaxPlayer")));
                settings.Add("MaxConnectFailed", Convert.ToInt32(INIHelper.ReadINI("Socket", "MaxConnectFailed")));
            }
            return settings;
        }

        public static void GetServerSettings()
        {
            try
            {
                Hashtable settings = GetServerSettingHashtable();
                if (settings != null)
                {
                    string? Name = (string?)settings["Name"];
                    string? Password = (string?)settings["Password"];
                    string? Describe = (string?)settings["Describe"];
                    string? Notice = (string?)settings["Notice"];
                    string? Key = (string?)settings["Key"];
                    if (Name != null) Config.SERVER_NAME = Name;
                    if (Password != null) Config.SERVER_PASSWORD = Password;
                    if (Describe != null) Config.SERVER_DESCRIBE = Describe;
                    if (Notice != null) Config.SERVER_NOTICE = Notice;
                    if (Key != null) Config.SERVER_KEY = Key;
                    int? Status = (int?)settings["Status"];
                    int? Port = (int?)settings["Port"];
                    int? MaxPlayer = (int?)settings["MaxPlayer"];
                    int? MaxConnectFailed = (int?)settings["MaxConnectFailed"];
                    if (Status != null) Config.SERVER_STATUS = (int)Status;
                    if (Port != null) Config.SERVER_PORT = (int)Port;
                    if (MaxPlayer != null) Config.MAX_PLAYERS = (int)MaxPlayer;
                    if (MaxConnectFailed != null) Config.MAX_CONNECTFAILED = (int)MaxConnectFailed;
                }
            }
            catch (Exception e)
            {
                ServerHelper.WriteLine(e.StackTrace);
            }
        }

        public static void InitOrderList()
        {
            Config.OrderList.Clear();
            Config.OrderList.Add(OrderDictionary.Help, "Milimoe -> 帮助");
            Config.OrderList.Add(OrderDictionary.Quit, "关闭服务器");
            Config.OrderList.Add(OrderDictionary.Exit, "关闭服务器");
            Config.OrderList.Add(OrderDictionary.Close, "关闭服务器");
            Config.OrderList.Add(OrderDictionary.Restart, "重启服务器");
        }
    }

    public class SocketHelper
    {
        public static int GetType(string msg)
        {
            int index = msg.IndexOf(';') - 1;
            if (index > 0)
                return Convert.ToInt32(msg[..index]);
            else
                return Convert.ToInt32(msg[..1]);
        }

        public static string GetMessage(string msg)
        {
            int index = msg.IndexOf(';') + 1;
            return msg[index..];
        }

        public static string MakeMessage(int type, string msg)
        {
            return type + ";" + msg;
        }

        public static string MakeClientName(string name, User? user = null)
        {
            if (user != null)
            {
                return "玩家 " + user.Username;
            }
            if (name != "") return "客户端(" + name + ")";
            return "客户端";
        }
    }
}
