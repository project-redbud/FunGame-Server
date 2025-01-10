using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Library.Common.Addon;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Library.SQLScript.Common;
using Milimoe.FunGame.Core.Library.SQLScript.Entity;
using Milimoe.FunGame.Server.Utility;
using Milimoe.FunGame.Server.Utility.DataUtility;

namespace Milimoe.FunGame.Server.Others
{
    public class FunGameSystem
    {
        /// <summary>
        /// 初始化数据库连接器
        /// </summary>
        public static void InitSQLHelper()
        {
            try
            {
                Factory.OpenFactory.RegisterFactory(() => Config.SQLMode switch
                {
                    SQLMode.MySQL => new MySQLHelper(),
                    SQLMode.SQLite => new SQLiteHelper(),
                    _ => null,
                });
                if (INIHelper.ExistINIFile())
                {
                    string useMySQL = INIHelper.ReadINI("MySQL", "UseMySQL").Trim();
                    string useSQLite = INIHelper.ReadINI("SQLite", "UseSQLite").Trim();

                    // 根据配置文件选择适当的 SQLHelper 实例
                    SQLHelper? sqlHelper = useMySQL == "true" ? new MySQLHelper() : (useSQLite == "true" ? new SQLiteHelper() : null);
                    if (sqlHelper != null)
                    {
                        Config.SQLMode = sqlHelper.Mode;
                        switch (Config.SQLMode)
                        {
                            case SQLMode.MySQL:
                                ServerHelper.WriteLine("Connect -> MySQL://" + sqlHelper.ServerInfo.SQLServerIP + ":" + sqlHelper.ServerInfo.SQLServerPort);
                                break;

                            case SQLMode.SQLite:
                                ServerHelper.WriteLine("Connect -> SQLite://" + sqlHelper.ServerInfo.SQLServerDataBase);
                                break;
                        }
                        AfterCreateSQLService(sqlHelper);
                    }
                    else
                    {
                        Config.SQLMode = SQLMode.None;
                        ServerHelper.WriteLine("未开启 SQL 服务，某些请求将无法处理。", InvokeMessageType.Warning);
                    }
                }
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
            }
        }

        /// <summary>
        /// 初始化邮件发送器
        /// </summary>
        public static void InitMailSender()
        {
            try
            {
                Factory.OpenFactory.RegisterFactory(SmtpHelper.GetMailSender);
                MailSender? sender = SmtpHelper.GetMailSender();
                if (sender != null)
                {
                    ServerHelper.WriteLine("SMTP 服务已启动！");
                    sender.Dispose();
                }
                else
                {
                    ServerHelper.WriteLine("SMTP 服务处于关闭状态", InvokeMessageType.Warning);
                }
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
            }
        }

        /// <summary>
        /// 加载游戏模组
        /// </summary>
        /// <returns></returns>
        public static bool GetGameModuleList()
        {
            List<string> supported = [];
            // 构建AddonController
            Dictionary<string, object> delegates = [];
            delegates.Add("WriteLine", new Action<string>(msg => ServerHelper.WriteLine(msg, InvokeMessageType.GameModule)));
            delegates.Add("Error", new Action<Exception>(ServerHelper.Error));
            // 读取modules目录下的模组
            try
            {
                Config.GameModuleLoader = GameModuleLoader.LoadGameModules(Config.FunGameType, delegates);
                foreach (GameModuleServer module in Config.GameModuleLoader.ModuleServers.Values)
                {
                    try
                    {
                        bool check = true;
                        // 检查模组是否有相对应的地图
                        if (!Config.GameModuleLoader.Maps.ContainsKey(module.DefaultMap))
                        {
                            ServerHelper.WriteLine("GameModule Load Failed: " + module + " 没有找到相对应的地图，加载失败", InvokeMessageType.Error);
                            check = false;
                        }
                        if (check)
                        {
                            supported.Add(module.Name);
                        }
                    }
                    catch (Exception e)
                    {
                        ServerHelper.Error(e);
                    }
                }
            }
            catch (Exception e2)
            {
                ServerHelper.Error(e2);
            }
            // 设置全局
            Config.GameModuleSupported = supported.Distinct().ToArray();
            foreach (string modename in Config.GameModuleSupported)
            {
                ServerHelper.WriteLine("Loaded: " + modename, InvokeMessageType.GameModule);
            }

            return Config.GameModuleSupported.Length > 0;
        }

