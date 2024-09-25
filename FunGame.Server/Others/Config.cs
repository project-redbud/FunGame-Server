using System.Collections;
using System.Text;
using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Library.SQLScript.Common;
using Milimoe.FunGame.Core.Library.SQLScript.Entity;
using Milimoe.FunGame.Core.Model;
using Milimoe.FunGame.Server.Utility;
using Milimoe.FunGame.Server.Utility.DataUtility;

namespace Milimoe.FunGame.Server.Others
{
    public static class Config
    {
        /// <summary>
        /// 服务器名称
        /// </summary>
        public static string ServerName { get; set; } = "FunGame Server";

        /// <summary>
        /// Socket 端口
        /// </summary>
        public static int ServerPort { get; set; } = 22222;

        /// <summary>
        /// 使用 WebSocket
        /// </summary>
        public static bool UseWebSocket { get; set; } = false;

        /// <summary>
        /// WebSocket 监听地址
        /// </summary>
        public static string WebSocketAddress { get; set; } = "localhost";

        /// <summary>
        /// WebSocket 端口
        /// </summary>
        public static int WebSocketPort { get; set; } = 22222;
        
        /// <summary>
        /// WebSocket 监听子路径
        /// </summary>
        public static string WebSocketSubUrl { get; set; } = "ws";

        /// <summary>
        /// WebSocket 开启 SSL
        /// </summary>
        public static bool WebSocketSSL { get; set; } = false;

        /// <summary>
        /// 默认状态：1可连接 0不可连接 -1不可用
        /// </summary>
        public static int ServerStatus { get; set; } = 1;

        /// <summary>
        /// 服务器的公告
        /// </summary>
        public static string ServerNotice { get; set; } = "";

        /// <summary>
        /// 服务器的密码
        /// </summary>
        public static string ServerPassword { get; set; } = "";

        /// <summary>
        /// 服务器的描述
        /// </summary>
        public static string ServerDescription { get; set; } = "";

        /// <summary>
        /// 注册社区服务器的Key
        /// </summary>
        public static string ServerKey { get; set; } = "";

        /// <summary>
        /// 禁止连接的黑名单
        /// </summary>
        public static List<string> ServerBannedList { get; set; } = [];

        /// <summary>
        /// 最多接受连接的玩家数量
        /// </summary>
        public static int MaxPlayers { get; set; } = 20;

        /// <summary>
        /// 最大连接失败次数
        /// </summary>
        public static int MaxConnectionFaileds { get; set; } = 5;

        /// <summary>
        /// 已连接的玩家数量
        /// </summary>
        public static int OnlinePlayerCount { get; set; } = 0;

        /// <summary>
        /// 正在连接的玩家数量
        /// </summary>
        public static int ConnectingPlayerCount { get; set; } = 0;

        /// <summary>
        /// 默认传输字符集
        /// </summary>
        public static Encoding DefaultEncoding { get; } = General.DefaultEncoding;

        /// <summary>
        /// FunGame Runtime
        /// </summary>
        public static FunGameInfo.FunGame FunGameType => FunGameInfo.FunGame.FunGame_Server;

        /// <summary>
        /// 服务器指令列表
        /// </summary>
        public static Hashtable OrderList { get; } = [];

        /// <summary>
        /// 在线房间列表
        /// </summary>
        public static RoomList RoomList { get; } = new();

        /// <summary>
        /// 是否运行数据库模式
        /// </summary>
        public static SQLMode SQLMode { get; set; } = SQLMode.None;

        /// <summary>
        /// Server实际安装的模组
        /// </summary>
        public static GameModuleLoader? GameModuleLoader { get; set; }

        /// <summary>
        /// 未Loadmodules时，此属性表示至少需要安装的模组
        /// </summary>
        public static string[] GameModuleSupported { get; set; } = [];

        /// <summary>
        /// 全局数据库连接器
        /// </summary>
        public static SQLHelper SQLHelper
        {
            get
            {
                if (_SQLHelper is null) throw new SQLServiceException();
                return _SQLHelper;
            }
        }

        private static SQLHelper? _SQLHelper;

        /// <summary>
        /// 初始化数据库连接器
        /// </summary>
        public static void InitSQLHelper()
        {
            try
            {
                if (INIHelper.ExistINIFile())
                {
                    if (INIHelper.ReadINI("MySQL", "UseMySQL").Trim() == "true")
                    {
                        _SQLHelper = new MySQLHelper("", false);
                        if (((MySQLHelper)_SQLHelper).Connection != null)
                        {
                            SQLMode = _SQLHelper.Mode;
                            ServerLogin();
                            ClearRoomList();
                        }
                    }
                    else if (INIHelper.ReadINI("SQLite", "UseSQLite").Trim() == "true")
                    {
                        _SQLHelper = new SQLiteHelper();
                        SQLMode = _SQLHelper.Mode;
                        ServerLogin();
                        ClearRoomList();
                    }
                    else SQLMode = SQLMode.None;
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
        public static void ServerLogin()
        {
            if (SQLMode != SQLMode.None)
            {
                SQLHelper.Execute(ServerLoginLogs.Insert_ServerLoginLogs(ServerName, ServerKey));
            }
        }

        /// <summary>
        /// 重启服务器后，所有房间都会被删除
        /// </summary>
        public static void ClearRoomList()
        {
            if (SQLMode != SQLMode.None)
            {
                SQLHelper.Execute(RoomQuery.Delete_Rooms());
            }
        }
    }

    /// <summary>
    /// 此服务器的联系邮箱
    /// </summary>
    public static class OfficialEmail
    {
        public static string Email { get; set; } = "";
        public static string SupportEmail { get; set; } = "";
    }
}
