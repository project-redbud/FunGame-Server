using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Library.Common.Addon;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Library.SQLScript.Common;
using Milimoe.FunGame.Core.Library.SQLScript.Entity;
using Milimoe.FunGame.Core.Model;
using Milimoe.FunGame.Server.Model;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Services.DataUtility;

namespace Milimoe.FunGame.Server.Services
{
    public class FunGameSystem
    {
        public delegate Task CloseListenerHandler();
        public static event CloseListenerHandler? CloseListener;

        /// <summary>
        /// 服务器指令列表
        /// </summary>
        public static Dictionary<string, Action<string>> OrderList { get; } = [];

        /// <summary>
        /// 在线房间列表
        /// </summary>
        public static RoomList RoomList { get; } = new();

        /// <summary>
        /// Server实际安装的模组
        /// </summary>
        public static GameModuleLoader? GameModuleLoader { get; set; }

        /// <summary>
        /// Server插件
        /// </summary>
        public static ServerPluginLoader? ServerPluginLoader { get; set; }

        /// <summary>
        /// Web API插件
        /// </summary>
        public static WebAPIPluginLoader? WebAPIPluginLoader { get; set; }

        /// <summary>
        /// 服务器配置
        /// </summary>
        public static PluginConfig UserKeys { get; set; } = new("system", "user_keys");

        /// <summary>
        /// 服务器配置
        /// </summary>
        public static PluginConfig LocalConfig { get; set; } = new("system", "local");

        /// <summary>
        /// 数据库配置
        /// </summary>
        public static PluginConfig SQLConfig { get; set; } = new("system", "sqlconfig");

        /// <summary>
        /// 默认的 Web API Token ID，在首次初始化数据库时生成一个 Secret Key
        /// </summary>
        public const string FunGameWebAPITokenID = "fungame_web_api";