        /// <summary>
        /// 加载服务器插件
        /// </summary>
        public static void GetServerPlugins()
        {
            Dictionary<string, object> delegates = [];
            delegates.Add("WriteLine", new Action<string>(msg => ServerHelper.WriteLine(msg, InvokeMessageType.Plugin)));
            delegates.Add("Error", new Action<Exception>(ServerHelper.Error));
            try
            {
                // 读取plugins目录下的插件
                Config.ServerPluginLoader = ServerPluginLoader.LoadPlugins(delegates);
                foreach (ServerPlugin plugin in Config.ServerPluginLoader.Plugins.Values)
                {
                    ServerHelper.WriteLine("Loaded: " + plugin.Name, InvokeMessageType.Plugin);
                }
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
            }
        }

        /// <summary>
        /// 加载 Web API 插件
        /// </summary>
        public static void GetWebAPIPlugins()
        {
            Dictionary<string, object> delegates = [];
            delegates.Add("WriteLine", new Action<string>(msg => ServerHelper.WriteLine(msg, InvokeMessageType.Plugin)));
            delegates.Add("Error", new Action<Exception>(ServerHelper.Error));
            try
            {
                // 读取plugins目录下的插件
                Config.WebAPIPluginLoader = WebAPIPluginLoader.LoadPlugins(delegates);
                foreach (WebAPIPlugin plugin in Config.WebAPIPluginLoader.Plugins.Values)
                {
                    ServerHelper.WriteLine("Loaded: " + plugin.Name, InvokeMessageType.Plugin);
                }
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
            }
        }

        /// <summary>
        /// 服务器启动登记
        /// </summary>
        public static void ServerLogin(SQLHelper sqlHelper)
        {
            sqlHelper.Execute(ServerLoginLogs.Insert_ServerLoginLogs(sqlHelper, Config.ServerName, Config.ServerKey));
        }

        /// <summary>
        /// 重启服务器后，所有房间都会被删除
        /// </summary>
        public static void ClearRoomList(SQLHelper sqlHelper)
        {
            sqlHelper.Execute(RoomQuery.Delete_Rooms(sqlHelper));
        }

        /// <summary>
        /// 创建 SQL 服务后需要做的事
        /// </summary>
        /// <param name="sqlHelper"></param>
        public static void AfterCreateSQLService(SQLHelper sqlHelper)
        {
            Config.SQLMode = sqlHelper.Mode;
            if (sqlHelper is SQLiteHelper sqliteHelper && !sqliteHelper.DatabaseExists())
            {
                ServerHelper.WriteLine("正在初始化数据库 . . .", InvokeMessageType.Core);
                sqliteHelper.ExecuteSqlFile(AppDomain.CurrentDomain.BaseDirectory + "fungame_sqlite.sql");
            }
            ServerLogin(sqlHelper);
            ClearRoomList(sqlHelper);
            sqlHelper.Dispose();
        }

        /// <summary>
        /// 关闭服务器要做的事
        /// </summary>
        public static void CloseServer()
        {
            if (Config.GameModuleLoader != null)
            {
                foreach (GameModuleServer server in Config.GameModuleLoader.ModuleServers.Values)
                {
                    server.Controller.Close();
                }
            }
            if (Config.ServerPluginLoader != null)
            {
                foreach (ServerPlugin plugin in Config.ServerPluginLoader.Plugins.Values)
                {
                    plugin.Controller.Close();
                }
            }
            if (Config.WebAPIPluginLoader != null)
            {
                foreach (WebAPIPlugin plugin in Config.WebAPIPluginLoader.Plugins.Values)
                {
                    plugin.Controller.Close();
                }
            }
        }
    }
}
