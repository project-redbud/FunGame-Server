using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Server.Services;

namespace Milimoe.FunGame.Server.Models
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
        public static string GetConnectPropertiesForMySQL()
        {
            if (Name == "" && DataSource == "" && Port == "" && DataBase == "" && User == "" && Password == "")
            {
                if (INIHelper.INIFileExists())
                {
                    DataSource = INIHelper.ReadINI("MySQL", "DBServer");
                    Port = INIHelper.ReadINI("MySQL", "DBPort");
                    DataBase = INIHelper.ReadINI("MySQL", "DBName");
                    User = INIHelper.ReadINI("MySQL", "DBUser");
                    Password = INIHelper.ReadINI("MySQL", "DBPassword");
                }
                else ServerHelper.Error(new MySQLConfigException());
            }
            return "data source = " + DataSource + "; port = " + Port + "; database = " + DataBase + "; user = " + User + "; password = " + Password + "; charset = utf8mb4;";
        }

        /// <summary>
        /// 读取SQLite服务器配置文件
        /// </summary>
        /// <returns></returns>
        public static string GetConnectPropertiesForSQLite()
        {
            if (DataSource == "")
            {
                if (INIHelper.INIFileExists())
                {
                    DataSource = INIHelper.ReadINI("SQLite", "DataSource");
                }
                else ServerHelper.Error(new SQLServiceException());
            }
            return $"data source={AppDomain.CurrentDomain.BaseDirectory}" + DataSource;
        }
    }
}