        /// <summary>
        /// API Secret 文件名
        /// </summary>
        public const string APISecretFileName = ".apisecret";

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
                    SQLHelper? sqlHelper = useMySQL == "true" ? new MySQLHelper() : useSQLite == "true" ? new SQLiteHelper() : null;
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
            delegates.Add("WriteLine", new Action<string, string, LogLevel, bool>((name, msg, level, useLevel) => ServerHelper.WriteLine_Addons(name, msg, InvokeMessageType.GameModule, level, useLevel)));
            delegates.Add("Error", new Action<Exception>(ServerHelper.Error));
            // 读取modules目录下的模组
            try
            {
                GameModuleLoader = GameModuleLoader.LoadGameModules(Config.FunGameType, delegates);
                foreach (GameModuleServer module in GameModuleLoader.ModuleServers.Values)
                {
                    try
                    {
                        bool check = true;
                        // 检查模组是否有相对应的地图
                        if (!GameModuleLoader.Maps.ContainsKey(module.DefaultMap))
                        {
                            ServerHelper.WriteLine("GameModule Load Failed: " + module.Name + " 没有找到相对应的地图，加载失败", InvokeMessageType.Error);
                            check = false;
                        }
                        if (check)
                        {
                            if (!module.IsAnonymous) supported.Add(module.Name);
                            ServerHelper.WriteLine("GameModule Loaded -> " + module.Name, InvokeMessageType.Core);
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
            Config.GameModuleSupported = [.. supported.Distinct()];

            return Config.GameModuleSupported.Length > 0;
        }

        /// <summary>
        /// 加载服务器插件
        /// </summary>
        public static void GetServerPlugins()
        {
            Dictionary<string, object> delegates = [];
            delegates.Add("WriteLine", new Action<string, string, LogLevel, bool>((name, msg, level, useLevel) => ServerHelper.WriteLine_Addons(name, msg, InvokeMessageType.Plugin, level, useLevel)));
            delegates.Add("Error", new Action<Exception>(ServerHelper.Error));
            try
            {
                // 读取plugins目录下的插件
                ServerPluginLoader = ServerPluginLoader.LoadPlugins(delegates);
                foreach (ServerPlugin plugin in ServerPluginLoader.Plugins.Values)
                {
                    ServerHelper.WriteLine("Plugin Loaded -> " + plugin.Name, InvokeMessageType.Core);
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
        public static void GetWebAPIPlugins(params object[] otherobjs)
        {
            Dictionary<string, object> delegates = [];
            delegates.Add("WriteLine", new Action<string, string, LogLevel, bool>((name, msg, level, useLevel) => ServerHelper.WriteLine_Addons(name, msg, InvokeMessageType.Plugin, level, useLevel)));
            delegates.Add("Error", new Action<Exception>(ServerHelper.Error));
            try
            {
                // 读取plugins目录下的插件
                WebAPIPluginLoader = WebAPIPluginLoader.LoadPlugins(delegates, otherobjs);
                foreach (WebAPIPlugin plugin in WebAPIPluginLoader.Plugins.Values)
                {
                    ServerHelper.WriteLine("Plugin Loaded -> " + plugin.Name, InvokeMessageType.Core);
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
            sqlHelper.Execute(ServerLoginLogs.Insert_ServerLoginLog(sqlHelper, Config.ServerName, Config.ServerKey));
        }

        /// <summary>
        /// 重启服务器后，所有房间都会被删除
        /// </summary>
        public static void ClearRoomList(SQLHelper sqlHelper)
        {
            sqlHelper.Execute(RoomQuery.Delete_Rooms(sqlHelper));
        }

        /// <summary>
        /// 初始化服务器其他配置文件
        /// </summary>
        public static void InitOtherConfig()
        {
            LocalConfig.LoadConfig();
            LocalConfig.SaveConfig();
            UserKeys.LoadConfig();
            UserKeys.SaveConfig();
        }

        /// <summary>
        /// 获取指定用户的密钥
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public static string GetUserKey(string username)
        {
            if (UserKeys.TryGetValue(username.ToLower(), out object? value) && value is string key)
            {
                return key;
            }
            return username;
        }

        /// <summary>
        /// 更新指定用户的密钥
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public static void UpdateUserKey(string username)
        {
            UserKeys.Add(username.ToLower(), Encryption.GenerateRandomString());
            UserKeys.SaveConfig();
        }

        /// <summary>
        /// 检查是否存在 API Secret Key
        /// </summary>
        /// <param name="key"></param>
        public static bool APISecretKeyExists(string key)
        {
            using SQLHelper? sql = Factory.OpenFactory.GetSQLHelper();
            if (sql != null)
            {
                key = Encryption.HmacSha256(key, Encryption.FileSha256(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, APISecretFileName)));
                sql.ExecuteDataSet(ApiTokens.Select_GetAPISecretKey(sql, key));
                if (sql.Result == SQLResult.Success)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 创建 API Secret Key
        /// </summary>
        /// <param name="token"></param>
        /// <param name="reference1"></param>
        /// <param name="reference2"></param>
        public static string CreateAPISecretKey(string token, string reference1 = "", string reference2 = "", SQLHelper? sqlHelper = null)
        {
            bool useSQLHelper = sqlHelper != null;
            sqlHelper ??= Factory.OpenFactory.GetSQLHelper();
            string key = Encryption.GenerateRandomString();
            string enKey = Encryption.HmacSha256(key, Encryption.FileSha256(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, APISecretFileName)));
            if (sqlHelper != null && enKey != "")
            {
                sqlHelper.ExecuteDataSet(ApiTokens.Select_GetAPIToken(sqlHelper, token));
                if (sqlHelper.Success)
                {
                    sqlHelper.Execute(ApiTokens.Update_APIToken(sqlHelper, token, enKey, reference1, reference2));
                }
                else
                {
                    sqlHelper.Execute(ApiTokens.Insert_APIToken(sqlHelper, token, enKey, reference1, reference2));
                }
            }
            else
            {
                ServerHelper.WriteLine($"API Secret Key '{token}' 创建失败，未连接到数据库或者找不到加密秘钥。", InvokeMessageType.Error);
            }
            if (!useSQLHelper)
            {
                sqlHelper?.Dispose();
            }
            return key;
        }

        /// <summary>
        /// 创建 SQL 服务后需要做的事，包括数据库初始化，API 初始化，首次建立管理员账户等等
        /// </summary>
        /// <param name="sqlHelper"></param>
        public static void AfterCreateSQLService(SQLHelper sqlHelper)
        {
            Config.SQLMode = sqlHelper.Mode;
            ServerHelper.WriteLine("正在检查数据库 . . .", InvokeMessageType.Core);
            if (!DatabaseExists() && !sqlHelper.DatabaseExists())
            {
                ServerHelper.WriteLine("数据库检查失败，正在初始化数据库 . . .", InvokeMessageType.Core);
                if (sqlHelper is SQLiteHelper sqliteHelper)
                {
                    sqliteHelper.ExecuteSqlFile(AppDomain.CurrentDomain.BaseDirectory + "fungame_sqlite.sql");
                }
                else if (sqlHelper is MySQLHelper mysqlHelper)
                {
                    mysqlHelper.ExecuteSqlFile(AppDomain.CurrentDomain.BaseDirectory + "fungame.sql");
                }
                ConsoleModel.FirstRunRegAdmin();
                using StreamWriter sw = File.CreateText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, APISecretFileName));
                sw.WriteLine(Encryption.GenerateRandomString());
                sw.Flush();
                sw.Close();
                ServerHelper.WriteLine($"已生成一个默认的 API Token，Token ID: {FunGameWebAPITokenID}, Secret Key: 【{CreateAPISecretKey(FunGameWebAPITokenID, sqlHelper: sqlHelper)}】，请妥善保管方括号内的内容，仅显示一次。如遗忘需要使用管理员账号重置。");
                sqlHelper.Execute(Configs.Insert_Config(sqlHelper, "Initialization", FunGameInfo.FunGame_Version, "SQL Service Installed."));
                SQLConfig.Clear();
                SQLConfig.Add("Initialized", true);
                SQLConfig.SaveConfig();
                ServerHelper.WriteLine("数据库初始化完毕！", InvokeMessageType.Core);
            }
            else ServerHelper.WriteLine("数据库检查通过！", InvokeMessageType.Core);
            ServerLogin(sqlHelper);
            ClearRoomList(sqlHelper);
            sqlHelper.Dispose();
        }

        /// <summary>
        /// 数据库是否存在
        /// </summary>
        /// <returns></returns>
        public static bool DatabaseExists()
        {
            SQLConfig.LoadConfig();
            if (SQLConfig.TryGetValue("Initialized", out object? value) && value is bool initialized)
            {
                return initialized;
            }
            return false;
        }

        /// <summary>
        /// 关闭服务器要做的事
        /// </summary>
        public static void CloseServer()
        {
            if (GameModuleLoader != null)
            {
                foreach (GameModuleServer server in GameModuleLoader.ModuleServers.Values)
                {
                    server.Controller.Close();
                }
            }
            if (ServerPluginLoader != null)
            {
                foreach (ServerPlugin plugin in ServerPluginLoader.Plugins.Values)
                {
                    plugin.Controller.Close();
                }
            }
            if (WebAPIPluginLoader != null)
            {
                foreach (WebAPIPlugin plugin in WebAPIPluginLoader.Plugins.Values)
                {
                    plugin.Controller.Close();
                }
            }
            // 停止所有正在运行的监听
            CloseListener?.Invoke();
        }
    }
}
